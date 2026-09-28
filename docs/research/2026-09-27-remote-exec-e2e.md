# 遠隔実行 検証機での実機確認（Task 8, 2026-09-28）

対象: `feat/remote-exec` の e84fdedbc を Windows x64 Release（`ReleaseLocalBuildCli.CreateRequest` と同じ Release・strict・ゲームデータ同梱）で焼いたビルド（初回。当時の起動フラグは `-remote-exec`。以下の記述は改名後の `--remoteExec` に読み替えて統一している）。検証機の作業フォルダ `C:\moorestech-remote-exec\build` に置いた（Steam のインストール先・Steam 配布は触っていない）。

## 実施方法と plan からの差分

- 検証機は自動ログイン後すぐにロック画面になる設定で、対話セッションへクリックを送れなかった。ロック画面を迂回する操作はしていない。
- そのため「ゲーム開始後」の状態は、入力を要しない `--playtestSmoke`（phase1＝ワールドに入って保存し終了、phase2＝ワールドを読んでバグ報告を送信し終了）に `--remoteExec` を足して作り、その間に検証機内または Mac から要求を送った。
- Step 4 の「ポーズメニューからのバグ報告」は、smoke phase2 の自動送信（同じ `BugReportSubmitter` 経路）で代えた。
- Steam 初期化（報告送信に必要）のため、作業フォルダに `steam_appid.txt`（1958160）を置いた。

## 結果

### Step 2: オプションなし（R13-3, R1）: 合格
- `POST http://127.0.0.1:25050/api/remote-exec` → **404**
- `%APPDATA%\.moorestech\RemoteExec\access.json` は作られない（起動前に無し・起動後も無し）
- Player.log: `[RemoteExec] 起動オプションが無いため遠隔実行は無効です`

### Step 3: `--remoteExec` 付き（R13-1, R13-2, R4, R6, R12）: 合格
検証機内の PowerShell から:
```
[c1.cs/client] status=403   ← トークン誤り。Player.log: [RemoteExec] 要求を拒否しました: トークン不一致
[c1.cs/client] status=200 body={"ok":true,"result":"2",...}
[tid.cs/client] status=200 body={"ok":true,"result":"1",...}
[tid.cs/server] status=200 body={"ok":false,...,"exception":"内蔵サーバーが起動していないため、サーバー側では実行できません"}  ← ワールドのロード前（設計どおりの理由付き失敗）
[tid.cs/server] status=200 body={"ok":true,"result":"24",...}  ← サーバー起動後。client の 1 と異なるスレッド
[vsync.cs/client] status=200 body={"ok":true,"result":"0",...}
[harmony.cs/client] status=200 body={"ok":true,"result":"20,777777,20",...}
```
Harmony は `Core.Update.GameUpdater.SecondsToTicks` に Postfix を当て、適用前 20・適用中 777777・`UnpatchAll` 後 20。プレイヤーには Harmony がプラグインとして入らず、`moorestech_Data/RemoteExec/0Harmony.dll` を有効起動時に実行時読み込みする方式が x64 でも動くことを確認した。

Mac から送る側 CLI（`scripts/playtest/remote-exec.sh --windows`）:
```
== client vsync      → {"ok":true,"result":"0",...} exit=0
== server tid        → {"ok":true,"result":"22",...} exit=0
== harmony           → {"ok":true,"result":"20,777777,20",...} exit=0
return 1 + 1;        → stdout: {"ok":true,"result":"2",...}（json.load 可）
```
stdout は JSON だけ。stderr に PowerShell の進捗レコード（`#< CLIXML` …）が混ざる（Minor として最終レビューへ）。

### Step 4: 印付き報告の一巡（R10, R11）: 合格
- phase2 の報告 `76561198217468291/20260927_190323_56948405` を受け口から取得し、直後に ACK した（取り込みに検証用の報告を混ぜないため。テスター報告の母数は汚していない）。
- manifest: `schemaVersion 4`、`remoteExec: {"enabled": true, "ledgerFiles": ["remote-exec/ledger-2532.jsonl"]}`、Missing に遠隔実行関連なし。
- 箱の `remote-exec/ledger-2532.jsonl` に、報告前に送った実行の開始行と結果行:
  ```
  {"event":"start","sequence":1,...,"target":"client","code":"return \"ledger-entry-before-report\";\n"}
  {"event":"result","sequence":1,...,"ok":true}
  ```
