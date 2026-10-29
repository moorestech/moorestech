# 遠隔実行の専用待受: Windows 配布ビルドでの実機確認

対象コミット: `57bd4b47095f3f1f89e0b667fc76f00e465b7158`。R7・R8 の3ケースはすべて合格。`HttpListener` が動いたため Task 5 の Kestrel 代替は不要。

## 実施方法と plan からの差分

- Unity 6000.3.8f1、Windows x64、Mono（`scriptingBackend.Standalone: 0`）、Release、`managedStrippingLevel` はプロジェクト既定のまま。`SteamPlaytestBuildCli.WindowsSteamPlaytestBuild` を batchmode で実行し、終了コード0・`Build Result :Succeeded`・`outcome:Succeeded`。
- マスターデータの入力は `.moorestech-external-revisions.json` が指定した `0d129c8b0f7129e0d59647384a226db08310e1cd` の pin worktree。最終 `build-info.json.masterDataCommit` も同じで、`masterDirty:false`。
- ビルド前は作業treeがcleanだった。最初の strict 書込では `dirty:False`、最終成果物は `OnPreprocessBuild` の再書込で **`dirty:true`**。ビルド後に観測した差分は、自動同期による master ピンの `630fdce08eccee47feaa15721ca607f407000ab9` への変更と、自動生成された StreamingAssets の build-info。コード変更はない。ピンを元へ戻し、生成ファイルを RUN へ退避した。最終値をcleanとは扱わない。
- 初回Windows向けのAddressables/shader作成を含め約89分。転送用tarの複製は容量不足で失敗し、自分の不完全tarのみ削除。完成した6.2GBを直接scpした。Windows側の `Client.RemoteExec.dll`・`Client.Starter.dll` のSHA256がMac生成物と一致した。
- 検証機の専用rootは `C:/remote-exec-listener-20261026-012214`。Steam配布・Steamクライアントの操作・Discord投稿はせず、専用build内の `steam_appid.txt` に `1958160` を置いた。
- 外部uploadを起動しないため、planのケース2/3のphase2を **全ケースphase1** に置き換えた。ケース2/3では本番remote snippetで `StandalonePlaytestSmokeRunner.RunAsync` の呼出しを一時保留し、要求を送り切ってから1秒後に `Application.Quit(0)` を呼ぶsnippetで正常終了した。製品コードは変えていない。出所: agent 既定（ユーザー未裁定）。
- 各ケースは既存の `MOORESTECH_DEFAULT_WORLD_DIRECTORY` で専用root内のworldへ隔離した。他のmoorestechプロセスがあれば起動を拒否するguardを使い、名前によるkillや既存作業フォルダの削除はしていない。
- Tailscale接続が途切れた際、User32 Event1074にExplorer経由の電源OFFを3回観測した。原因は未同定。WoL反復を止め、MACが一致するLANアドレスへ既存のSSH鍵・host key照合を保って接続した。CLIのコード変更はなく、接続先と `SSH_BIN` だけ環境指定した。

ビルド入口（持込・検証用の一意なRUNを作成してから実行）:

```bash
MOORESTECH_BUILD_OUTPUT="$RUN/build-windows" \
MOORESTECH_STEAM_BUILD_LABEL=remote-exec-listener-20261026-012214 \
MOORESTECH_BUILD_BRANCH=feature/uitk-s0-remote-exec \
MOORESTECH_MASTER_DATA_ROOT="$HOME/hermes-agent/data/worktrees/moorestech_master/pin-0d129c8b" \
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics \
-projectPath "$W/moorestech_client" \
-executeMethod Client.Editor.Build.SteamPlaytestBuildCli.WindowsSteamPlaytestBuild \
-logFile "$RUN/unity-build-windows.log"
```

原ログ・JSON・終了コード・検証snippetの保存先:
`/Users/sakastudio/hermes-agent/data/services/playtest/runs/remote-exec-listener-20261026-012214`

主な証拠は `evidence/r1/Player.log`・`evidence/r2/Player.log`・`evidence/r3/Player.log`、`r1-probe.log`・`r2-probe.log`・`r3-probe.log`、`r2-*.json/.exit/.stderr`・`r3-*.json/.exit/.stderr`、`r2-expect.log`、`evidence/validation.txt`。access tokenは報告・probe・HTTP traceでマスクし、access.json自体は回収していない。

## 結果

### ケース1: `--remoteExec` なし（R8-2）— 合格

PID23236のListenは `127.0.0.1:25050` のみで、25100〜25119はない。`access.json=none`。Kestrelの `POST /api/remote-exec` は **404**。probe終了コード0。

