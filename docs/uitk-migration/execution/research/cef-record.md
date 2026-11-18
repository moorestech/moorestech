# 調査: CEF 停止の記録の master 切り出し（bd moorestech-albof.18）

調査日 2026-11-06。基準: `origin/master` = `e95dc17db1`、`origin/feature/uitk` = `54c8a4c8c8`、merge-base = `979725bcd1`。
パスは `moorestech_client/Assets/Scripts/` を `S/` と略す。行番号は各 ref の `git show` で実測した。推測には「推測」と書いた。

## 0. 結論（3 行）

- 該当するのは feature/uitk の 3 コミット `19b34304aa`（Task 7 本体）・`271cfcec4b`（異常終了の箱・Dispose）・`d1bfc4afbd`（D-3 の改名。同じコミットに入っている設定画面のファイル削除は無関係）。このほかに `5421c7f9f0` が閾値定数を `private` に戻している。
- 段階 1 の基盤への依存は 4 つ: 入力排他の改名（`UiInputExclusivity`、Task 1 `aa0efcdd33`）、監視の登録先（`MainGameUiRegistration`、段階 1 で新設）、outbox 根の注入（D-2 `4a80048bf4` の `TestBugReportOutboxRoot`／`BugReportOutboxRoot`）、休止（`SetWebUiSuspended`、R1）。master 版では 4 つとも master の既存物（`WebUiInputExclusivity`・`MainGameModelRegistration`・既定 outbox・休止なし）へ差し替える。新規ファイル 7 本、変更ファイル 12〜13 本の小さな PR で済む。
- feature/uitk では、次の master 取り込みで重複を解く。新規ファイルは feature/uitk の blob（.meta を含む）をそのまま使い、中身を変えたものだけを衝突として手で解決する。その手順は §6 の表にまとめた。

## 1. feature/uitk の中の該当コミットとファイル

`git log origin/master..origin/feature/uitk -- <該当パス>` で実測した（102 コミットのうち、関係するのは次のもの）。

| コミット | 中身 | 切り出しの扱い |
|---|---|---|
| `19b34304aa` Task 7 | 生の値 3 つを manifest v6 へ載せる。WS 受信時刻の記録、無応答時に Web 側の値を無視、WebUiStartup の LogError（31 ファイル、+494/-17） | 本体。master 版へ書き換えて持ち込む |
| `271cfcec4b` Task 7 fix | 異常終了の箱（`CrashBundleWriter`）では `unknown` と欠損理由を書く。enum に `Unknown` を足す。監視から `IDisposable` を外す（`.superpowers/sdd/task-7-fix-report.md` は持ち込まない） | 持ち込む |
| `d1bfc4afbd` D-3 | `CefProcessState.Running` → `ObservedAfterBrowserStart` へ改名。ホスト起動失敗時は観測を始めない（`WebUiCefNavigator` の呼び位置を `url` の null 判定の後へ）。同じコミットの `GraphicsQualityItemRows.cs` 削除は無関係 | 改名と呼び位置だけ持ち込む |
| `5421c7f9f0` | `UiInputExclusivity.WebSilenceThresholdSeconds` を `public const` から `private const` へ（1 行） | 最終形を写す（private） |
| `4a80048bf4` D-2 | outbox 根の注入。テストは `TestBugReportOutboxRoot` へ書く | **持ち込まない**（段階 1 の基盤。§2） |
| `aa0efcdd33`・`3116fb8c07` Task 1 | `WebUiInputExclusivity` → `UiInputExclusivity` への改名と GameUi 側の値 | **持ち込まない**（§2） |
| `bdaa84c383` D-1 | 休止中の `ui_state.request` 拒否（dispatcher に 8 行） | 持ち込まない（UITK 休止の機能） |

feature/uitk 上の最終形のファイル（`git show origin/feature/uitk:<path>` で読んだもの）:

