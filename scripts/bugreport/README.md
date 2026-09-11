# バグ報告の運搬（MacBook側）

1. `~/.config/moorestech/bugreport-shipper.env` を作る:
   ```
   MACMINI_SSH=sakastudio@<Mac miniのTailscaleホスト名>
   MACMINI_INBOX=hermes-agent/data/repos/moorestech_logs/harness/bug-report/inbox
   ```
2. plist を配置する。**`com.moorestech.bugreport-shipper.plist` の `/Users/katsumi/...` はプレースホルダなので、コピー後に自分のユーザー名・repo の実パスへ必ず書き換える**（`ProgramArguments` のスクリプトパスと `StandardOutPath`/`StandardErrorPath` の3箇所。書き換えずに読み込むとパスが存在せず即失敗する）:
   ```
   mkdir -p ~/Library/Logs/moorestech
   cp scripts/bugreport/launchd/com.moorestech.bugreport-shipper.plist ~/Library/LaunchAgents/
   # ~/Library/LaunchAgents/com.moorestech.bugreport-shipper.plist を編集して /Users/katsumi/... を実パスへ置換
   launchctl load ~/Library/LaunchAgents/com.moorestech.bugreport-shipper.plist
   ```
3. 手動実行: `bash scripts/bugreport/ship-outbox.sh`。ログは `~/Library/Logs/moorestech/bugreport-shipper.log`
4. Tailscale で届かないときは何もしない（裁定 2026-09-11）。箱は outbox に残り、次回接続時に送られる