```text
[RemoteExec] 起動オプションが無いため遠隔実行は無効です
[WebUiHost] Kestrel started at http://127.0.0.1:25050
kestrel.remote-exec.http=404
```

`[RemoteExec] 待受を開きました` はない。phase1の `result.json` は `success:true`、tutorial-startedとsaveが成功し、reportBundleDirectory/reportSteamIdは空。自然終了後はpid無し・access.json無し。

### ケース2: `--remoteExec` あり、CLI・Harmony（R7・R8-1）— 合格

PID2536、access.jsonの `port=25100/processId=2536`、Listenは `127.0.0.1:25100` と `127.0.0.1:25050`。

| 既存CLIから送ったコード | JSON outcome | result | exit / stderr |
|---|---|---|---|
| `return 1 + 1;` | Succeeded | `2` | 0 / 0 bytes |
| Harmony Postfix→解除 | Succeeded | `20,777777,20` | 0 / 0 bytes |
| clientのManagedThreadId | Succeeded | `1` | 0 / 0 bytes |
| serverのManagedThreadId | Succeeded | `22` | 0 / 0 bytes |

server要求は既存キューの `HasDrainedThisLifetime=true` をclient要求で観測してから送信した。serverの応答ログにtick1146での受付・復帰があり、clientと異なるスレッドで動いた。

```json
{"outcome":"Succeeded","result":"20,777777,20","compileErrors":[],"rejectionReason":null,"exception":null,"logs":[]}
```

大きい本文はコメントで処理を増やさず2597 bytesにした。まず既存 `remote-exec.sh --windows` でHarmonyの成功を確認し、その後、受信境界への一時的な観測用Harmony prefixで、同じCLIの実要求ヘッダを記録した。トークンは記録していない。

```text
[RemoteExecWireProbe] Expect=100-continue ContentLength=2597
```

同じ本文をcurlから明示的なExpectヘッダ付きでも送り、実HTTP traceと結果を確認した。サイズだけからの推測ではない。

```text
> Expect: 100-continue
> X-Remote-Exec-Token: [MASKED]
> Content-Length: 2597
< HTTP/1.1 100 Continue
< HTTP/1.1 200 OK
< Server: Mono-HTTPAPI/1.0
curl.exit=0
```

この追加要求も `Succeeded/result=20,777777,20`。正常終了後に待受の閉鎖・入口撤去を記録し、PID2536の退出を確認した。

### ケース3: WebUi成果物を外す（R8-3）— 合格

`moorestech_Data/StreamingAssets/WebUi` を `WebUi.disabled` に改名した同じ配布物を起動。PID13696のListenは **`127.0.0.1:25100` のみ**で、Kestrelの25050帯はない。access.jsonの `port=25100/processId=13696`。

```text
[RemoteExec] 待受を開きました http://127.0.0.1:25100
[WebUiHost] production artifact rejected: manifest not found: .../WebUi/dist/webui-manifest.json; Web UI remains unavailable
[WebUiHost] 起動が完了しなかったため、Web UIなしで続行します
[WebUiHost] Bind skipped: hub is null (WebUiHost not started)
```

`return 1 + 1;` は `Succeeded/result=2`、exit0、stderr0 bytes。正常終了後はPID無し・access.json無し。WebUiを元の名前へ戻し、`WebUi-restored=True` と `game-process-count=0` を確認した。

### 警告語grep

各ケースの `[RemoteExec]`・`[WebUiHost]` 行を `拒否|refus|Exception|Error|書けません|開けません|failed|失敗` で検索し、**r1=0、r2=0、r3=0**。ケース3の意図したartifact拒否・Web UIなし続行・Bind skippedも別途確認した。正常な要求結果とListenも併せて合否を判定した。これはログ全体が無エラーという意味ではない。

## 未検証・残差

R7・R8の指定範囲に未完了はない。手動UI操作・ゲームプレイの見た目はこの検証の対象外。

ログ全体では次を観測した。無効起動でも出ており、遠隔実行の有効化に固有とは言えない。原因・本変更との因果は未同定で、本タスクでは修正していない。Task6の残課題整理へ引き継ぐ。文脈付き原文は `evidence/other-error-context.log`。

- UnityDebugSheetの `CanvasGroupDrawerBackdrop.SetProgressInternal` のNullReferenceException: r1:60、r2:62、r3:62。
- WebSocketHubの終了時close failed/receive loop faulted: r1:490/492、r2:498/500。Kestrelを開かないr3にはない。
- 終了時 `UserPacketHandler.ReceiveProcess` のSocketException: r1:512、r2:521、r3:500。
- 起動時 `d3d12: failed to query info queue interface (0x80004002)`: 全ケース17行。故障とは未判定。
- 最終build-infoのdirty=Trueとビルド時の自動ピン同期は上記のとおり。生成物の同梱元は0d129c、コードは57bdのまま。自動ピン差分を本変更には含めていない。