- 新規 `S/Client.Game/InGame/BugReport/WebUiHealth/IWebUiHealthProbe.cs`（9 行）
- 新規 `S/Client.Game/InGame/BugReport/WebUiHealth/WebUiHealthReading.cs`（48 行）。enum `WebUiHostStartupState { NotAttempted, Started, Failed, SkippedByEditorSwitch, Unknown }`、`CefProcessState { NotObserved, ObservedAfterBrowserStart, LostRestarting, RecoveryFailed, Unknown }`、`double? SecondsSinceLastWebMessage`、`UnknownForPreviousSession()`。enum は `StringEnumConverter`＋`CamelCaseNamingStrategy` で書き出す
- 新規 `S/Client.WebUiHost/Health/WebUiHealthRecorder.cs`（70 行。static。lock を使う。`CefRuntime.ServerLost/ServerRecovered/ServerRecoveryFailed` を購読する。`RecoveryFailed` だけ LogError を出す）
- 新規 `S/Client.WebUiHost/Health/WebUiHealthProbe.cs`（21 行。`UiInputExclusivity.SecondsSinceLastWebMessage(Stopwatch.GetTimestamp())` を読む）
- 新規 `S/Client.WebUiHost/Boot/WebUiSilenceWatcher.cs`（22 行。`IInitializable`。`Observable.Interval(1s)` から `UiInputExclusivity.EvaluateWebSilence` を呼ぶ）
- 変更 `S/Client.Input/UiInputExclusivity.cs` の 21-23・31-32・115-146・169・180-190 行（閾値 15.0、受信時刻、`_webSilent`、`EvaluateWebSilence`、テスト用 reset）
- 変更 `BugReportManifest.cs`（v6、`WebUiHealth = new()`）、`BugReportBundleWriter.cs`（ctor に probe を足し、`CreateHeader` の直後で `Read()`）、`LastSession/CrashBundleWriter.cs`（`UnknownForPreviousSession()`＋`AddMissing("webUiHealth", …)`）
- 変更 `S/Client.Starter/Initialization/Boot/WebUiStartup.cs`（成否を記録し、LogWarning を LogError にする。Editor スイッチは LogWarning のまま `SkippedByEditorSwitch`）
- 変更 `S/Client.WebUiHost/Boot/WebSocketMessageDispatcher.cs`（`DispatchAsync` の先頭で、中身を問わず受信時刻を記録する）
- 変更 `S/Client.WebUiHost/Cef/WebUiCefNavigator.cs`（`url` の null 判定の後で `ObserveCefRuntime()`）
- 変更 `S/Client.Starter/Registration/PlaytestRecordRegistration.cs`（`IWebUiHealthProbe` を登録）、`MainGameUiRegistration.cs`（`RegisterEntryPoint<WebUiSilenceWatcher>()`。**段階 1 で新設したファイル**）
- テスト: `S/Client.Tests/BugReport/Bundle/{FixedWebUiHealthProbe,WebUiHealthManifestTest}.cs`、`S/Client.Tests/BugReport/LastSession/CrashBundleWebUiHealthTest.cs`、`S/Client.Tests/Ui/Input/UiInputExclusivityWebSilenceTest.cs`。既存テストの更新は `BugReportManifestTest.cs`（キー一覧に `webUiHealth`）、`RemoteExecBundleMarkTest.cs`（5→6）、`BugReportBundleWriterIdentityTest.cs`（ctor 3 か所）、`DeveloperReportRealReceiverCheck.cs`（ctor）、`Ui/State/PauseMenuCommandsTestFactory.cs`（ctor。段階 1 のファイル）
- シナリオ: `.agents/skills/unity-playmode-recorded-playtest/scenarios/playtest-receiver/developer-report-interrupt.cs:13`（ctor を変更）、`scenarios/uitk/stage1/uitk-lifeline.cs:81`（`webUiHealth` があることを assert。UITK 専用）

## 2. master に無い依存（段階 1 の基盤への依存）

