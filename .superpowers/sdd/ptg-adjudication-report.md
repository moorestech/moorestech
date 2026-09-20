# タイトル開始ゲート レビュー裁定の反映報告（run 2026-09-20-0250 / D-C1・C2・C3・C4・C8）

ブランチ `feature/playtest-title-gates`、起点 `e29ffb0f4`。裁定はすべて design.md の推奨（案A）。
`.decisions/2026-09-20-タイトル開始ゲートのレビュー保留7件は全て推奨案Aで直す.md` に従う。D2（TextMeshProLocalize）と D3（WebUI の useFrontmostStartGate 撤去）は別担当のため未着手。

## D-C4: 検証済み識別の寿命と型（commit bdfb3996c）

- `PlaytestGateResult.Allowed(session, verifiedSteamId)` と `TryGetVerifiedSteamId(out string)` を追加。検証済み SteamID は Allowed の結末だけが持つ。
- `PlaytestSession.VerifiedSteamId` の可変プロパティを削除し、`PlaytestSessionResult.SteamId` へ移した。`PlaytestGateDecision.Decide` は `PlaytestSessionResult` を受け取る形に変えた（outcome/detail の2引数を畳んだ）。
- 識別の設定・解除を `PlaytestLaunchGate.SetCurrent` の1箇所へ寄せた。Allowed なら `ReceiverVerifiedSessionIdentity`、それ以外（NotEvaluated・Checking・DeveloperMode・Blocked）は `EmptyPlaytestSessionIdentity` を据える。
- 触ったファイル: `Client.PlaytestReceiver/PlaytestSession.cs`・`Gate/PlaytestGateResult.cs`・`Gate/PlaytestGateDecision.cs`・`Gate/PlaytestLaunchGate.cs`・`Client.Tests/PlaytestReceiver/PlaytestSessionTest.cs`・`Gate/PlaytestGateDecisionTest.cs`・`Gate/PlaytestLaunchGateIdentityTest.cs`・`Upload/PlaytestUploadRunnerTest.cs`
- テスト: `PlaytestLaunchGateIdentityTest` の手動リセット（SetUp/TearDown の `SetCurrent(new EmptyPlaytestSessionIdentity())`）を外し、「許可された後に開発者モードへ移ると識別は空へ戻る」「許可された後に不許可へ移ると識別は空へ戻る」を追加。

## D-C2: 再試行が失敗した試行と同じセッション名を使う（commit d59552a66）

- `PreviousSessionStartupTasks.RunAtStartup` の先頭で `ProcessSessionScope.BeginNewSession()` を無条件に呼ぶ。タイトル経路は `SalvageAtTitle` が退避の前に自分で呼ぶ。退避自体はプロセス1回のまま（ADR 0060 裁定5を字義どおり維持）。
- 実装上の判断: design は「Salvage() も退避前に自前で呼ぶ」だが、素直に書くと直接起動経路で BeginNewSession が2連続になるため、共通の `Salvage()` からは外して呼び手（RunAtStartup の先頭・SalvageAtTitle）2箇所に置いた。挙動は裁定どおり。
- 触ったファイル: `Client.Starter/Playtest/PreviousSessionStartupTasks.cs`・（新規）`Client.Tests/Playtest/PreviousSessionStartupTasksTest.cs`
- テスト: 「再試行のたびに別のセッション名になり失敗した試行の印は正常終了へ畳まれない」（CleanExitMarker で実証）と「RunAtStartup は退避の判断より前にセッション名を更新する」（MethodCallInspector で呼び出し順を固定）。

## D-C3: タイトルの退避が今回起動の既定値を仮定する（commit 0ec2b479c）

