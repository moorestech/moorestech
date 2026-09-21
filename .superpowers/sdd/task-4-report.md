# Task 4 実機確認

検証日: 2026-09-21（JST）。対象 HEAD: `c92ffb94f0fc965f81f51f24566e60471b891c62`、ブランチ `feature/editor-direct-play-always-on-capture`。
作業ディレクトリは指定パス。`pwd` の物理表記は `/Users/sakastudio/repos/moorestech`（同一 checkout の symlink 解決）。コード・Unity YAML の手編集なし。検証のみのためコミットなし。

## Step 0

- `uloop launch /Users/sakastudio/hermes-agent/data/repos/moorestech/moorestech_client`: Success/Ready/ServerReady/ProjectIpcReady = true、Unity 6000.3.8f1、PID 33148。
- 起動ツールが stale UnityLockfile と Temp を削除して起動した（ツール標準動作）。Library は変更・削除していない。
- `PlaytestStartGateBypass.UnattendedReason()` は `<null>`。残留印なし。
- シーン未選択だったため EditorSceneManager で既存 `Assets/Scenes/Game/GameInitialaizer.unity` を開いた。playModeStartScene は空。
- 起動前 Error ログ 0 件。シーンを開いた後の clear-console は Error 2/Warning 2/Log 0 を除去したため、シーンロード時点のエラー内容は未採取。
- master HEAD `2aa34d0b8f25d1d8b95b756b3f594231d6b71ed8` はコミット済み pin と一致。ポート 11564 は起動前に LISTEN なし。

## Step 1 有人直 Play

無人印を立てず `uloop control-play-mode --action Play` を実行。MainGame シーンへ移行後 `resolver=True;ready=True;capture=True` を実測。

有人 Play 開始から報告送信後までの Console 全種別の検索結果（停止前）:

| 語 | 件数 |
|---|---:|
| `[AlwaysOnCaptureSetting] 常時記録 enabled:True` | 1 |
| `開始しません` | 0 |
| `有効にしません` | 0 |
| `無効のため` | 0 |

ただし開始ゲート待機中に、DI 構築前の MainGame コンポーネントが Update して NRE を連発した。停止前 Error 総件数 37,453。確認できたスタックは `UIStateControl.Update:48`、`ThirdPersonController.JumpAndGravity:298`、`ThirdPersonController.CameraRotation:168`。加えて操作中の DOM 待機タイムアウトが 1 件含まれる。ゲート通過後に実行した DSL の ErrorLogs は空。今回変更との因果・既存不具合かどうかは未確定であり、「今回変更由来の例外なし」は断定しない。

ゲート操作:

- `同意表示の応答を待ちます` を確認。DOM query は Found=false のまま、ClickWebUi は 15 秒で TimeoutException。Game view を開く前の rendering screenshot は画像なしで失敗。
- Game view を Editor API で表示した後、画面に同意ボタンが見えることを確認。SemanticInput 座標が後続フレームで OS 座標 `(0,1080)` に戻ったため、CEF Browser.SendMouseClick で表示中ボタンを押した。
- 同意の次に前回異常終了確認が出たため「Do not send」を CEF 入力で押した。過去セッションの報告は送信していない。
- OS simulate-keyboard / simulate-mouse-input は不使用。強制的に無人印を立ててゲートを迂回していない。
- スクリーンショット: `.superpowers/sdd/task-4-evidence/Rendering_20260921_183253_856.png`（同意）、`Rendering_20260921_183401_907.png`（異常終了確認）。

## Step 2 Web UI のバグ報告

同じ Play 内で DSL `task4-direct-play-open-report` を実行（新規 boot はしていない）。`AlwaysOnCaptureSetting.Current.IsEnabled` の assert PASS、PauseMenu へ RequestTransition して実際の Web UI を表示。DOM query はゲーム初期化後には正常応答した。

`bug-report-description` の DOM 矩形へ CEF クリック→SendCharEvent で `Task4 direct Play capture verification 20260921` を入力→`bug-report-send` の DOM 矩形へ CEF クリック。ゲーム内部の報告 submit メソッドを直接呼んでいない。

最新箱:
`/Users/sakastudio/Library/Application Support/moorestech/BugReports/outbox/20260921_093640_d66b0a21`