| 依存 | feature/uitk の形 | master の現物 | master 版での置き換え |
|---|---|---|---|
| 入力排他の型名 | `UiInputExclusivity`（Task 1 で改名。GameUi 値・ESC ラッチ・休止 `_webUiSuspended` を持つ） | `S/Client.Input/WebUiInputExclusivity.cs`（60 行。`_pointerOverUi`・`TextInputFocusedProperty`・lock `TextInputFocusLock` だけ）。参照は 22 か所（`git grep` で実測） | `WebUiInputExclusivity` に `_webSilent`・`_lastWebMessageTimestamp`・`RecordWebMessageReceived`・`SecondsSinceLastWebMessage`・`EvaluateWebSilence`・`ResetWebSilenceForTest` を足す。`IsPointerOverWebUi` と `IsSuppressed`／`TextInputFocused` が `_webSilent` を見るように変える。**改名はしない**（改名は段階 1 の範囲） |
| `RecomputeTextInputFocused` | GameUi・休止・無応答の論理和を 1 か所で計算する | 無い。`SetState` が `TextInputFocusedProperty.Value = textInputFocused` を直接書く（`WebUiInputExclusivity.cs:32-42`） | `_webTextInputFocused` を生値として保持し、`TextInputFocusedProperty.Value = !_webSilent && _webTextInputFocused` を lock の中で計算する小さな private 関数を足す。`SetState` と `EvaluateWebSilence` の両方から呼ぶ |
| 監視の登録先 | `S/Client.Starter/Registration/MainGameUiRegistration.cs`（段階 1 で新設。master に無い） | `Registration/` は 7 ファイル（`MainGameModelRegistration.cs` 141 行など） | `MainGameModelRegistration.cs` に `builder.RegisterEntryPoint<WebUiSilenceWatcher>();` を足す（agent 既定。理由: MainGame の DI に入り、Web の常駐と同じ寿命になる。141 行なので 200 行に収まる） |
| outbox 根の注入（D-2） | `BugReportBundleWriter(identity, probe, IBugReportOutboxRoot)`、`CrashBundleWriter.Write(…, outboxRoot)`、テストは `TestBugReportOutboxRoot` | 無い（`git grep BugReportOutboxRoot origin/master` が 0 件）。ctor は `BugReportBundleWriter(IPlaytestSessionIdentity)`（`BugReportBundleWriter.cs:46`）、`Write` は 4 引数 | ctor は `(identity, IWebUiHealthProbe)` の 2 引数にする。テストは master の前例（`CrashBundleWriterTest.cs:181` の 4 引数 `Write` と `finally` での `Directory.Delete`）に従う |
| Web 休止（R1） | `SetWebUiSuspended`。テスト「休止とは独立に無応答を判定する」 | 無い | このテストケースは持ち込まない。残る 3 ケース（閾値の前後と復帰・未受信・秒の換算）を持ち込む |

そのほかの依存は無い（実測）:
- asmdef: `Client.WebUiHost.asmdef` は master でも `Client.Game`・`Client.Input`・`CefUnity.Runtime`・`UniRx`・`VContainer` を参照している。feature/uitk でも `Client.WebUiHost.asmdef` は変わっていない。`Client.Starter.asmdef` の差分は `Client.Ui` の追加だけで、この機能には要らない
- `CefRuntime`（`CefUnity.Interop`）は upm パッケージ `jp.juha.cefunity`（`moorestech_client/Packages/manifest.json:44`）にあり、master にもある
- `Client.Input/AssemblyInfo.cs` が `InternalsVisibleTo("Client.Tests")` を持つので、`#if UNITY_EDITOR internal` のテスト用 reset は master でもテストから呼べる
- master と merge-base の間で、該当パス（Client.Input・BugReport・WebUiHost/Boot・Cef・Client.Starter・Client.Tests）に差分は無い（`git diff --stat 979725bc origin/master -- …` が空）。feature/uitk の差分はそのまま master の現物に当たる

## 3. master 単独で成立させる最小差分

### 3.1 新規（7 本＋.meta）

| ファイル | 中身 | feature/uitk の blob と同一にできるか |
|---|---|---|
| `S/Client.Game/InGame/BugReport/WebUiHealth/IWebUiHealthProbe.cs` | 最終形のまま | **同一**（`git checkout origin/feature/uitk -- <path> <path>.meta` で取る） |
| `…/WebUiHealth/WebUiHealthReading.cs` | 最終形のまま（`Unknown`・`ObservedAfterBrowserStart`・`UnknownForPreviousSession` を含む） | **同一** |
| `…/BugReport/WebUiHealth.meta`（フォルダの meta） | — | **同一** |
| `S/Client.WebUiHost/Health/WebUiHealthRecorder.cs` | 最終形。ただし 64 行目のコメント「復旧を諦めたCEFは**命綱以外の**UIを失う」は master では誤りになる（master には UITK の命綱が無く、全 UI を失う） | コメントを直すと**不一致**になる（§6 で feature 側を採る）。agent 既定: 直す（誤ったコメントを master に入れない） |
| `S/Client.WebUiHost/Health/WebUiHealthProbe.cs`・`Health.meta` | `UiInputExclusivity` を `WebUiInputExclusivity` に置き換える | **不一致**（1 語）。フォルダ meta は同一 |
| `S/Client.WebUiHost/Boot/WebUiSilenceWatcher.cs` | 同上の置き換え | **不一致**（1 語）。Boot は 9→10 ファイルで、10 ファイル/ディレクトリの上限ちょうど（実測） |
| テスト 3 本（§5） | — | 一部不一致 |