## 追跡issueと最終レビュー状態

- `moorestech-albof.2.1`: Windows配布起動時UnityDebugSheet CanvasGroupDrawerBackdropのNullReferenceを調査
- `moorestech-albof.2.2`: Windows終了時WebSocketHubのcloseとreceive loop faultを調査
- `moorestech-albof.2.3`: Windows終了時UserPacketHandler.ReceiveProcessのSocketExceptionを調査
- `moorestech-albof.2.4`: Windows起動時d3d12 info queue query失敗の影響を確認
- `moorestech-albof.2.5`: 配布ビルド中の外部ピン自動同期が最終build-infoをdirtyにする
- `moorestech-albof.2.6`: Editor SearchDatabase例外と遠隔実行PlayModeテストの周辺ログを切り分ける
- `moorestech-albof.2.7`: 検証機でExplorer経由の計画外電源OFFが連続する条件を調査

この実機合格はコード `57bd4b4709` に対するもの。最終レビューの自動反映 `fd635bf73d`・`11ada59aa4` はコンパイル・実機再確認が未完了で、合格を引き継がない。Refix RF1-C1（取消中の再入による待受所有権喪失）は静的指摘のまま未収束。D2〜D4の独立再現作業が自動承認審査に2回拒否され、修正・bug-passを完了できていない。draft PRは未完了の引継ぎ用である。未実行の再現fixtureは製品のテストツリーへ含めずSDDアーカイブへ退避した。

## 最終 head での再確認（2026-10-26 夜）

対象コード: `cc725eb0402f654b75646a95d665b36a856e1d38`（RF1-C1/D4・D2 の修正後。統合ブランチと master の取り込み前で、取り込みで `Client.RemoteExec` は変わっていない）。R8 の 3 ケースはすべて合格。

- 前回と同じ入口 `SteamPlaytestBuildCli.WindowsSteamPlaytestBuild` を batchmode で実行し、exit 0・`Build Result :Succeeded`。master pin は `0d129c8b`、`masterDirty:false`、build-info は前回と同じく `dirty:true`（自動生成の StreamingAssets。退避済み）。pin の自動変更は無かった。
- 配布物 6,664,588,123 bytes を丸ごと LAN 経由で検証機の新しい root `C:/remote-exec-listener-20261026-172117` へ送り、総バイト数（+`steam_appid.txt` 7 bytes）と `Client.RemoteExec.dll` の SHA256 `59EE4B81…6100` の一致を確かめた。Steam 配布・アップロードはしていない。
- ケース 1（フラグなし、r1b）: Listen は `127.0.0.1:25050` のみ、`access.json=none`、Kestrel の `POST /api/remote-exec` は 404、phase1 `success:true` で自然終了。r1 は phase1 が早く終わり実行中の probe を取れなかったため、起動直後から probe する r1b でやり直した。
- ケース 2（フラグあり、r2）: Listen は `127.0.0.1:25100` と `25050`、access.json は `port=25100/processId` 一致。`return 1 + 1;`→`2`、Harmony→`20,777777,20`、client スレッド `1`、server スレッド `23`（サーバーキュー準備完了を確かめてから送信。準備前の 1 回目は `ServerUnavailable` を正しく返した）。すべて exit 0・stderr 0。2,597 bytes の要求で `[RemoteExecWireProbe] Expect=100-continue ContentLength=2597`。quit 後に「待受を閉じました」「入口を撤去しました」、pid・access.json なし。
- ケース 3（WebUi 改名、r3）: Listen は `127.0.0.1:25100` のみ、artifact 拒否と「Web UIなしで続行」のログ、`return 1 + 1;`→`2`。終了後 pid・access.json なし、WebUi を戻し `WebUi-restored=True`・`game-process-count=0`。
- 各ケースの `[RemoteExec]`/`[WebUiHost]` 行の警告語 grep は r1=0・r1b=0・r2=0・r3=0。ログ全体の NullReference/SocketException は無効起動の r1 にも同数出ており、既知の albof.2.1・.2.3 と同じ扱い。

原ログと JSON: `/Users/sakastudio/hermes-agent/data/services/playtest/runs/remote-exec-listener-20261026-172117`（`evidence/<case>/Player.log`、`r2-*.json/.exit/.stderr`、`r3-*.json`、`r2-expect.log`、`windows-copy-verification.log`）。
