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

## 再レビュー Warning の処置（post-check: applied-diff-correctness / refix w7-round1）

対象は `moorestech_logs/harness/moores-code-review/runs/2026-09-20-0250/agents/refix-correctness-w7-r1.md` の Warning 6件。
各件とも該当コードを読み直して成立可否を判定し、成立するものだけ直した。裁定（2026-09-20 推奨案A・D1/D2 確定）は覆していない。

### W1 `PlaytestGateDecision` が空の検証済みSteamIDで Allowed を組みうる — 成立（直した）

`PlaytestSession.EnsureTokenAsync` のキャッシュ短絡は `Outcome=Allowed・SteamId=null` を返す。現状 `Decide` の呼び手は
`EvaluateAsync`（毎回新しい `PlaytestSession`）1本なので実害は無いが、不変条件がコメントだけで守られていないのは事実。
`Decide` で `Allowed` かつ SteamID が空なら `Debug.LogError` を出して `Blocked(Unreachable)` へ倒す（MalformedResponse と同じ落ち先）。
テスト: `PlaytestGateDecisionTest.検証済みSteamIDの無いAllowedは契約違反として止める`（null と空文字の両方）。

### W2 出展モードの自動開始が漏斗1段目に間に合わず配布版の出展機が固着 — 成立（直した・配布経路の実害）

`AutoStartIfEventMode` は `AfterSceneLoad` で、MainMenu の `Start`（＝`PlaytestLaunchGateView` の `EvaluateAsync`）より前に
`LoadScene(GameInitializer)` を呼んでいた。配布版（build-info.json あり）＋Steam 稼働では照合が `NotEvaluated` のまま漏斗へ届き、
`PlaytestTitleGates.TryPassStart` の1段目（`PlaytestLaunchGate`）が拒否して `LoadScene(MainMenu)` へ戻す。
`RuntimeInitializeOnLoadMethod` は二度と発火しないため、出展機はタイトルで固着する（無人宣言は2段目にしか効かない）。
直し: `PlaytestLaunchGate.Current` を1回だけ購読し、`NotEvaluated`/`Checking` の間は待ち、確定してから `StartLocalGame`。
照合に止められた場合は自動開始せず `Debug.LogError` で理由を残す（fail-closed）。同意・異常終了確認を出さない D2 裁定はそのまま
（`DeclareUnattendedProcess` は従来どおり最初に宣言する）。判定は純関数 `DecideAutoStart` に切り出した。
テスト: `EventModeAutoStartVerdictTest`（待ち・開始・断念の3件）。

### W3 再訪で新しい `_uploadRequester` が捨てられる — 前半は成立（直した）／後半（ポップアップ重なり）は不成立

前半: 列はプロセス寿命、送り手はタイトル（合成ルート）寿命という寿命の食い違いは実在する。`TryBegin` が既存の列を返すときに
`PlaytestTitleGateSequence.SetUploadRequester` で今回のタイトルが組んだ送り手へ繋ぎ直すようにした（`_uploadRequester` の readonly を外した）。
テスト: `PlaytestTitleGatesTest.再訪のタイトルが組んだ送り手へ繋ぎ直す`。

後半（「まだ開いている同意／異常終了確認ポップアップの上に待ち文言が再表示される」）は、当初この節に書いた棄却根拠
（「`Allowed` の直前に `messagePopup.gameObject.SetActive(false)` が走るので同時に出る瞬間は無い」）が実コードと食い違っていた。
再訪の `Start()` は `Subscribe(Show)` で前回の確定値を同期再送され、非 Blocked なら `BeginTitleGates` が確認ポップアップを出し、
**その直後に** `EvaluateAsync` が同期で `SetCurrent(Checking)` を置く。`Show(Checking)` は `IsBlocked` 側なので `messagePopup.SetText`
（＝`SetActive(true)`）へ入る。棄却の正しい理由は別で、**配布版は起動シーンが必ず MainMenu のため `_directBootPassReason` 経路が無く、
`InitializeScenePipeline` の漏斗へ到達できた＝段階は既に `Passed`** であり、現状の呼び出し集合では「未応答の確認が残る再訪」と
「`RequiresCheck == true`」が両立しない、という点にある（再レビュー2周目 W2 で訂正）。

### W4 `Checking` の間に検証済みSteamIDが無音で消える — 成立（ログで直した）

`SetCurrent` の解除を `DeveloperMode`/`Blocked` 確定時だけに絞ると「再評価中は前回の識別が読める」ことになり、D-C4 案A
（Allowed 以外は必ず空へ戻す）と衝突する。よって解除は据え置き、載っていた識別を消すときだけ `status` 付きで `Debug.Log` を出す
`ClearIdentity` を足した（無音の縮退を解消する側で対処）。あわせて Info-1 の
`PlaytestLaunchGateCheckingTest` の直接 `PlaytestSessionIdentityProvider.SetCurrent` を削り、解除の窓口を `SetCurrent` 1箇所に揃えた。

### W5 新テストが実ユーザーの印を書き TearDown が無い — 成立（直した）

`PreviousSessionStartupTasksTest` に `[TearDown]` を足し、`last-session/marks/pid_424242/` をディレクトリごと削除する。
アサート失敗時も必ず走るため、開発機の次回起動が「前回異常終了」として退避・確認する事故は起きない。
`ProcessSessionScope.BeginNewSession()` の共有 static 前進については、当初「`CurrentSessionName` は未開始なら1度だけ始める契約なので
復元不要」と棄却したが、これは getter の契約であって「既に開始済みの名前を進めてよい」の根拠にならない（再レビュー2周目 W4 で訂正・後述のとおり復元した）。