`.meta` について: AGENTS.md は「.meta を手で作らない」と定めている。feature/uitk の `.meta` は Unity が生成してコミット済みのものなので、`git checkout origin/feature/uitk -- X.cs.meta` で取るのは手作りに当たらない、と読んだ（**agent 既定**）。こうすると GUID が両ブランチで一致し、master を取り込むときに `.meta` が add/add で衝突しない。中身を変えたファイルの `.meta` も同じ方法で取る（GUID は中身に依らない）。Unity に作らせ直すと GUID が別になり、取り込みのたびに `.meta` の衝突を手で解くことになる。

### 3.2 変更（master の現物への当て方）

| ファイル（master の行） | 変更 |
|---|---|
| `S/Client.Input/WebUiInputExclusivity.cs`（60 行→約 115 行） | §2 の 1・2 行目のとおり。閾値は `private const double WebSilenceThresholdSeconds = 15.0;`（`webSocketClient.ts:89` の 15000ms を参照するとコメントに書く）。無応答に入ったときは `Debug.LogWarning`、戻ったときは `Debug.Log`（feature と同じ）。ProbeSuppressed は変えない |
| `S/Client.WebUiHost/Boot/WebSocketMessageDispatcher.cs:28-29`（180 行→184 行） | `DispatchAsync` の先頭（`TryDeserialize` の前）で `WebUiInputExclusivity.RecordWebMessageReceived(Stopwatch.GetTimestamp());` を呼ぶ。`using System.Diagnostics;` を足す |
| `S/Client.WebUiHost/Cef/WebUiCefNavigator.cs:42-43` の直後 | `WebUiHealthRecorder.ObserveCefRuntime();`（d1bfc4afbd の位置。`url` が空なら観測しない） |
| `S/Client.Starter/Initialization/Boot/WebUiStartup.cs:15-35` | 18 行の前に `RecordHostStartup(SkippedByEditorSwitch)` を置く。28・33 行を `LogError` にし、成否を記録する。**21-22 行のコメントは master 用に書く**: feature/uitk の「命綱はUITKにあるので続行し」は master では誤り。master では「Web が無いと UI は出ないが、ゲームの初期化は続け、失敗は LogError で開発者へ届ける」とする |
| `S/Client.Game/InGame/BugReport/BugReportManifest.cs:34, 69 の後` | `SchemaVersion = 6` と v6 の 2 行コメント。`public WebUiHealthReading WebUiHealth = new();` |
| `S/Client.Game/InGame/BugReport/BugReportBundleWriter.cs:46, 65 の後`（188 行→191 行） | ctor に `IWebUiHealthProbe webUiHealth` を足す。`CreateHeader` の直後で `manifest.WebUiHealth = _webUiHealth.Read();` |
| `S/Client.Game/InGame/BugReport/LastSession/CrashBundleWriter.cs:52 の後`（146 行→152 行） | `UnknownForPreviousSession()` と `AddMissing("webUiHealth", "前回セッションのWebホスト・CEFの値は記録していない")` |
| `S/Client.Starter/Registration/PlaytestRecordRegistration.cs:26 の前` | `builder.Register<IWebUiHealthProbe, WebUiHealthProbe>(Lifetime.Singleton);`。この登録は両方の起動経路で通る（`BugReportBundleWriter` と同じ位置。master 23-28 行のコメントのとおり） |
| `S/Client.Starter/Registration/MainGameModelRegistration.cs` | `builder.RegisterEntryPoint<WebUiSilenceWatcher>();`（§2） |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/playtest-receiver/developer-report-interrupt.cs:12` | ctor の第 2 引数に `new WebUiHealthProbe()`。**master 版は 2 引数**（feature/uitk の 13 行は D-2 の 3 引数） |
| ADR `docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md:74` 付近 | manifest の契約として `webUiHealth`（SchemaVersion 6、3 つの値の綴り・null の意味・crash の箱では unknown）を 1 段落足す（agent 既定。v5 の remoteExec が同じ場所に書かれているため） |

### 3.3 「bug-report-auto-fix の読み手も同じ PR で対応する」（plan §3.11 の 1）は前提が古い

- `.decisions/2026-10-31-自動修正の基盤を消しプレイテスト生データの正本をR2だけにする.md` で、bug-report-auto-fix スキル・inbox poller・enqueue-autofix は削除済み（実測: `git grep -l bug-report-auto-fix origin/master` で当たるのは docs・decisions だけ。scripts には無い）。plan.md:535 と :99 はこの裁定より古い記述が残ったものと読める（推測）
- 今の manifest の読み手は `scripts/playtest/` の取り込み・ダッシュボード・日次ダイジェスト。manifest の `schemaVersion` は検査していない。`digest_schema.py:36-41` の `MANIFEST_SCHEMA` は `kind/description/remoteExec/buildInfo` だけを型検査し、`schemaVersion==1` の検査は `record.json` だけが対象（`digest_schema.py:163`）。したがって v6 にしても読み手は壊れない（コードを読んだ結論。実データでは回していない）
- 生の値は logs repo の `manifest.json` と `fetch-report.sh` で人と agent が読める。agent 既定: ダッシュボードの `display_manifest`（`scripts/playtest/dashboard/collect_reports.py:109-123`）に `webUiHealth.hostStartup/cefProcess/secondsSinceLastWebMessage` を `pick()` で 3 つ足して表示し、`scripts/playtest/tests/test_dashboard.py` に 1 ケース足す。これを「読み手の対応」とし、plan §3.11 の文言を直すことを計画側の TODO にする

## 4. master 側の既存コードとの接続点（まとめ）

- WS の受信: `WebSocketHub.HandleConnectionAsync` → 受信ループ → `WebSocketMessageDispatcher.DispatchAsync`（`Boot/WebSocketMessageDispatcher.cs:28`）。WS スレッドから呼ばれるので、時刻は `Interlocked` で書く
- Web の ping: `webSocketClient.ts:86-94` が 5 秒ごとに `ping` を送り、pong が 15 秒途絶えると close する。正常時の受信間隔は最大でも約 5 秒なので、閾値 15 秒は ping 3 回分の欠落にあたる
- 切断時のリセット: `WebSocketHub.cs:174`（`if (_connections.IsEmpty) WebUiInputExclusivity.SetState(false, false);`）。無応答の判定とは独立しているので変えない
- 抑止の読み手（変更なしで効く）: `HybridInput.cs:96-97`、`InputManager.cs:168,177,183`（`CloseUI` は Keyboard 抑止の対象。`InputManager.cs:110`）、`UiPointerHitTest.cs:19`、`PlayerSystemContainer.cs:37`（`TextInputFocused` を購読）、`PlaytestUiOps.cs:138`（`IsPointerOverWebUi` で待つ）
- バグ報告: `PlaytestRecordRegistration.Register` → `BugReportBundleWriter`（DI）→ `BugReportManifest.CreateHeader` → `manifest.json`。異常終了の箱は `CrashBundleWriter.Write`（DI 外の static）
- ホストの起動: `InitializeScenePipeline.cs:80` → `WebUiStartup.StartAsync`。CEF の観測は MainGame シーンの `WebUiCefNavigator`

## 5. テスト

| テスト | 置き場（master） | 中身 | feature/uitk との関係 |
|---|---|---|---|
| `WebUiInputExclusivityWebSilenceTest` | `S/Client.Tests/Input/`（master では 2→3 ファイル。master に `Client.Tests/Ui/` は無い） | feature の 4 ケースから「休止とは独立」を除いた 3 ケース。型名は `WebUiInputExclusivity`。TearDown は `ResetWebSilenceForTest()`＋`SetState(false,false)` | feature 側は `Ui/Input/UiInputExclusivityWebSilenceTest.cs`。**別パスなので取り込み後に両方が残る**。§6 で master 版を消す |
| `FixedWebUiHealthProbe` | `S/Client.Tests/BugReport/Bundle/`（2→3） | 最終形のまま | **同一 blob にできる** |
| `WebUiHealthManifestTest` | 同上（3→4） | 4 ケースのうち、ケース 4（WriteAsync 経由）は ctor を 2 引数にして master の既定 outbox へ書き、`finally` で消す（master の前例 `BugReportBundleWriterIdentityTest` と同じ） | 不一致（1 行）。§6 で feature 側を採る |
| `CrashBundleWebUiHealthTest` | **`S/Client.Tests/BugReport/Bundle/` に置く**（agent 既定）。feature の置き場 `BugReport/LastSession/` は master で `.cs` が 10 本あり、11 本目は 10 ファイル/ディレクトリの規約違反になる（実測: master=10、feature/uitk=11。feature 側はすでに違反している） | `Write` を 4 引数で呼ぶ。`TestPreviousSessionArtifacts.Unclean()` は master にある（`CrashBundleWriterTest.cs:113`） | 取り込み後に feature の `LastSession/` 版と重複する。§6 で feature 版を消し、master 版を 5 引数へ直す（feature の違反もここで解ける） |
| 既存テストの更新 | — | `BugReportManifestTest.cs:35` のキー一覧に `"webUiHealth"`。`RemoteExecBundleMarkTest.cs:70` を 5→6。`BugReportBundleWriterIdentityTest.cs:29,60,94` と `DeveloperReportRealReceiverCheck.cs:36` の ctor に `FixedWebUiHealthProbe.NotAttempted()`。**`BugReportSubmitKindTest.cs:42` も更新が要る**（feature/uitk ではこの呼び出しが段階 1 の `PauseMenuCommandsTestFactory` へ移っていたため、Task 7 の差分に無い。master では直さないとコンパイルが落ちる） | ctor 引数の数が違うので、取り込みのとき feature 側を採る |

実行: `uloop compile` の後、`uloop run-tests --filter-type regex --filter-value "WebSilence|WebUiHealth|BugReportManifestTest|RemoteExecBundleMarkTest|BugReportBundleWriterIdentityTest|BugReportSubmitKindTest|CrashBundle|TextInputMovementLock|PlayableInteractInput"`（入力排他の既存テストを含める）。

unityプレイ録画テスト（入力排他という実行時の挙動に触れるため入れる）:
1. 既存シナリオ `scenarios/playtest-receiver/developer-report-interrupt.cs`（ctor 更新後）と `developer-report-delivery.cs` を流す。書き出した箱の `manifest.json` が `schemaVersion 6`、`webUiHealth.hostStartup="started"`、`cefProcess="observedAfterBrowserStart"`、`secondsSinceLastWebMessage` が 0 以上 15 未満であることを、シナリオの assert で確かめる（`uitk-lifeline.cs:81` の前例に倣って 1 行足す）
2. Web の入力欄を使う既存シナリオ（BP 名入力など。ESC と移動の抑止に触れるもの）を 1 本流し、正常時は抑止が従来どおり効いていることを確かめる（無応答の印が誤って立たないことの確認）
3. 無応答の再現（JS を止めて WS だけ残す）は Editor の中では作りにくい（推測）。L0 の 3 ケースで代える

## 6. feature/uitk 側で二重にならない取り込み方

方針: 二重は **master を feature/uitk へ取り込むとき（albof.20 または週 1 回の取り込み）に解消する**。feature/uitk 側で事前に revert はしない。ただし W0-cef の最終レビューで、バグ報告側（webUiHealth を確保時に読む・writer の ctor）は master の形が正になった（W0-cef done.md「integrator への依頼」）。入力排他・WebUiHost は feature の形（型名 `UiInputExclusivity`・UITK の命綱前提のコメント）を採る。

I-weekly-1（2026-11-06、master 0e81bbe3c3 の取り込み）で当てた実測の解決表:

| パス | 実測の状態 | 解決 |
|---|---|---|
| `WebUiHealth/IWebUiHealthProbe.cs`・フォルダ `.meta` | 同一 blob で衝突しない | 何もしない |
| `WebUiHealth/WebUiHealthReading.cs` | add/add で衝突（master が `UnknownForPreviousSession` の注記と綴りのコメントを足した） | master 側を採る |
| `BugReportBundleWriter.cs` | 衝突。master は ctor 1 引数（identity）で webUiHealth を `BugReportCapturedData.WebUiHealth` から写し、無ければ LogError＋欠損＋unknown | master の本文を採り、ctor は feature の outbox 根の注入と合わせて `(IPlaytestSessionIdentity, IBugReportOutboxRoot)` の 2 引数にする（probe の引数とフィールドを消す） |
| `BugReportManifest.cs` | コメントだけ衝突 | master 側を採る |
| `Capture/*`・`CrashBundleWriter.cs`・`UncollectedBugReportCaptureSources.cs`・テストの capture 偽物 | 自動で解ける | 何もしない |
| `PlaytestRecordRegistration.cs` | 自動で解けるが `IWebUiHealthProbe` の登録が 2 行になる（feature は writer 用に全起動で、master は確保を集める起動だけ） | feature 側の行を消し、master の位置（確保を集める起動だけ）の 1 行にする |
| `Health/WebUiHealthRecorder.cs` | add/add で衝突（master は `internal` 化） | `internal` は master を採る（`Client.Tests`・`Client.Starter` へは `InternalsVisibleTo` 済み）。CEF 復旧断念の LogError のコメントは feature（命綱前提）を採る |
| `Health/WebUiHealthProbe.cs`・`WebSocketMessageDispatcher.cs`・`WebUiStartup.cs` | 衝突（型名・コメント） | feature 側を採る（型名 `UiInputExclusivity`） |
| `Boot/WebUiSilenceWatcher.cs` | add/add で衝突（master は `Scheduler.MainThreadIgnoreTimeScale`） | master 側を採り、型名だけ `UiInputExclusivity` へ直す |
| `Client.Input/WebUiInputExclusivity.cs` | modify/delete | 削除を採る（`git rm`）。D-17（受信時刻を `long?` で持ち時刻 0 も受信とする・秒数の算出と印の書き込みを 1 回の lock に収める）を `UiInputExclusivity.cs` へ写す。200 行に収めるため `InputSuppressionScope` を `InputSuppressionFilter.cs` へ移した |
| `MainGameModelRegistration.cs` | コメントだけ衝突（feature はすでに master と同じ位置に登録し `MainGameUiRegistration.cs` には無い） | feature 側を採る。登録は 1 本のまま |
| `Client.Tests/Input/WebUiInputExclusivityWebSilenceTest.cs`（master） | 衝突せずに残り、コンパイルが落ちる | 「時刻 0 の受信も受信として扱う」ケースを `Ui/Input/UiInputExclusivityWebSilenceTest.cs` へ移して削除 |
| `Client.Tests/WebUiHost/Transport/WebSocketMessageReceiptRecordTest.cs`（master の新規） | 衝突せずに入り、`WebUiInputExclusivity` を参照してコンパイルが落ちる | 型名を `UiInputExclusivity` へ直す |
| `Client.Tests/BugReport/Bundle/FixedWebUiHealthProbe.cs`（feature だけ） | master で消えた | 削除する |
| `Client.Tests/BugReport/Bundle/WebUiHealthManifestTest.cs` | add/add で衝突 | master 側を採り、writer の ctor に `new TestBugReportOutboxRoot()` を足す |
| `BugReportBundleWriterIdentityTest.cs`・`DeveloperReportRealReceiverCheck.cs`・`Ui/State/PauseMenuCommandsTestFactory.cs` | 衝突（ctor 行） | master の確保データ（`WebUiHealth = new WebUiHealthReading()`）を採り、ctor は `(identity, new TestBugReportOutboxRoot())` |
| `Client.Tests/BugReport/Bundle/CrashBundleWebUiHealthTest.cs`（master）と `LastSession/` 版（feature） | 両方残る | feature の `LastSession/` 版を消し、master 版を `TestBugReportOutboxRoot.Directory` の 5 引数へ直す |
| シナリオ `developer-report-interrupt.cs` | 衝突（ctor 行）＋ master が足した待ちの行が `WebUiInputExclusivity` を参照 | master 側を採り、ctor に `new GameSystemBugReportOutboxRoot()` を足し、型名を `UiInputExclusivity` へ直す |
| `localizationKeys.ts`・`Client.Localization/_CompileRequester.cs` | 生成物の衝突 | `node scripts/generate-localization-keys.mjs` で再生成。印は feature 側を採る |
| `scripts/playtest/dashboard/*`・ADR 0057 | master だけが変える | そのまま入る |

取り込み後の機械検査（取り込みの PR のタスクに入れる）:
- `git grep -n "WebUiInputExclusivity" -- moorestech_client .agents` が、過去 PR のレビュー fixture（`.agents/skills/moores-code-review/tests/s5_shape/fixtures/pr-1457-*.md`。文書で、取り込み前から feature にある）を除いて 0 件
- `git grep -c "RegisterEntryPoint<WebUiSilenceWatcher>" -- moorestech_client` の合計が 1
- `git grep -n "class CrashBundleWebUiHealthTest\|class .*WebSilenceTest" -- moorestech_client` が各 1 件
- `UiInputExclusivityWebSilenceTest` のケース数が取り込み前の feature 版より 1 件多く（時刻 0 のケース）、`git grep -n "long? \|long?>" -- moorestech_client/Assets/Scripts/Client.Input/UiInputExclusivity.cs` で最後の受信時刻が `long?` である
- `uloop compile` が通り、§5 のフィルタのテストが緑
- 台帳 `docs/uitk/TODO.md` の追従記録: `check-uitk-web-follow.py --base origin/feature/uitk --head HEAD` で未記帳 0 件（I-weekly-1 では #1582 の `localizationKeys.ts` 再生成 1 件を記帳した）

切替の日（撤去 5a）: `WebUiHealth/`・`Health/`・`WebUiSilenceWatcher`・`EvaluateWebSilence` 系・manifest の `webUiHealth` が一緒に消える（plan §3.11）。manifest から欄を消すときは v7 へ上げる（agent 既定。ADR 0057 の契約変更の作法）。

## 7. リスクと未確認

- **LogError への格上げの副作用（未確認）**: `WebUiStartup` の `!started` と例外を LogError にすると、Web ホストが起動しない環境（CI の batchmode や、node の無い Editor）で、Error ログ 0 を条件にする検査やテストが赤くなるおそれがある。master には同じ関数の中にすでに LogError の経路がある（`WebUiHost.cs:66` の production artifact rejected）ので新しい種類の失敗ではない、と読める（推測）。計画には「worktree で EditModeInPlayingTest 一式と run_test.yml 相当を流して、LogError の増減を見る」タスクを入れる
- **CEF のタイマー間引き（推測）**: Chromium は非表示のページで `setInterval` を間引くことがある。オフスクリーン描画の CEF でゲームウィンドウがフォーカスを失ったとき、ping 間隔が 15 秒を超えると、誤って「無応答」の印が立つおそれがある。印が立っても Web 側の抑止値を無視するだけで、Esc と移動が効くようになる方向（fail-open）なので、害は小さい。LogWarning が出るので気づける。実測していない
- `CefProcessState` は「生死」ではなく「観測を始め、喪失通知が来ていない」ことを表す（D-3 の改名理由）。plan §3.11 の「CEF プロセスの生存」という言い方は manifest の値とずれているので、計画の文言を `observedAfterBrowserStart` に合わせる
- §3.3 の読み手の対応（ダッシュボードへの表示）は agent 既定で、裁定は無い。表示しない選択肢もある（manifest.json を直接読めば足りる）
- 取り込み時の衝突の形（§6 の 2 列目）は I-weekly-1 の実際の `git merge` で実測した値に置き換えた

## 8. 計画への反映案（レーンの形）

- レーン名の案: `fix/cef-stop-record`（master から切る）。最後は pr-create で master へ PR を出し、マージはユーザーが行う（裁定）
- Task 列の案: (1) 新規ファイルを feature/uitk の blob で取り込み、master 用の 3 ファイルを置き換える → (2) `WebUiInputExclusivity` の無応答と L0 → (3) manifest v6・writer・crash の箱と L0・既存テストの ctor → (4) 起動の記録・LogError・CEF の観測・DI 登録 → (5) ダッシュボードの表示と ADR 0057 → (6) compile・テスト・unityプレイ録画テスト（§5）・LogError 副作用の確認 → (7) moores-code-review → (8) pr-create
- 後続: master にマージされた後の最初の master 取り込みで §6 の表を適用する（albof.20 と同じ PR でもよい）
