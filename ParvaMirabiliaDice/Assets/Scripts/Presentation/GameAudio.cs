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
        private const float BgmGain = 0.189f; // 40% below the previous 0.315 maximum.

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

        /// <summary>
        /// Clips that are played through the voice pool with a small random pitch and level, so a
        /// run of identical die clicks or hits stops sounding like a loop. Values are the pitch and
        /// level spread either way. The result stings are left out: they must sound the same.
        /// </summary>
        private static readonly Dictionary<string, Vector2> Variation = new Dictionary<string, Vector2>
        {
            { "dice_roll", new Vector2(.06f, .10f) },
            { "die_keep", new Vector2(.08f, .12f) },
            { "ui_click", new Vector2(.05f, .08f) },
            { "ui_hover", new Vector2(.07f, .15f) },
            { "tick", new Vector2(.03f, .12f) },
            { "hit", new Vector2(.05f, .06f) },
            { "guard", new Vector2(.05f, .06f) },
            { "hurt", new Vector2(.05f, .06f) },
            { "commit", new Vector2(.025f, .05f) },
            { "formation", new Vector2(.02f, .05f) },
            { "stamp", new Vector2(.02f, 0f) }
        };

        /// <summary>How deep the music dips under each big moment, and for how long it holds.</summary>
        private static readonly Dictionary<string, Vector2> DuckOn = new Dictionary<string, Vector2>
        {
            { "hit", new Vector2(.6f, .35f) },
            { "hurt", new Vector2(.6f, .35f) },
            { "stamp", new Vector2(.3f, 1.3f) },
            { "win", new Vector2(.35f, 2.6f) },
            { "lose", new Vector2(.35f, 2.6f) },
            { "fanfare", new Vector2(.35f, 2.2f) },
            { "toll", new Vector2(.35f, 2.2f) }
        };

        private const int VoiceCount = 6;
        private readonly AudioSource[] _voices = new AudioSource[VoiceCount];
        private readonly float[] _voiceEnds = new float[VoiceCount];
        private int _nextVoice;
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private float _duck = 1f, _duckDepth = 1f, _duckUntil;
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

            // Added after the BGM and main SFX sources so their component order never changes.
            for (int i = 0; i < VoiceCount; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }

            for (int i = 0; i < ClipNames.Length; i++)
            {
                var clip = Resources.Load<AudioClip>("Audio/" + ClipNames[i]);
                if (clip == null)
                    continue;

                float gain;
                _clips[ClipNames[i]] = PrepareSfx(ClipNames[i], clip, out gain);
                _gains[ClipNames[i]] = gain;
            }

            foreach (ProceduralSfx.Entry entry in ProceduralSfx.Build())
            {
                if (entry.Clip == null || _clips.ContainsKey(entry.Name)) continue;
                _clips[entry.Name] = entry.Clip;
                _gains[entry.Name] = entry.Gain;
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

            Vector2 duck;
            if (DuckOn.TryGetValue(clipName, out duck))
                Duck(duck.x, duck.y);

            // volumeScale is a multiplier, so gains above 1 are allowed; PrepareSfx already
            // capped the gain so the result stays below the clipping point.
            Vector2 spread;
            if (!Variation.TryGetValue(clipName, out spread))
            {
                Instance._sfxSource.PlayOneShot(clip, Mathf.Max(0f, gain));
                return;
            }

            float pitch = 1f + UnityEngine.Random.Range(-spread.x, spread.x);
            float level = 1f + UnityEngine.Random.Range(-spread.y, spread.y);
            Instance.PlayVoice(clip, Mathf.Max(0f, gain * level), pitch);
        }

        /// <summary>Plays a clip only if the same clip has not started within the interval.</summary>
        public static void PlayThrottled(string clipName, float minInterval)
        {
            if (Instance == null)
                return;
            float now = Time.unscaledTime;
            float last;
            if (Instance._lastPlayed.TryGetValue(clipName, out last) && now - last < minInterval)
                return;
            Instance._lastPlayed[clipName] = now;
            Play(clipName);
        }

        /// <summary>The pointer arriving on a live button: a quiet tap, never stacked.</summary>
        public static void PlayHover()
        {
            PlayThrottled("ui_hover", .07f);
        }

        /// <summary>One tick of a counting figure; the pitch climbs with the count's progress.</summary>
        public static void PlayTick(float progress)
        {
            if (Instance == null)
                return;
            float now = Time.unscaledTime;
            float last;
            if (Instance._lastPlayed.TryGetValue("tick", out last) && now - last < .05f)
                return;
            AudioClip clip;
            if (!Instance._clips.TryGetValue("tick", out clip) || clip == null)
                return;
            Instance._lastPlayed["tick"] = now;
            float gain;
            if (!Instance._gains.TryGetValue("tick", out gain))
                gain = 1f;
            float pitch = Mathf.Lerp(.85f, 1.45f, Mathf.Clamp01(progress)) + UnityEngine.Random.Range(-.02f, .02f);
            Instance.PlayVoice(clip, gain, pitch);
        }

        /// <summary>
        /// Lowers the music to <paramref name="depth"/> of its level for <paramref name="hold"/>
        /// seconds, then lets it swell back. Overlapping calls keep the deeper dip and the later end.
        /// </summary>
        public static void Duck(float depth, float hold)
        {
            if (Instance == null)
                return;
            float now = Time.unscaledTime;
            if (now >= Instance._duckUntil)
                Instance._duckDepth = 1f;
            Instance._duckDepth = Mathf.Min(Instance._duckDepth, Mathf.Clamp01(depth));
            Instance._duckUntil = Mathf.Max(Instance._duckUntil, now + Mathf.Max(0f, hold));
        }

        private void PlayVoice(AudioClip clip, float volumeScale, float pitch)
        {
            // Pitch is per source, so each varied sound gets a voice of its own; six voices cover
            // the busiest overlap (roll, keep, hit, stamp) without cutting a tail short.
            // A free voice is preferred; when all are busy the one closest to finishing is reused.
            float now = Time.unscaledTime;
            int pick = _nextVoice;
            for (int k = 0; k < VoiceCount; k++)
            {
                int i = (_nextVoice + k) % VoiceCount;
                if (_voiceEnds[i] <= now) { pick = i; break; }
                if (_voiceEnds[i] < _voiceEnds[pick]) pick = i;
            }
            _nextVoice = (pick + 1) % VoiceCount;
            AudioSource voice = _voices[pick];
            if (voice == null)
            {
                _sfxSource.PlayOneShot(clip, volumeScale);
                return;
            }
            pitch = Mathf.Clamp(pitch, .5f, 2f);
            voice.Stop();
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volumeScale);
            _voiceEnds[pick] = now + clip.length / pitch;
        }

        private void Update()
        {
            float target = Time.unscaledTime < _duckUntil ? _duckDepth : 1f;
            if (Mathf.Approximately(_duck, target))
                return;
            // A fast dip and a slow swell back, like a sidechain on the music bus.
            float rate = target < _duck ? 14f : 1.6f;
            _duck = Mathf.MoveTowards(_duck, target, rate * Mathf.Min(Time.unscaledDeltaTime, .1f));
            if (_bgmSource != null)
                _bgmSource.volume = Mathf.Clamp01(_volume * BgmGain * _duck);
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
            for (int i = 0; i < _voices.Length; i++)
                if (_voices[i] != null) _voices[i].volume = Mathf.Clamp01(volume);
            _bgmSource.volume = Mathf.Clamp01(volume * BgmGain * _duck);

            if (volume <= 0.001f)
                _bgmSource.Pause();
            else if (_bgmSource.time > 0f)
                _bgmSource.UnPause();          // isPlaying is false while paused; Play() would restart the track
            else if (!_bgmSource.isPlaying && _bgmSource.clip != null)
                _bgmSource.Play();
        }
    }
}
