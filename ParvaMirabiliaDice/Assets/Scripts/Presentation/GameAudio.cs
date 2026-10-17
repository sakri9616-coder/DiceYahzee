using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Sound for the duel. The clips live in Resources/Audio so the game never needs inspector
    /// wiring, and every rule-facing class stays silent: only the presentation layer calls in.
    /// Clips are AI generated (StepAudio, StepFun official API); the provenance record lives in
    /// docs/ASSET_LICENSES.md.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        private static readonly string[] ClipNames =
        {
            "dice_roll", "die_keep", "ui_click", "hit", "guard", "hurt", "commit", "win", "lose",
            "formation"
        };

        // Preserve the old four-step preference when upgrading existing installations.
        private static readonly float[] VolumeSteps = { 1f, 0.6f, 0.3f, 0f };
        private const float BgmGain = 0.315f; // 30% below the former 0.45 gain.

        /// <summary>RMS the interface sounds are lifted to; impulses stay louder by nature.</summary>
        private const float TargetRms = 0.06f;

        /// <summary>
        /// Clips that must not run past a fixed length. The commit stamp carried three seconds of
        /// decay, which felt long against a round that takes a handful of clicks.
        /// </summary>
        private static readonly Dictionary<string, float> MaxDuration = new Dictionary<string, float>
        {
            // The source has a second full tumble after a long quiet gap. Keep
            // the first tumble and its tail only, with the shared edge fade.
            { "dice_roll", 1.7f },
            { "commit", 2.0f },
            { "ui_click", 1.0f },
            { "die_keep", 1.0f },
            { "formation", 1.5f }
        };

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _gains = new Dictionary<string, float>();
        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private float _volume;
        private float _lastAudibleVolume = 1f;
        private bool _volumeSavePending;
        private float _volumeSaveAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<GameAudio>() != null)
                return;

            new GameObject("ParvaMirabiliaAudio").AddComponent<GameAudio>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            for (int i = 0; i < ClipNames.Length; i++)
            {
                var clip = Resources.Load<AudioClip>("Audio/" + ClipNames[i]);
                if (clip == null)
                    continue;

                float gain;
                _clips[ClipNames[i]] = PrepareSfx(ClipNames[i], clip, out gain);
                _gains[ClipNames[i]] = gain;
            }

            _volume = PlayerPrefs.HasKey("pv.audioVolume")
                ? Mathf.Clamp01(PlayerPrefs.GetFloat("pv.audioVolume"))
                : VolumeSteps[Mathf.Clamp(PlayerPrefs.GetInt("pv.audioStep", 0), 0, VolumeSteps.Length - 1)];
            if (_volume > 0.001f) _lastAudibleVolume = _volume;

            var bgm = Resources.Load<AudioClip>("Audio/bgm_duel");
            if (bgm != null)
            {
                _bgmSource.clip = PrepareBgm(bgm);
                _bgmSource.Play();
            }

            ApplyVolumes();
        }

        /// <summary>Continuous master volume, 0 when muted.</summary>
        public static float Volume
        {
            get { return Instance == null ? 0f : Instance._volume; }
        }

        public static string VolumeLabel()
        {
            float volume = Volume;
            if (volume <= 0.001f)
                return "音 OFF";

            return "音 " + Mathf.RoundToInt(volume * 100f) + "%";
        }

        public static void SetVolume(float value)
        {
            if (Instance == null)
                return;
            Instance._volume = Mathf.Clamp01(value);
            if (Instance._volume > 0.001f) Instance._lastAudibleVolume = Instance._volume;
            // The registry write is cheap, so the value is always current; the costly Save is
            // postponed until the slider has been still for a moment, because one drag fires
            // OnValueChanged dozens of times.
            PlayerPrefs.SetFloat("pv.audioVolume", Instance._volume);
            Instance.QueueVolumeSave();
            Instance.ApplyVolumes();
        }

        /// <summary>Writes the pending volume preference only once the slider has stopped moving.</summary>
        private void QueueVolumeSave()
        {
            _volumeSaveAt = Time.unscaledTime;
            if (!_volumeSavePending)
            {
                _volumeSavePending = true;
                StartCoroutine(SaveVolumeWhenIdle());
            }
        }

        private System.Collections.IEnumerator SaveVolumeWhenIdle()
        {
            while (Time.unscaledTime - _volumeSaveAt < .5f)
                yield return new WaitForSecondsRealtime(.25f);
            _volumeSavePending = false;
            PlayerPrefs.Save();
        }

        /// <summary>Flushes the pending volume save when the debouncer is cut short, so the
        /// waiting coroutine cannot leave the preference unsaved for this session.</summary>
        private void OnDisable()
        {
            if (!_volumeSavePending) return;
            _volumeSavePending = false;
            PlayerPrefs.Save();
        }

        public static void ToggleMute()
        {
            if (Instance != null) SetVolume(Volume <= 0.001f ? Instance._lastAudibleVolume : 0f);
        }

        public static void Play(string clipName)
        {
            if (Instance == null)
                return;

            AudioClip clip;
            if (!Instance._clips.TryGetValue(clipName, out clip) || clip == null)
                return;

            float gain;
            if (!Instance._gains.TryGetValue(clipName, out gain))
                gain = 1f;

            // volumeScale is a multiplier, so gains above 1 are allowed; PrepareSfx already
            // capped the gain so the result stays below the clipping point.
            Instance._sfxSource.PlayOneShot(clip, Mathf.Max(0f, gain));
        }

        /// <summary>
        /// Cuts the silence the generator leaves around a sound effect and measures a makeup gain
        /// from the RMS of what is left. Two things come out of it: a die click no longer sits on
        /// six seconds of nothing, and a quiet click and a loud fanfare reach a similar level
        /// without ever asking the mixer for a clipped peak.
        /// </summary>
        private static AudioClip PrepareSfx(string name, AudioClip source, out float gain)
        {
            gain = 1f;

            int channels = source.channels;
            int total = source.samples;
            if (total <= 0 || channels <= 0)
                return source;

            var data = new float[total * channels];
            if (!source.GetData(data, 0))
                return source;

            int window = Mathf.Max(64, source.frequency / 20);   // 50 ms
            int first = -1;
            int last = -1;

            for (int start = 0; start < total; start += window)
            {
                double sum = 0.0;
                int count = 0;
                for (int i = start; i < start + window && i < total; i++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        float v = data[i * channels + c];
                        sum += (double)v * v;
                    }
                    count++;
                }

                float rms = (float)Math.Sqrt(sum / Math.Max(1, count * channels));
                if (rms < 0.01f)
                    continue;

                if (first < 0)
                    first = start;
                last = start + window;
            }

            if (first < 0)
                return source;                                  // the whole file is silence

            int from = Mathf.Max(0, first - window / 2);        // keep 25 ms of head room
            int to = Mathf.Min(total, last + window * 3);       // keep 150 ms of tail room
            if (to - from < source.frequency / 20)
                to = Mathf.Min(total, from + source.frequency / 20);

            int length = to - from;
            if (length <= 0)
                return source;

            // A clip with a length ceiling is cut here, and every cut gets a short fade so the
            // sound never ends on a step.
            float capSeconds;
            if (MaxDuration.TryGetValue(name, out capSeconds))
                length = Mathf.Min(length, Mathf.RoundToInt(capSeconds * source.frequency));

            if (length <= 0)
                return source;

            var slice = new float[length * channels];
            Array.Copy(data, from * channels, slice, 0, slice.Length);

            int edgeFade = Mathf.Min(Mathf.RoundToInt(0.06f * source.frequency), length / 2);
            for (int i = 0; i < edgeFade; i++)
            {
                float w = (i + 1) / (float)edgeFade;
                int edge = (length - 1 - i) * channels;
                for (int c = 0; c < channels; c++)
                    slice[edge + c] *= w;
            }

            double energy = 0.0;
            float peak = 0f;
            for (int i = 0; i < slice.Length; i++)
            {
                energy += (double)slice[i] * slice[i];
                float v = Mathf.Abs(slice[i]);
                if (v > peak)
                    peak = v;
            }

            float trimmedRms = (float)Math.Sqrt(energy / Math.Max(1, slice.Length));
            if (trimmedRms > 0.0001f)
            {
                gain = Mathf.Clamp(TargetRms / trimmedRms, 0.4f, 4f);
                if (peak > 0.001f)
                    gain = Mathf.Min(gain, 0.9f / peak);        // never ask for a clipped peak
            }

            if (length == total && edgeFade == 0)
                return source;                                  // nothing was touched

            var trimmed = AudioClip.Create(name, length, channels, source.frequency, false);
            trimmed.SetData(slice, 0);
            return trimmed;
        }

        /// <summary>
        /// Prepares the loop. The generated track carries dead air at both ends — a lead-in of
        /// about 0.6 s and a run-out of about 1.6 s — which would leave a hole at the seam on
        /// every pass. Both are trimmed down to 50 ms and the last 100 ms is faded out, so the
        /// seam becomes a short breath with soft edges instead of a pause.
        /// </summary>
        private static AudioClip PrepareBgm(AudioClip source)
        {
            int channels = source.channels;
            int total = source.samples;
            if (total <= 0 || channels <= 0)
                return source;

            var data = new float[total * channels];
            if (!source.GetData(data, 0))
                return source;

            int window = Mathf.Max(64, source.frequency / 20);
            int silent = 0;
            for (int start = 0; start + window <= total; start += window)
            {
                if (WindowRms(data, start, window, channels) >= 0.01f)
                    break;

                silent = start + window;
            }

            int keepSilence = source.frequency / 20;                   // 50 ms of lead-in
            int cut = Mathf.Max(0, silent - keepSilence);

            // Generated music also carries a silent run-out; keeping it would reopen a dead
            // gap of the same size at the loop seam, so the tail is trimmed the same way.
            int tailSilent = total;
            for (int end = total; end - window >= 0; end -= window)
            {
                if (WindowRms(data, end - window, window, channels) >= 0.01f)
                    break;

                tailSilent = end - window;
            }

            int tailCut = Mathf.Max(0, total - (tailSilent + keepSilence)); // 50 ms of run-out
            int length = total - cut - tailCut;
            if (length <= source.frequency / 2)
                return source;

            var loop = new float[length * channels];
            Array.Copy(data, cut * channels, loop, 0, loop.Length);

            int fade = Mathf.Min(source.frequency / 10, length / 4);   // 100 ms
            for (int i = 0; i < fade; i++)
            {
                float w = (i + 1) / (float)fade;                       // 0 at the last sample
                int head = (length - 1 - i) * channels;
                for (int c = 0; c < channels; c++)
                    loop[head + c] *= w;
            }

            if (cut == 0 && tailCut == 0 && fade == 0)
                return source;

            var clip = AudioClip.Create(source.name + "_loop", length, channels, source.frequency, false);
            clip.SetData(loop, 0);
            return clip;
        }

        /// <summary>RMS of one window of interleaved samples.</summary>
        private static float WindowRms(float[] data, int start, int window, int channels)
        {
            double sum = 0.0;
            int count = 0;
            for (int i = start; i < start + window; i++)
            {
                for (int c = 0; c < channels; c++)
                {
                    float v = data[i * channels + c];
                    sum += (double)v * v;
                    count++;
                }
            }

            return (float)Math.Sqrt(sum / System.Math.Max(1, count));
        }

        private void ApplyVolumes()
        {
            float volume = _volume;
            _sfxSource.volume = Mathf.Clamp01(volume);
            _bgmSource.volume = Mathf.Clamp01(volume * BgmGain);

            if (volume <= 0.001f)
                _bgmSource.Pause();
            else if (_bgmSource.time > 0f)
                _bgmSource.UnPause();          // isPlaying is false while paused; Play() would restart the track
            else if (!_bgmSource.isPlaying && _bgmSource.clip != null)
                _bgmSource.Play();
        }
    }
}