- `SessionSnapshotSource`（接続種別＋実ワールドのスナップショットディレクトリ）を新設し、`SessionOriginSnapshot` に載せた。`CleanExitMarkWriter.InstallAtStartup` がセッション開始時に書き残す。
- タイトル／直接起動どちらの退避も、退避元は前回セッション自身の印から決める。`PreviousSessionSalvageRequest` から `IsRemoteConnection` と `WorldSnapshotDirectory` を外し、`UncleanSessionSalvage` が最新の異常終了セッションの `Origin.SnapshotSource` を読む。
- 記録の無い旧版の印は「スナップショット源不明: 前回セッションの印に退避元（接続種別・ワールド）の記録が無い」として missing に明記する（既定ワールドへ落とさない）。
- 実装上の判断: design の「実ワールドディレクトリ」は、解決を走っているセッション側に閉じるため**スナップショットディレクトリ**として記録した（`WorldDataDirectory.FromWorldRoot(...)` の変換は Client.Starter 側に残る）。
- 触ったファイル: `Client.Game/InGame/BugReport/LastSession/Marks/SessionSnapshotSource.cs`（新規）・`Marks/SessionOriginSnapshot.cs`・`Marks/CleanExitMarkWriter.cs`・`Salvage/PreviousSessionSalvage.cs`・`Salvage/PreviousSessionSalvageRequest.cs`・`Salvage/UncleanSessionSalvage.cs`・`Client.Starter/Playtest/PreviousSessionStartupTasks.cs`・関連テスト4本
- テスト: 「退避元は前回セッションが記録したワールドから決まる」「退避元の記録が無い前回セッションはスナップショット源不明として表明する」を追加。`CleanExitMarkerTest` で退避元の読み戻しを確認。

## D-C1 / D-C8: 状態の持ち主の集約と開始の関所の一本化（commit 0e1fdccbd）

D-C1:
- 段階は `PlaytestTitleGateSequence` が自分の `ReactiveProperty` で持つ。`PlaytestTitleGates` は現行の列 `_current` だけを保持し、列が無いことが「未開始」を表す。static な `StepProperty`・`Step`・`SetStep` は削除。
- `Begin` を `TryBegin(verdict, uploadRequester, out sequence)` に変え、「始動済みなら既存の列を返す」をここ1箇所へ集約。`PlaytestLaunchGateView` の先行判定（`Step != NotStarted`）を削除し、再訪でも同じ列にポップアップを繋ぎ直して未応答の確認を出す。
- `PreviousSessionStartupTasks.SalvageAtTitle` は退避済みなら throw せず `PreviousSessionSalvage.RequireArtifacts()` を返す（「退避済みか」の持ち主は `_salvagedThisBoot` 1つ）。
- 待ちの寿命を `Application.exitCancellationToken` へ揃えた。タイトル破棄では打ち切らないので、打ち切りはアプリ終了だけになり、`打ち切られた後の了解では通過しない` テストの主張はそのまま成立する（コメントで前提を書き直した）。

D-C8:
- `PlaytestTitleGates.TryPassStart(callerName, out denyReasonText)` が照合（`PlaytestLaunchGate.TryPassStart`）→ タイトルのゲートの順序を内部に閉じる。`StartLocal`・`ConnectServer`・`InitializeScenePipeline` の漏斗はこれを1回だけ呼ぶ。
- 未開始の拒否は新規キー `ui.playtest.gate.notStarted` の文言を返す。Consent/CrashReport は文言を空にし（確認が画面に出ているため）ログだけ残す。呼び手は空でなければ表示する。
- タイトルを通らない起動の明示通過 `MarkPassedForDirectBoot(reason)` は、`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` で「起動シーンが MainMenu でない」ことを見て宣言する。
  - **design からの逸脱と理由**: design は `RunAtStartup` の直接起動分岐に置くとしていたが、`RunAtStartup` は `InitializeScenePipeline` の漏斗より後に走るため、そこで印を立てても漏斗に間に合わず直接起動が恒久拒否になる。漏斗より前に必ず走る起動フックへ移した。
