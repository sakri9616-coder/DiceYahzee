using System;
using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    /// <summary>
    /// Quality-of-life housekeeping for the standalone player, kept apart from GameFlow so that
    /// no rule, timing or test-visible object changes:
    /// - frame pacing: without vsync the player otherwise renders uncapped, and because the game keeps
    ///   drawing out of focus (see GameFlow.KeepDrawingOutOfFocus) a background window would spin the
    ///   GPU at full speed. Focused play is capped to the display refresh; unfocused play to 30 fps.
    /// - F11 toggles borderless fullscreen / windowed (Alt+Enter keeps working as Unity provides it;
    ///   Unity itself remembers the chosen window mode between launches).
    /// - settings written with PlayerPrefs.Set* (motion, volume) are flushed to disk when the window
    ///   loses focus or the game quits, so a crash or forced close does not forget them.
    /// The Editor, batch mode and the diagnostic smoke/network launches are left untouched.
    /// </summary>
    internal sealed class AppSystem : MonoBehaviour
    {
        private const int BackgroundFrameRate = 30;
        private const int MinForegroundFrameRate = 60;
        private const int MaxForegroundFrameRate = 240;

        private int _foregroundVSync;
        private int _foregroundFrameRate = -1;
        private bool _pacingActive;
        private bool _focused = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Application.isEditor || Application.isBatchMode || IsDiagnosticLaunch()) return;
            if (FindFirstObjectByType<AppSystem>() != null) return;
            var host = new GameObject("ParvaMirabiliaAppSystem");
            DontDestroyOnLoad(host);
            host.AddComponent<AppSystem>();
        }

        private static bool IsDiagnosticLaunch()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                foreach (string arg in args)
                    if (arg == "-arcaneSmokeOutput" || arg == "-diceNetHost" || arg == "-diceNetJoin") return true;
            }
            catch (Exception) { }
            return false;
        }

        private void Awake()
        {
            _foregroundVSync = QualitySettings.vSyncCount;
            if (_foregroundVSync == 0)
            {
                // Respect an explicit cap someone already set; otherwise match the display.
                if (Application.targetFrameRate > 0) _foregroundFrameRate = Application.targetFrameRate;
                else
                {
                    int refresh = Mathf.RoundToInt((float)UnityEngine.Screen.currentResolution.refreshRateRatio.value);
                    _foregroundFrameRate = Mathf.Clamp(refresh > 0 ? refresh : MinForegroundFrameRate,
                        MinForegroundFrameRate, MaxForegroundFrameRate);
                }
            }
            _pacingActive = true;
            ApplyPacing();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F11)) ToggleFullscreen();
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            ApplyPacing();
            if (!focused) FlushPrefs();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) FlushPrefs();
        }

        private void OnApplicationQuit() => FlushPrefs();

        private void ApplyPacing()
        {
            if (!_pacingActive) return;
            if (_focused)
            {
                QualitySettings.vSyncCount = _foregroundVSync;
                Application.targetFrameRate = _foregroundVSync > 0 ? -1 : _foregroundFrameRate;
            }
            else
            {
                // targetFrameRate is ignored while vsync is on, so vsync is released in the background.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = BackgroundFrameRate;
            }
        }

        private static void ToggleFullscreen()
        {
            if (UnityEngine.Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                Resolution native = UnityEngine.Screen.currentResolution;
                UnityEngine.Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
            }
            else
            {
                UnityEngine.Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            }
        }

        private static void FlushPrefs()
        {
            try { PlayerPrefs.Save(); }
            catch (Exception exception) { Debug.LogWarning("Settings could not be saved: " + exception.Message); }
        }
    }
}