- manifest createdAt `2026-09-21T09:36:40Z`、kind `bug`、isEditor `true`。
- missing は `steamId` 1 件だけ。理由: `テスター識別（SteamID）が差し込まれていない（plan D 未導入またはSteam未起動）`。
- `video` missing 0、`serverSnapshot` missing 0。
- `video.mp4` = 1,224,697 bytes、ffprobe duration = 6.700000 秒。フレーム抽出・目視で実ゲームのオープニング場面が記録されていることを確認（`.superpowers/sdd/task-4-evidence/bundle-video-frame.png`）。
- snapshots は 6 件: ticks `15478,16078,16678,17278,17878,18363`。各 JSON 約 4.75 MB、packet log 5 件、world.json、frames.tsv、screenshot.png、READY あり。
- repository commit は対象 HEAD、masterData commit は pin と一致。repository dirty は既存 `_CompileRequester.cs` に由来。
- DeveloperMode のため outbox に保持されている。
- 進行記録 `/Users/sakastudio/Library/Application Support/moorestech/ProgressRecords/current/pid_33148/session_639255798183667200/events.jsonl` に次を確認:

```json
{"t":"2026-09-21T09:36:42Z","tick":19138,"type":"reportSent","data":{"kind":"bug"}}
```

DSL 成果物: `moorestech_client/PlaytestResults/adhoc/task4-direct-play-open-report/result.json`、Success=true、Asserts 1/1 PASS、ErrorLogs=[]、recording.mp4=598,794 bytes、pause-report.png=836,507 bytes。画面はポーズメニューと実ゲーム内のキャラクター/オープニング背景。自由操作 HUD の通常地上視点までの検証はしていない。

有人 Play は Stop 成功（IsPlaying=false）。停止後 clear-console は Error 37,457 / Warning 17 / Log 470。停止時に増えた 4 Error の原因は未採取。

## Step 3 無人起動

実行: `uloop run-tests --project-path .../moorestech_client --filter-type regex --filter-value 'PlaytestReportAndProgressTest|EditModeInPlayingTestUtilTest' --unsaved-changes fail`

CLI は `UNITY_DISCONNECTED_AFTER_ACCEPT` (Cause EOF, SafeToRetry=false) で終了したが、Unity のテストは完了していた。再実行で結果を上書きせず、Unity が保存した `/Users/sakastudio/Library/Application Support/sakastudio/moorestech/TestResults.xml` を回収。

- test-run start `2026-09-21 09:38:09Z` / end `09:38:39Z`、total=2 / passed=2 / failed=0 / skipped=0。
- `PlaytestReportAndProgressTest.種別付きで送るとmanifestに載り終了で進行記録が出る`: Passed。
- `EditModeInPlayingTestUtilTest.CreateServerSettings_オートセーブと常時記録を無効化する`: Passed。
- 証跡コピー: `.superpowers/sdd/task-4-evidence/unattended-test-results.xml`。
- 当該区間の `enabled:True`=1、`有効にしません`=1（reason:unattendedBootMark）、`開始しません`=0、`無効のため`=0。Enabled はテスト自身が明示設定した 1 件で、自動設定ではない。R3 の意図に一致。
- 無人テスト区間の Console Error は 36 件を取得、直後 clear-console は Error 37 / Warning 16 / Log 195。例: `地面が見つかりませんでした x:0 z:0 layer:512`。テストは `LogAssert.ignoreFailingMessages=true` を使用するため、PASS は出力が無傷であることを意味しない。
- テスト後 Status は IsPlaying=false。

追加 DSL `sample-chest` を区間を分けて実行。preflight は compile/master load/port を含む 5/5 PASS。セッション `PlaytestResults/20260921_183938`、ready まで約 72 秒。

- `sample-chest/result.json`: Success=true、Asserts 2/2 PASS、ErrorLogs=[]。実行時刻 `2026-09-21T18:40:54.3707370+09:00`〜`18:40:56.7003180+09:00`。
- 無人 DSL 区間の Console 全種別検索: `enabled:True`=0、`有効にしません`=1（reason:unattendedBootMark）、`開始しません`=3（スナップショットリング・録画リング・進行記録）、`無効のため`=0。
- この区間の拒否語は無人起動での期待動作。無人の印が開始ゲートまで残り、ready とシナリオ完了へ到達したことを確認。
- 既存 sample-chest は Record=false であり、この補助シナリオの動画は生成されない。final.png は目視したが開幕スキット中の宇宙船が映り、設置チェストや通常プレイヤー視点は映っていない。そのため「録画付き実プレイ視点の検証」全条件を満たしたとは扱わず、無人起動と抑止ログの確認としてのみ採用。
- ランナーが Play を停止し、追加 Stop でも IsPlaying=false / WasAlreadyStopped=true、StoppedAt=`2026-09-21T09:40:58.6592150Z` を確認。