- 出展モードの自動開始は、新しい迂回 API を足さず既存の `PlaytestStartGateBypass.DeclareUnattendedProcess("eventModeAutoStart")` で無人宣言する。タイトルのゲートは閉じた状態で組まれて即 Passed になるので、確認は出ず（D2 裁定 A）未応答の印は残る。`AfterSceneLoad` は MainMenu の `Start()` より前に走るため、View がゲートを開いてしまう競合は起きない。
- `PlaytestLaunchGate` と `ConnectServer` の「唯一の関所」コメントを実態（2段は PlaytestTitleGates に閉じる）へ書き換えた。
- plan `docs/superpowers/plans/2026-09-20-playtest-identity-and-title-start-gates.md` の機能の死活表 D1/D2 の「暫定A・要ユーザー裁定」を、2026-09-20 ユーザー裁定で確定した旨へ書き換えた。あわせて待ちの寿命の行も更新。
- 触ったファイル: `Client.Starter/Playtest/TitleGates/PlaytestTitleGates.cs`・`TitleGates/PlaytestTitleGateSequence.cs`・`Client.Starter/Playtest/PreviousSessionStartupTasks.cs`・`Client.Starter/InitializeScenePipeline.cs`・`Client.Starter/EventMode/EventModeAutoStart.cs`・`Client.MainMenu/Playtest/PlaytestLaunchGateView.cs`・`Client.MainMenu/StartLocal.cs`・`Client.MainMenu/ConnectServer.cs`・`Localization/localization.csv`・`Client.Localization/_CompileRequester.cs`・`Client.Tests/Playtest/TitleGates/PlaytestTitleGatesTest.cs`・`PlaytestTitleGateSequenceTest.cs`
- テスト: 「通過するまで開始を断り通過したら通す」を文言の有無まで見る形に書き換え（未開始はテスター向け文言あり・確認中は空）、「タイトルを通らない起動は明示通過で開始できる」＋その後タイトルへ戻ると未応答の確認で止まることを追加。ほかのケースは `Compose` で組んだ列を現行として据えて進める形へ移した。

## 実行したコマンドと結果

```
uloop compile --project-path ./moorestech_client                      # ErrorCount 0 / WarningCount 0（各件ごとに実施）
uloop run-tests --filter-type regex --filter-value "Client\.Tests\.PlaytestReceiver\..*"            # 146 passed / 0 failed
uloop run-tests --filter-type regex --filter-value "Client\.Tests\.Playtest\.PreviousSessionStartupTasksTest\..*"  # 2 passed / 0 failed
uloop run-tests --filter-type regex --filter-value "Client\.Tests\.BugReport\..*"                   # 147 passed / 0 failed
uloop run-tests --filter-type regex --filter-value "Client\.Tests\.(Playtest|BugReport|PlaytestReceiver|PlaytestSmoke|Localization|EventMode)\..*"  # 528 passed / 0 failed（最終）
uloop run-tests --filter-type regex --filter-value ".*PlaytestReportAndProgressTest.*"              # 1 passed（PlayMode 遷移で CLI は切断するため TestResults.xml で確認）
```

localization.csv を足したので、`moorestech/Check Schema Changes` を実行して `Client.Localization/_CompileRequester.cs` を更新し、`LocalizationKeys.Ui.Playtest.Gate.NotStarted` を再生成した（この印もコミットに含む）。

## 残した懸念

- `PlaytestTitleGates` は「現行の列」と「タイトルを通らない起動の通過理由」の2つの静的状態を持つ。優先順位は「列があればそれが正本」で、直接起動の後にタイトルへ戻ると確認が出る（D-C1 の要求）ようにしてある。1つに畳まなかったのは、直接起動の通過を列で表すと再訪時に確認を出せなくなるため。
- `RunAtStartup` の先頭で必ず `BeginNewSession` することは、IL の呼び出し順テスト（`PlaytestStartGateBypass.UnattendedReason` より前）でしか固定できていない。「無条件」であること自体は実挙動テストでは押さえていない（`RunAtStartup` が実物の `GameSystemPaths` を破壊的に触るため、EditMode で丸ごと回すのを避けた）。
- 退避元の記録は前回セッションの**パス文字列**なので、ワールドを移動・削除した後の起動では欠損として表明される（黙って別ワールドを退避するよりは良いが、理由文は「移動できない」系になる）。
- 出展モードを `DeclareUnattendedProcess` で無人宣言したため、出展機でタイトルへ戻って手動で遊ぶ場合もそのプロセスの間はゲートが閉じたままになる。出展機の運用としては意図どおりだが、挙動としては記録しておく。
- PlayMode 遷移を伴う `PlaytestReportAndProgressTest` 以外の EditModeInPlayingTest 群、および WebUI 側の e2e は今回回していない（D3 の担当範囲と重なるため）。