- 取得した箱を一時 LOGS に置いて `enqueue-autofix.sh` → **exit 3**（`遠隔実行が有効だったセッションの箱は自動修正ランの対象外。投入するなら --force`）。
- 日次集計からの除外は scripts/playtest/tests の単体テストで確認（実データでの digest 実行はしていない）。

### Step 5: 警告語 grep: 合格
各起動の Player.log で `[RemoteExec]`・`[WebUiHost]` 行を `拒否|refus|Exception|Error|書けません|開けません|failed|失敗` で grep した結果、意図したトークン不一致の拒否1件（Step 3）以外は0件。合否は期待する肯定行ではなく、この警告語ゼロで判定した。

同じログにある次の2件は、今回の変更と無関係の既存事象（オプションなしの起動でも出る）:
- `UnityDebugSheet` の `NullReferenceException`
- 終了時の `UserPacketHandler` の `SocketException`

## 最終レビュー反映後の再実施（a67d71920, 2026-09-28）

最終レビューの反映が有効化の判定（起動フラグの `--remoteExec` への改名）と結果の表し方（`outcome`）に触れたため、a67d71920 を同じ条件で焼き直し、同じ手順を再実施した。

- Step 2（オプションなし）: `POST /api/remote-exec` → **404**。access.json は作られない。Player.log: `[RemoteExec] 起動オプションが無いため遠隔実行は無効です`・`[RemoteExec] 起動オプションが無いため要求を404で拒否しました`
- Step 3（`--remoteExec` 付き）: トークン誤り **403**（`[RemoteExec] 要求を拒否しました: トークン不一致`）。client `return 1 + 1;` → `{"outcome":"Succeeded","result":"2",...}`。サーバー起動前の server 指定 → `{"outcome":"Rejected",...,"exception":"内蔵サーバーが起動していないため、サーバー側では実行できません"}`、起動後 → `Succeeded`（client と別スレッド）
- Mac からの CLI（`remote-exec.sh --windows`）: client vsync `Succeeded` exit 0、Harmony `20,777777,20` exit 0、ロード前 server `Rejected` exit 2、コンパイル失敗 `CompileFailed` exit 2。stdout は JSON だけで **stderr は 0 バイト**（初回にあった PowerShell 進捗レコードの混入は解消）
- Step 4（印付き報告の一巡）: phase2 の報告 `76561198217468291/20260928_094755_85c4df84` を取得し直後に ACK。manifest は `schemaVersion 4`、`remoteExec: {"ledgerFiles": ["remote-exec/ledger-6040-session_639261856209102150.jsonl"]}`（台帳名にセッション名）、Missing に遠隔実行関連なし。台帳に開始行と結果行（`"outcome":"Succeeded"`）
  - `enqueue-autofix.sh` → **exit 6**（遠隔実行の拒否。kind 拒否と終了コードを分離）。一時 LOGS で `--force` → 投入され `AUTOFIX_FORCED` が残る
  - 取得した箱（台帳1件つき）を一時 LOGS に置き `digest_collect.load_reports` を実行 → `remoteExec: 1`・`invalidManifest: 0`・集計対象 0 件（実データの箱で除外を確認。日次集計の本文表示までは見ていない）
- Step 5（警告語 grep）: 全起動の Player.log で `[RemoteExec]`・`[WebUiHost]`・`[ServerThreadActionQueue]` 行に対し同じ語で grep し、意図した 404 拒否とトークン不一致の2件以外は0件

## 未検証・残差
- 対話操作（ポーズメニューからの報告送信・タイトルからの手動開始）での確認はしていない（検証機のロック画面のため）。
- 実データでの日次集計（digest）の本文表示（読み込み段の除外は実データの箱で確認済み）。