## Step 4 記録・懸念

- 指示により `bd moorestech-sdme` が存在しないため bd note は行っていない。完全な実測は本報告へ記録する。
- `_CompileRequester.cs` の既存 dirty は変更・復元・コミットしていない。
- 最終ステータス: **DONE_WITH_CONCERNS**。
- 主目的の有人自動有効化・有人報告の video/serverSnapshot/reportSent・無人明示 Enabled 維持・無人 DSL の自動有効化抑止は実測できた。
- 懸念: 有人の開始ゲート待機中に NRE が連発し、「今回変更由来の例外なし」を断定できない。無人テストにも地形関係 Error がある。CLI のテスト応答は切断したが一次資料 XML により 2/2 PASS を回収している。
- 未実施: 別の専用再生ボタン（生成ワールドで Play / セーブ無しで Play）の有人実機操作。Task 4 ブリーフの素の Play は実施済み。
- bd 未記録、出力エラー、通常地上視点の未確認を Task 5 (a) で起票する判断が必要。
- 変更ファイルは本報告のみ。補助証跡は git 無視対象の `.superpowers/sdd/task-4-evidence/` と `PlaytestResults/` に保存。既存 `_CompileRequester.cs` dirty を維持。新規コミットなし（検証担当への明示指示）。

## レビュー修正追記（2026-09-21 19:46–19:51 JST）

再開担当による確認。対象 HEAD は引き続き `c92ffb94f0fc965f81f51f24566e60471b891c62`。コード・Unity YAML・セーブ形式の変更なし。前担当の `recheck-*` 証跡を保存したまま、新しい証跡を `task-4-evidence/fix2-*` へ追加した。

### Important 1: Error の因果分離

- `uloop launch /Users/sakastudio/hermes-agent/data/repos/moorestech/moorestech_client` は Success/Ready=true、PID=31032。Unity プロセス不在のため起動した。launch が stale Lockfile と Temp を除去した。Library は削除していない。
- `execute-dynamic-code` で `PlaytestStartGateBypass.UnattendedReason()` を読み捨て、`EditorSceneManager.OpenScene("Assets/Scenes/Game/GameInitialaizer.unity")` と GameView 表示を実施。`residual=<null>;playing=False`（`fix2-preconditions.json`）。
- `get-logs --log-type Error --max-count 100 --include-stack-trace` → `clear-console` → `control-play-mode --action Play` の順に実行した。全 uloop コマンドの project-path は `/Users/sakastudio/hermes-agent/data/repos/moorestech/moorestech_client`。
- 無人印を設定せず、capture や DI の状態を注入していない。今回は同意／前回異常終了の待機ログが出ず、そのまま MainGame へ到達した。CEF クリックは不要だった。OS 入力 simulate 系は一切呼んでいない。
- `fix2-ready-state.json`: `utc=2026-09-21T10:48:16.0725010Z;resolver=True;ready=True;capture=True;frame=1666`。
- ready 到達後に `clear-console` を実行し、その直後 `execute-dynamic-code` から既存 `PlaytestRunner.Run` へ `task4-fix2-post-ready` を投入した。`Record=false`、`await p.WaitSeconds(5f)`、resolver 非 null と capture enabled の2 assert を実行。新しい起動や無人印の設定はしていない。
- `fix2-dsl-result.json`: 19:48:17.863–19:48:23.858 JST、Success=true、assert 2/2 PASS、ErrorLogs=[]。待機区間は frame 1830→2126（296フレーム）、Timeline に 5.0秒と記録された。DSL の初期安定待ちを含め実行全体は約6秒。
- 完走後 `get-logs --log-type Error --include-stack-trace --max-count 10000` は TotalCount=DisplayedCount=0（`fix2-post-ready-errors.json`）。さらに `--log-type All` は TotalCount=DisplayedCount=3、全件 Log（`fix2-post-ready-all.json`）。単に Error フィルタから漏れた例外が無いことも全件で確認した。

前回ゲート待機時の NRE を発生箇所まで確認した:

| 代表 stack | null 依存の設定箇所 | capture 変更との関係 |
|---|---|---|
| `UIStateControl.Update:48` | `_uiStateDictionary` は `Construct:20` で設定 | Update は辞書を直接参照し、capture 設定を参照しない |
| `ThirdPersonController.CameraRotation:168` | `_input` は `Initialize:105` で設定 | LateUpdate から初期化前の `_input.look` を直接参照 |
| `ThirdPersonController.JumpAndGravity:298` | 同上 | Update から初期化前の `_input.jump` を直接参照 |

`MainGameInitializationFinalizer.cs:58` は開始ゲートを await し、その後の `:62-63` で `StartGame` と ClientDIContext 構築へ進む。したがって、ゲート待機中に上の MonoBehaviour が更新されると依存が未設定のままになる。前担当の `recheck-before-gate-state.json` は resolver=False、`recheck-ready-state.json` はゲート通過後 resolver=True。`recheck-before-stop-summary.json` の9,555 Errorは上の3 stack 各3,185件であり、この初期化待機区間と整合する。

検索証拠は `fix2-source-causality.json`。上の2コンポーネントと Finalizer を対象に `rg -n 'AlwaysOnCapture|DirectPlay'` は exit=1、該当0件。同じ3ファイルの `git diff master...HEAD -- <paths>` は空（比較 master=`27c3a9e42`）。直Play変更は `DirectPlayAlwaysOnCaptureSettings.ApplyForUnattendedReason` で設定を Apply する経路であり、これらの未初期化参照やゲート後の DI 構築順を変更していない。これらの代表 NRE は capture 変更経路外の開始ゲート／初期化順の問題と切り分けられる。今回の capture enabled を維持した ready 後5秒では再発していない。

ただし、この結果を「起動から停止まで全 Error が無い」とは扱わない。起動前 `get-logs Error=0` に対し clear の ErrorCount=2、ready 前 `get-logs Error=0` に対し clear の ErrorCount=1 という差があった（`fix2-preplay-errors.json` / `fix2-preplay-clear.json` / `fix2-startup-errors.json` / `fix2-ready-clear.json`）。clear 前に全種別を保存しなかったため、この差の原文は未確定。ready 後は All と Error の両方を保存しており、その区間の0件は確認できた。元の37,453件すべてや元の停止時追加4件をこの再実行で遡及的に分類したわけでもない。

### Minor: 生ログと停止状態の保存

- `get-logs --log-type All --search-text 'PlaytestStartGates|enabled:True|開始しません|有効にしません|無効のため' --use-regex --include-stack-trace --max-count 100` の生応答を `fix2-startup-filtered.json` へ保存。TotalCount=DisplayedCount=1、`[AlwaysOnCaptureSetting] 常時記録 enabled:True` が1件、拒否語3種は各0件。Apply までの stack も同梱。
- `control-play-mode --action Stop` は IsPlaying=false / Changed=true。続く `--action Status` の生応答を `fix2-stop.json` へ保存。IsPlaying=false、StoppedBy=cli-control-play-mode、StoppedAt=`2026-09-21T10:48:51.9193630Z`。
- 停止後の All 全件は `fix2-after-stop-all.json`。20件中 Log=18、Warning=2、Error=0。Warning は ffmpeg 終了待ち timeout→kill と、同期終了時の正常終了マークに関するもの。元の停止時4 Error は今回再現していない。
- 無人区間の生ログは前担当が保存した `unattended-dsl-console-filtered.json` にある。今回、既に PASS と判断された無人テスト／報告送信は繰り返していない。

### Important 2: Beads 参照不整合

コントローラーから「pull 後も moorestech-sdme が不在。作成・偽造しない」と明示されたため、bd note は未実施のまま。実測はこの追記と一次証跡へ保存した。plan の durable note 要件の参照先解決はコントローラーへ戻す。架空の issue に記録済みとはしない。

### 修正担当の自己レビューと引き継ぎ

ready 後の例外0件、有人の有効化／拒否語件数、生ログ、最終停止状態の不足を補った。コードや保存形式を変更していないためコンパイル・migration 追加は対象外。既存 `_CompileRequester.cs` の dirty を維持した。総合ステータスは **DONE_WITH_CONCERNS**（Beads 参照先未解決、起動時 Console 件数差、停止時 ffmpeg warning）。本追記は検証結果であり、開始ゲート待機中の既存不具合を修正したものではない。