---

# タイトル開始ゲート レビュー裁定の反映報告（run 2026-09-20-0250 / D2・D3）

D-C1〜C4・C8 の担当（起点 `f3d099bdf`）に続く、Warning 由来の設計判断2件。裁定は design.md の推奨（案A）。

## 前提の修繕: 生成済みLキーの鮮度（commit fd57c9c89）

D-C8 で `Localization/localization.csv` へ足した `ui.playtest.gate.notStarted` が webui 側の生成物へ反映されておらず、
`localizationKeysFreshness` テストが `f3d099bdf` の時点で赤だった。`npm run gen:i18n` で
`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` を更新した（D2/D3 とは独立の修繕なので別コミット）。

## D3: WebUI に残った開始ゲート調停一式（commit 1b95817bf）

ゲートが出展モードの言語選択1枚になった後の姿へ畳んだ。C#・TS・mock の wire 契約を同時に変更している。

- WebUI: `src/app/startGates/`（`useFrontmostStartGate`・`pickFrontmostStartGate` とそのテスト）を削除。
  `App.tsx` が `useTopicSelector(Topics.eventLanguageGate, (d) => d?.waiting === true)` を直接購読し `visible` へ渡す。
- wire 契約から `precedence` を外し、`EventLanguageGateDataSchema = z.object({ waiting: z.boolean() })` にした。
  契約テストは `startGateContract.test.ts` → `eventLanguageGateContract.test.ts` へ改名し、「precedence 欠落を拒否する」主張を
  「waiting だけを受理し欠損・型違いを拒否する」へ差し替えた。
- C#: `Client.WebUiHost/Game/StartGates/`（`IStartGateWaitState`・`StartGateTopics`・`WaitingGateTopic`）を丸ごと削除し、
  `Game/EventMode/EventLanguageGateTopic.cs` へ畳んだ（`TopicName` 定数を自分で持ち、payload は `{ waiting }` のみ）。
  `EventLanguageGate` はインターフェース実装を落とし、`EventLanguageGateBinder` は `EventLanguageGateTopic.Register(hub, gate)` を呼ぶ。
  この形は元々 plan `2026-08-28-event-mode-language-select-gate.md` が書いていた姿で、そこへ戻したことになる。
- `GATE_ALREADY_ANSWERED_ERRORS`（1エントリの表）を `EVENT_LANGUAGE_ALREADY_SELECTED` 定数へ畳み、`GateAnswerActionType` を削除した。
- `shared/ui/FullScreenGate`（外殻・`useGateAnswer`・style）を `features/eventLanguageGate/FullScreenGate/` へ移し、
  `shared/ui` の公開barrelから外した。受益者が1つになったため。
  - **design からの逸脱と理由**: `useGateAnswer` は action type の総称型を取る形だったが、抑止コード表が1本の定数になった時点で
    総称が支える対象が消える。名前を実処理に合わせて `useLanguageSelectionAnswer` へ改名し、action type は
    ファイル内の `AnswerActionType` 定数に固定した（design は改名まで指示していないが、`GATE_ALREADY_ANSWERED_ERRORS` 撤去の帰結）。
- mock-host: `fixtures/startGateFixtures.ts`（`StartGatePrecedence`）を削除し、`topicControls`・`topicFixtures` の payload を `{ waiting }` だけにした。
- `.agents/skills/webui-design/SKILL.md` §8.20 を新しい姿（購読は App.tsx 直・`precedence` 撤去・配置は features 配下）へ書き換えた。

