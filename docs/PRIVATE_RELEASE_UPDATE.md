# 招待ユーザー向け Windows 更新

## 配布者

1. `scripts/Build-Windows.ps1` で Windows ビルドを作成する。出力 ZIP には `DiceYahzee-Update.ps1` が含まれる。
2. private repository `sakri9616-coder/DiceYahzee` に `v<bundleVersion>`（例: `v0.2.0`）の GitHub Release を公開する。
3. ビルド ZIP を **`DiceYahzee-win64.zip`** というアセット名で添付する。GitHub API がアセットに返す `digest: sha256:...` が更新時の必須検証値となる。
4. 更新を受ける人をリポジトリに招待する。タグ名はゲームの `Application.version` より大きい三部の数値にする。ドラフトや prerelease は `releases/latest` の対象外。

## 利用者

1. 招待を受け入れ、Release の ZIP を一度ダウンロードして展開し、`DiceYahzee.exe` を起動する。ZIP の内容を同じフォルダーに保つ。
2. 自分の GitHub アカウントで fine-grained personal access token を作る。対象を `sakri9616-coder/DiceYahzee` のみに限定し、**Contents: Read-only** を付ける。組織側の追加承認が必要なら完了させる。
3. タイトル画面の「更新設定」でトークンを入力し「保存して確認」。以後は同じ Windows ユーザーで起動するたびに最新版を確認する。新しい Release があれば表示される「更新して再起動」を選ぶ。
4. 「認証を削除」でローカルの資格情報を消せる。トークンが失効したときも再設定する。

## 動作と復旧

- トークンは Windows 資格情報マネージャーにユーザー単位で保存され、GitHub API 以外には送らない。ログ、ZIP、設定ファイル、アップデーターのコマンドラインには含めない。GitHub の署名付きアセット URL への転送時にも認証ヘッダーを渡さない。
- アプリは ZIP の SHA-256 を API の `digest` と照合する。外部アップデーターは終了を待って再照合し、安全な相対パスのみ展開する。差し替え対象の既存ファイルをバックアップしてから置き換え、差し替え途中の失敗時は元に戻す。
- 更新ログと差し替え時のバックアップは、各ユーザーの Unity `Application.persistentDataPath` にある `private-update-<version>` 以下に保存される。失敗した場合は `update.log` を参照する。インストール先への書き込み権限が必要。
- private リポジトリを閲覧できない利用者は更新できない。初回 ZIP、トークン、権限のいずれかが欠けた場合はゲームをそのまま利用できる。
