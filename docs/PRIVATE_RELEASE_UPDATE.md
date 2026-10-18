# Windows 版の GitHub 自動更新

## 配布者

1. `scripts/Build-Windows.ps1` で Windows ビルドを作成する。ZIP には `DiceYahzee-Update.ps1` が含まれる。
2. 公開リポジトリ `sakri9616-coder/DiceYahzee` に `v<bundleVersion>`（例: `v0.2.1`）の GitHub Release を公開する。
3. ビルド ZIP を **`DiceYahzee-win64.zip`** という名前で添付する。GitHub API の `digest: sha256:...` を更新時の検証値とする。タグ名は現在の `Application.version` より大きい三部の数値にする。ドラフトと prerelease は `releases/latest` の対象外。

## 利用者

1. [最新 Release](https://github.com/sakri9616-coder/DiceYahzee/releases/latest) の `DiceYahzee-win64.zip` を一度ダウンロードして展開し、`DiceYahzee.exe` を起動する。ZIP の内容を同じフォルダーに保つ。GitHub アカウントやトークンは不要。
2. 以後は Windows 版の起動時に自動で最新版を確認する。新しい Release があればタイトル画面の「更新確認」から「更新して再起動」を選ぶ。手動確認も「更新確認」からできる。
3. 旧 `v0.2.0` の配布版には非公開リポジトリ用の認証処理が残っている。`v0.2.1` を一度手動で入れ直すと、以後は認証なしで更新できる。

## 動作と復旧

- アプリは公開 GitHub API の `releases/latest` を認証なしで読み、ZIP の SHA-256 を API の `digest` と照合する。外部アップデーターは終了後に再照合し、安全な相対パスのみ展開する。差し替え対象の既存ファイルをバックアップし、差し替え途中の失敗時は元に戻す。
- 更新ログと差し替え時のバックアップは、各ユーザーの Unity `Application.persistentDataPath` にある `release-update-<version>` 以下に保存する。失敗した場合は `update.log` を参照する。インストール先への書き込み権限が必要。
- GitHub に接続できない場合も現在のゲームはそのまま利用できる。