## D2: タイトル uGUI の文言配線（commit aed5b4426）

静的ラベルを前例（`TextMeshProLocalize` + Inspector のキー）へ移した。目的は表示中の言語切替への追従と、全数検査テストの対象化。

- `MainMenu.unity` の以下8点へ `TextMeshProLocalize` を付け、キーを入れた（**すべて `uloop execute-dynamic-code` 経由**。手編集はしていない）:
  `PlaytestConsentPopup/Panel` の Title・Message・AgreeButton/Text、
  `CrashReportPopup/Panel` の Title・Message・Description/Text Area/Placeholder・SendButton/Text・SkipButton/Text。
- `PlaytestConsentPopup` から `titleText`/`bodyText`/`agreeButtonText` を、`CrashReportPopup` から
  `titleText`/`bodyText`/`descriptionPlaceholderText`/`sendButtonText`/`skipButtonText` を削除し、`SetVisible` の流し込みも消した。
  C# に残る文言は `CrashReportPopup.statusText`（応答の結末で変わる1行。`ui.playtest.gate.respondFailed` と空文字）だけ。
- `SetVisible` は「出すときに前回の結末を消す」責務だけ残した（残ると押していない応答の失敗を読ませてしまうため。理由をコメントに書いた）。
- `ExceptionSceneLocalizedTextTest` に「タイトルのプレイテスト確認の静的ラベルが翻訳キーへ配線されている」を追加し、
  8点のパスとキーを固定した。既存の全数検査（キーがバニラ辞書に実在すること）も自動でこの8点を見るようになる。
  - `Client.Tests` は `Client.MainMenu` アセンブリを参照していないため、ポップアップの型ではなくシーン内パスで指定している。

## 実行したコマンドと結果

```
uloop compile --project-path ./moorestech_client                                   # ErrorCount 0（D3後・D2後の各回）
uloop run-tests --filter-type regex --filter-value "Client\.Tests\.(EventMode|WebUi)\..*"        # 223 passed / 0 failed
uloop run-tests --filter-type regex --filter-value "Client\.Tests\.Localization\..*"             # 124 passed / 0 failed
uloop run-tests --filter-type regex \
  --filter-value "Client\.Tests\.(Playtest|BugReport|PlaytestReceiver|PlaytestSmoke|Localization|EventMode|WebUi)\..*"  # 726 passed / 0 failed（最終）
moorestech_web/webui: npx tsc --noEmit -p tsconfig.json                            # エラー0
moorestech_web/webui: npx tsc -p e2e/tsconfig.json --noEmit                        # エラー0
moorestech_web/webui: npm run lint                                                  # 指摘0
moorestech_web/webui: npm run test                                                  # 127 files / 974 passed（gen:i18n 前は鮮度1件赤）
```

## 残した懸念

- webui e2e は回していない。`eventLanguage`/`language-gate` を触る spec が存在せず（mock-host の topic fixture のみ）、
  その fixture は型チェックが通っている。merge-base `b7b990251` 時点の既存32件との突き合わせは未実施。
- `TextMeshProLocalize` は `Awake` で1回引き、以後 `OnLanguageChanged` で追従する。ポップアップは初期非アクティブなので
  `Awake` は初回表示時に走る。`Description/Text Area/Placeholder` は入力があると `TMP_InputField` が非アクティブにするが、
  購読は `AddTo(this)`（コンポーネント破棄まで）なので言語切替は取りこぼさない。実機での目視確認はしていない。
- `TextMeshProLocalize` が引くのは `Localize.GetLegacy(string)` で、型付きキー（`LocalizationKeys.*`）の恩恵は無い。
  キーの実在は `ExceptionSceneLocalizedTextTest` の全数検査が担保するが、これは MainMenu シーンの前例どおりで本 PR で変えていない。
- 新テストはシーン内パス（`Canvas/CrashReportPopup/Panel/Title` 等）で指定しているため、GameObject 名の変更で落ちる。
  落ちたときのメッセージにパスを出しているので気づけるが、名前変更時はテストも直す必要がある。
