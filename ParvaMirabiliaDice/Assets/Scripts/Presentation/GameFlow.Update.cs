using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ParvaMirabilia.Presentation
{
    public sealed partial class GameFlow
    {
        private RectTransform _privateUpdateOverlay;
        private Text _privateUpdateStatus;
        private Text _privateTitleUpdateStatus;
        private Button _privateInstallButton;
        private bool _privateUpdateBusy;
        private PrivateReleaseUpdater.Asset _privateUpdateAsset;
        private Version _privateAvailableVersion;

        private void BuildPrivateUpdateUi()
        {
            SDButton("PrivateUpdate", _sdTitle, new Vector2(-430, -291), new Vector2(132, 38),
                "更新確認", 15, OpenPrivateUpdate);
            _privateTitleUpdateStatus = SDText("PrivateUpdateStatus", _sdTitle,
                "", 12, new Vector2(-427, -253), new Vector2(290, 28), SDMuted);

            _privateUpdateOverlay = UiFactory.CreateStretch("PrivateUpdateDialog", _canvasRoot.transform);
            UiFactory.CreateStretchPanel("Dim", _privateUpdateOverlay, new Color(0, 0, 0, .82f))
                .raycastTarget = true;
            var panel = UiFactory.CreatePanel("Panel", _privateUpdateOverlay,
                new Vector2(690, 240), Vector2.zero, DiceYahzeeVisuals.Panel);
            panel.raycastTarget = true;
            SDText("Heading", panel.transform, "ゲームの更新", 28,
                new Vector2(0, 78), new Vector2(600, 44), SDPositive);
            var explanation = SDText("Explanation", panel.transform,
                "公開 GitHub Release の最新版を確認します。\n" +
                "更新があれば ZIP を検証し、ゲーム終了後に差し替えます。", 16,
                new Vector2(0, 22), new Vector2(620, 62), SDRailInk);
            explanation.horizontalOverflow = HorizontalWrapMode.Wrap;
            SDButton("CheckUpdate", panel.transform, new Vector2(0, -33), new Vector2(160, 38),
                "更新を確認", 16, () => StartCoroutine(CheckPrivateRelease()));
            _privateUpdateStatus = SDText("Status", panel.transform, "", 15,
                new Vector2(0, -76), new Vector2(610, 45), SDRailInk);
            _privateUpdateStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            _privateInstallButton = SDButton("InstallUpdate", panel.transform,
                new Vector2(-88, -111), new Vector2(190, 40), "更新して再起動", 16,
                () => StartCoroutine(DownloadPrivateRelease()), true);
            _privateInstallButton.interactable = false;
            SDButton("CloseUpdate", panel.transform, new Vector2(130, -111),
                new Vector2(150, 40), "閉じる", 16, ClosePrivateUpdate);
            _privateUpdateOverlay.gameObject.SetActive(false);
            SetPrivateUpdateStatus("更新確認は未実行");
        }

        private void OpenPrivateUpdate()
        {
            _privateUpdateOverlay.gameObject.SetActive(true);
            _privateUpdateOverlay.SetAsLastSibling();
        }

        private void ClosePrivateUpdate()
        {
            _privateUpdateOverlay.gameObject.SetActive(false);
        }

        private void SetPrivateUpdateStatus(string message)
        {
            if (_privateUpdateStatus != null) _privateUpdateStatus.text = message;
            if (_privateTitleUpdateStatus != null) _privateTitleUpdateStatus.text = message;
        }

        private void StartPrivateUpdateCheck()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            StartCoroutine(CheckPrivateRelease());
#endif
        }

        private IEnumerator CheckPrivateRelease()
        {
            if (_privateUpdateBusy) yield break;
            _privateUpdateBusy = true;
            _privateUpdateAsset = null;
            _privateInstallButton.interactable = false;
            SetPrivateUpdateStatus("GitHub の最新リリースを確認中…");
            string endpoint = "https://api.github.com/repos/" + PrivateReleaseUpdater.Owner + "/" +
                              PrivateReleaseUpdater.Repository + "/releases/latest";
            using (var request = UnityWebRequest.Get(endpoint))
            {
                ConfigureGitHubRequest(request, "application/vnd.github+json");
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    SetPrivateUpdateStatus(request.responseCode == 404
                            ? "公開リリースが見つかりません"
                            : "更新確認に失敗しました (HTTP " + request.responseCode + ")");
                }
                else
                {
                    PrivateReleaseUpdater.Release release = null;
                    try { release = JsonUtility.FromJson<PrivateReleaseUpdater.Release>(request.downloadHandler.text); }
                    catch (ArgumentException) { }
                    if (release == null || !PrivateReleaseUpdater.TryVersion(release.tag_name, out var latest) ||
                        !PrivateReleaseUpdater.TryVersion(Application.version, out var installed))
                    {
                        SetPrivateUpdateStatus("リリースのバージョン形式を読めません");
                    }
                    else if (latest.CompareTo(installed) <= 0)
                    {
                        SetPrivateUpdateStatus("最新版です (Ver." + Application.version + ")");
                    }
                    else
                    {
                        PrivateReleaseUpdater.Asset asset = null;
                        if (release.assets != null)
                            foreach (var candidate in release.assets)
                                if (candidate.name == PrivateReleaseUpdater.ArchiveName) { asset = candidate; break; }
                        if (asset == null || !PrivateReleaseUpdater.IsReleaseAssetUrl(asset.browser_download_url, release.tag_name) ||
                            !PrivateReleaseUpdater.TrySha256(asset.digest, out _))
                        {
                            SetPrivateUpdateStatus("更新 ZIP または GitHub SHA-256 がありません");
                        }
                        else
                        {
                            _privateAvailableVersion = latest;
                            _privateUpdateAsset = asset;
                            _privateInstallButton.interactable = true;
                            SetPrivateUpdateStatus("Ver." + latest + " が利用できます");
                            if (_sdTitle != null && _sdTitle.gameObject.activeInHierarchy) OpenPrivateUpdate();
                        }
                    }
                }
            }
            _privateUpdateBusy = false;
        }

        private static void ConfigureGitHubRequest(UnityWebRequest request, string accept)
        {
            request.redirectLimit = 0;
            request.timeout = 30;
            request.SetRequestHeader("Accept", accept);
            request.SetRequestHeader("User-Agent", "DiceYahzee-Updater");
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
        }

        private IEnumerator DownloadPrivateRelease()
        {
#if !UNITY_STANDALONE_WIN || UNITY_EDITOR
            SetPrivateUpdateStatus("自動更新は Windows 配布版で利用できます");
            yield break;
#else
            if (_privateUpdateBusy || _privateUpdateAsset == null ||
                !PrivateReleaseUpdater.TrySha256(_privateUpdateAsset.digest, out string expected)) yield break;
            _privateUpdateBusy = true;
            _privateInstallButton.interactable = false;
            string staging = Path.Combine(Application.persistentDataPath, "release-update-" + _privateAvailableVersion);
            Directory.CreateDirectory(staging);
            string archive = Path.Combine(staging, "update.zip");
            if (File.Exists(archive)) File.Delete(archive);
            SetPrivateUpdateStatus("更新 ZIP をダウンロード中…");
            string redirected = null;
            using (var request = UnityWebRequest.Get(_privateUpdateAsset.browser_download_url))
            {
                request.downloadHandler = new DownloadHandlerFile(archive);
                ConfigureGitHubRequest(request, "application/octet-stream");
                yield return request.SendWebRequest();
                if (request.responseCode == 302 || request.responseCode == 301 || request.responseCode == 307)
                    redirected = request.GetResponseHeader("Location");
                else if (request.result != UnityWebRequest.Result.Success)
                    SetPrivateUpdateStatus("更新 ZIP を取得できません (HTTP " + request.responseCode + ")");
            }
            if (redirected != null)
            {
                if (!PrivateReleaseUpdater.IsGitHubDownloadRedirect(redirected))
                    SetPrivateUpdateStatus("GitHub 以外への転送を拒否しました");
                else
                {
                    if (File.Exists(archive)) File.Delete(archive);
                    using (var request = UnityWebRequest.Get(redirected))
                    {
                        request.downloadHandler = new DownloadHandlerFile(archive);
                        request.redirectLimit = 0;
                        request.timeout = 180;
                        request.SetRequestHeader("User-Agent", "DiceYahzee-Updater");
                        yield return request.SendWebRequest();
                        if (request.result != UnityWebRequest.Result.Success)
                            SetPrivateUpdateStatus("ZIP 転送に失敗しました (HTTP " + request.responseCode + ")");
                    }
                }
            }
            bool valid = File.Exists(archive) &&
                string.Equals(PrivateReleaseUpdater.Sha256File(archive), expected, StringComparison.OrdinalIgnoreCase);
            if (!valid)
            {
                if (File.Exists(archive)) File.Delete(archive);
                SetPrivateUpdateStatus("SHA-256 検証に失敗しました。更新を中止します");
                _privateUpdateBusy = false;
                _privateInstallButton.interactable = true;
                yield break;
            }
            SetPrivateUpdateStatus("検証完了。終了後に差し替えて再起動します");
            try
            {
                string install = Directory.GetParent(Application.dataPath).FullName;
                string packagedScript = Path.Combine(install, "DiceYahzee-Update.ps1");
                string script = Path.Combine(staging, "DiceYahzee-Update.ps1");
                File.Copy(packagedScript, script, true);
                string log = Path.Combine(staging, "update.log");
                int pid = Process.GetCurrentProcess().Id;
                var process = new ProcessStartInfo("powershell.exe")
                {
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + QuoteUpdateArgument(script) +
                        " -Archive " + QuoteUpdateArgument(archive) + " -ExpectedSha " + QuoteUpdateArgument(expected) +
                        " -InstallDirectory " + QuoteUpdateArgument(install) + " -WaitPid " + pid +
                        " -LogPath " + QuoteUpdateArgument(log),
                    UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = staging
                };
                Process.Start(process);
                Application.Quit();
            }
            catch (Exception)
            {
                SetPrivateUpdateStatus("更新処理を起動できません。インストール先の書込み権限を確認してください");
                _privateUpdateBusy = false;
                _privateInstallButton.interactable = true;
            }
#endif
        }

        private static string QuoteUpdateArgument(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