### W6 複数異常終了でスナップショットの見送りが無音 — 成立（直した）

`MoveUncleanRecordings` は全異常終了セッションの録画を移すのに、`MoveWorldSnapshots` は最新1件の出所からしか移さない。
`SelectLatestUncleanSession` の `1 < Count` 分岐に `missing.Report(snapshots, …)` を足し、
「最新の pid/セッションの出所だけを退避した・残り N 件のワールドは見送り」を欠損として表明する。
テスト: `UncleanSessionSalvageSelectionTest.複数の異常終了があればスナップショットを見送ったセッションも欠損として表明する`
（テストファイル数の上限のため `Client.Tests/BugReport/LastSession/Salvage/` を新設した）。

### 実行したコマンドと結果（再レビュー対応分）

```
uloop compile --project-path ./moorestech_client                                                  # ErrorCount 0
uloop run-tests --filter-type regex \
  --filter-value "Client\.Tests\.(Playtest|BugReport|PlaytestReceiver|PlaytestSmoke|Localization|EventMode)\..*"   # 535 passed / 0 failed
```

## 再レビュー2周目 Warning の処置（4件・全て直した）

### W1 出展モード自動開始の待ちに期限が無い — 成立（直した）

`EventModeAutoStart` の確定待ちを、前例 `StandalonePlaytestSmokeBootstrap.StartWhenPreconditionsHoldAsync` と同じ
「期限付きの UniTask 待ち＋超過時に理由を残す」形へ寄せた。`LaunchVerdictTimeoutSeconds = 180f`（前例と同値）を置き、
`DecideAutoStartWithinDeadline(verdict, secondsWaited, timeoutSeconds)` が期限超過の未確定を `Abandon` へ倒す（fail-closed）。
超過時は `Debug.LogError` で「起動時照合が180秒以内に確定しなかったため自動開始しません status:… detail:…」を残し、自動開始しない。
`Forget` には例外ハンドラを付け、待ちが例外で終わった場合も無音にしない。
テスト: `EventModeAutoStartVerdictTest.期限内は確定を待ち期限を過ぎたら自動開始を断念する` / `確定済みの結論は期限の影響を受けない`。

### W2 タイトル再訪で確認ポップアップの上に待ち文言が重なる — 成立（ガードを置いた・棄却根拠も訂正）

`PlaytestLaunchGateView.Show` の `Checking`（`IsBlocked`）分岐に、答え待ちの確認が画面に出ている間は `messagePopup` を出さない
ガードを置いた（抑止したことは `Debug.Log` に残す＝無音の縮退にしない）。判定は列が持つ
`PlaytestTitleGateSequence.IsShowingConfirmation()`（段階が `Consent` か `CrashReport`）。恒久的な拒否（`NotAllowed` 等）は
テスターが読めないと困るので抑止せず、抑止対象は再照合中の待ち文言だけに限っている。
上記 W3 節の棄却根拠も、レビュアーが示した正しい理由（配布版は起動シーンが必ず MainMenu なので漏斗到達時点で段階は `Passed`）へ書き直した。
テスト: `PlaytestTitleGatesTest.答え待ちの確認がある間だけ確認を表示中と答える`。

### W3 見送りの欠損表明が誤報になる — 成立（直した）

`UncleanSessionSalvage.SelectLatestUncleanSession` の「残り N 件のワールドは見送り」は、同じワールドで複数回落ちた場合
（ローカルプレイの既定ワールドは固定なのでこれが多数派）に実際には見送りが起きていないのに誤報していた。
判定を「最新以外のうち、`Origin.SnapshotSource.WorldSnapshotDirectory` が最新のそれと異なる件数」へ絞り、0 件なら表明しない。
リモート接続のセッションは退避すべき盤面がそもそも無いので数えず、出所不明は「同じワールドだった」と言い切れないので取り残した側へ倒す。
テスト: `UncleanSessionSalvageSelectionTest.同じワールドで複数回落ちていれば見送りは表明しない`（別ワールドの既存テストはそのまま通る）。

### W4 テストが共有 static のセッション名を前へ進める — 成立（安全側へ倒した）

`PreviousSessionStartupTasksTest` に `[SetUp]` を足し、`ProcessSessionScope._currentSessionName` をリフレクションで退避して
`[TearDown]` で戻す（getter を通すと未開始の状態まで開始してしまうため、フィールドを直接読んで null は null のまま保存する）。
テスト専用の口をプロダクションへ足さずに、先行書き手のセッション名へ干渉しない形にした。
棄却根拠（「未開始なら1度だけ始める契約」）は getter の契約であって名前を進めてよい根拠ではない、というレビュアーの指摘どおりで、
上の W5 節の記述も訂正済み。

### 実行したコマンドと結果（再レビュー2周目対応分）

```
uloop compile --project-path ./moorestech_client                                                  # ErrorCount 0 / WarningCount 0
uloop run-tests --project-path ./moorestech_client --filter-type regex \
  --filter-value "Client\.Tests\.(Playtest|BugReport|PlaytestReceiver|PlaytestSmoke|Localization|EventMode)\..*"   # 539 passed / 0 failed
```
