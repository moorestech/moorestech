# プレイテストの識別を受け口の検証済みSteamIDで確定し、同意と前回異常終了の確認をタイトルへ移す Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。
>
> **D1・D2 は案 A で確定済み:** 本文「## 機能の死活表」の行 D1・D2 は、2026-09-20 のユーザー裁定で推奨案 A（Editor の直接再生と出展モードの自動開始では同意・異常終了確認を出さない。未応答の印は残り、次にタイトルを通る起動で聞き直す）に確定した（`.decisions/2026-09-20-タイトル開始ゲートのレビュー保留7件は全て推奨案Aで直す.md`）。plan はこの前提のまま進める。

**Goal:** 起動時照合で受け口が返した検証済みSteamIDを報告・進行記録・異常終了箱の識別に入れ、参加同意と前回異常終了の確認を Play locally 後の WebUI からタイトル（uGUI）へ移して WebUI 側のゲート一式を撤去する。

**Architecture:** `PlaytestSessionResponse` が `steamId` を必須で読み、`PlaytestLaunchGate` が `Allowed` を配る直前に `PlaytestSessionIdentityProvider.SetCurrent` で検証済みSteamIDを据える。タイトルでは照合が通った直後に `PlaytestTitleGates.Begin` が前回セッションの退避と印の消費を行い、`PlaytestTitleGateSequence` が「同意 → 前回異常終了の確認」を順に進めて段階（`PlaytestTitleGateStep`）を配る。uGUI のポップアップ（`Client.MainMenu/Playtest`）は段階を映して応答を渡すだけで、Play locally とサーバー接続は `PlaytestTitleGates.TryPassStart` が `Passed` を返すまで開始しない。Play locally 後のワールド初期化からは開始ゲートを外し、正常終了の書き手の設置と進行記録の回収だけをパイプライン先頭に残す。

**Tech Stack:** Unity C#（UniTask, UniRx, uGUI, TextMeshPro, NUnit）、React + TypeScript（vitest, eslint, playwright）。

## Requirements

設計ADR: `docs/adr/0065-playtest-identity-from-receiver-and-title-start-gates.md`（裁定の出所は同ADRの出所欄と `.decisions/2026-09-20-*.md`）。

- R1. 起動時照合が `Allowed` になったら、受け口が返した検証済み `steamId` が `PlaytestSessionIdentityProvider.Current.SteamId` に入り、進行記録ヘッダ・バグ報告 manifest・異常終了箱（`SessionOriginSnapshot`）に載る。`steamId` が空や欠落の 200 応答は契約違反（`MalformedResponse`）として扱い `Allowed` にしない。開発者モードでは null のまま。受入: `PlaytestSessionTest` に steamId 欠落・空文字の `MalformedResponse` と `VerifiedSteamId` 保持のテスト、`PlaytestLaunchGateIdentityTest` に Allowed→`"7656"`／NotAllowed・steamId 欠落・開発者モード→null のテストがあり PASS する。識別を読む書き手（`CleanExitMarkWriter.InstallAtStartup`・DI の `IPlaytestSessionIdentity` 解決）はすべて照合通過後のパイプラインで走る（Task 2 の配置で保証）。
- R2. 検証機 smoke に SteamID 検査は足さない。受入: `Client.Starter/PlaytestSmoke/*` と `scripts/playtest/*` に steamId 照合のコードが増えていない（Task 8 のレビューで確認）。
- R3. 参加同意（初回のみ）と前回異常終了の確認（異常終了があったときのみ）をタイトルで出す。順序は「照合 → 同意 → 異常終了確認 → Play locally を受け付ける」。ゲートが済むまで Play locally（とサーバー接続）を押しても開始せず、理由を `LogWarning` に出す。受入: `PlaytestTitleGateSequenceTest` が同意→確認→`Passed` の順序と最小構成（どちらも無し／同意だけ／確認だけ／両方）を固定し、`PlaytestTitleGatesTest` が `Passed` 以外の段階で `TryPassStart` が false を返すことを固定する。Task 6 の unityプレイ録画テストで、未読＋異常終了ありの起動が「同意 → 確認 → Play locally → ゲーム開始」と進み、確認中の Play locally が `[PlaytestTitleGates] StartLocal refused` を出して開始しないことを実機で確かめる。
- R4. タイトルのゲートは uGUI で作る（`PlaytestLaunchGateView` と同型の MainMenu シーン上のポップアップ）。シーン編集は `uloop execute-dynamic-code` 経由だけで行う。受入: `moorestech_client/Assets/Scenes/Game/MainMenu.unity` の差分が Task 4 のスニペット実行で生じたものだけで、テキストエディタでの編集が無い。
- R5. 同意の文言と「保存先ごとに初回1回」の規則は変えない。受入: 文言は既存キー `ui.playtest.consent.*`・`ui.playtest.crashGate.*`・`ui.playtest.gate.respondFailed` だけを使い、`Localization/localization.csv` に差分が無い。既読判定は `PlaytestConsentFlag` のまま。
- R6. 前回から持ち越した未送信の記録は、初回の了解が済むまで送らない。既読なら照合通過の直後に送る。異常終了の箱を書いたらアップロード走行をもう一度要求する。受入: `PlaytestTitleGateSequenceTest` で「未読の間は `RequestUpload` 0 回 → 了解で 1 回」「既読なら開始時に 1 回」「箱を書いたら +1 回」「送らない・書けなかったでは増えない」「開発者モードでは 0 回」を固定。無人起動で未読なら送らないことを `PlaytestTitleGatesTest` で固定。
- R7. Play locally 後のワールド初期化から開始ゲートを外し、WebUI 側のゲート一式（C# の `PlaytestGateBinder`・`PlaytestStartGateHandles`・`Actions/Playtest/*`・`PlaytestStartGates`、React の `features/playtestGate`、bridge 契約、関連テスト）を撤去する。出展モードの言語ゲートは残す。前回セッションの退避と印の消費はタイトルへ前倒しし、ADR 0060 裁定5「1箇所」を保つ（`PreviousSessionStartupTasks` の中の1関数が起動1回に1度だけ走る）。記録を集めるかの判定から WebUiHost 起動の条件を外す。受入: `git grep -n "PlaytestGateBinder\|PlaytestStartGates\b\|playtest.consent_gate\|playtest.crash_report_gate\|playtest.consent.acknowledge\|playtest.crash_report.respond"` がコードから 0 件（docs/adr と本 plan は除く）。`EventLanguageGateTopicTest`・`EventModeStartGateTest`・webui の `EventLanguageGate.test.ts` が PASS。`PlaytestReportAndProgressTest`（直接起動）が PASS し、正常終了マーカーと進行記録が出る。
- R8. 無人起動（テスト・DSL・smoke）ではタイトルのゲートも迂回する。受入: `PlaytestTitleGatesTest` で無人の理由を渡すと開始直後に `Passed` になる。smoke はタイトルを経由する（`StandalonePlaytestSmokeBootstrap` が MainMenu で `DeclareUnattendedProcess("playtestSmoke")` を宣言してから `LocalGameLauncher.StartLocalGame()` を直接呼ぶ）ので、タイトルのゲートは閉じた状態で組まれ、開始は `StartLocal` ボタンを経由しないため止まらない。Task 7 の検証機 smoke の phase1/phase2 が成功する。
- R9. やらないこと: カーソルロックの修正（出展モードの言語ゲート。bd `moorestech-zzns`）、同意文言の変更、メインメニューの作り変え（`moorestech-zohw`）、map.json マイグレーション（`moorestech-pv0j`）、smoke への SteamID 検査の追加、`localization.csv` の未使用キーの整理（残課題として起票のみ）、トークン再発行時の識別の差し替え（識別は起動時照合の1回で確定する）。

## Global Constraints

- 1ファイル 200 行以下（既に 201 行の `InitializeScenePipeline.cs` は本 plan の編集で 200 行以下にする）、1ディレクトリ 10 ファイルまで、`partial` 禁止、`Func<>` 禁止、デフォルト引数禁止、イベント通知は UniRx（`Action` をイベントに使わない）。
- try-catch は外部境界（ディスクIO・外部JSON）に限り、根拠をコメントで明記。本 plan の新規コードは try-finally（解除の保証）だけを使い、catch を足さない。
- コメントは主要セクションに日本語1行→英語1行の対。複数行の解説は「日本語ひとかたまり→英語ひとかたまり」。
- fail-closed の経路（開始の拒否・送信要求の見送り・直接起動での確認省略）は理由を必ずログへ出す。
- `[SerializeField]` は `_` 無しの小文字キャメルケース。初期化メソッドは `Initialize`。単純な getter/setter プロパティは使わない（`{ get; private set; }` と get-only は可）。
- Prefab・シーンはテキスト編集禁止。`uloop execute-dynamic-code` 経由でのみ変更する。`.meta` は手で作らない（Unity が生成したものはコミット可。既存ファイルの移動は `git mv` で `.meta` ごと移す）。
- 同意の正本は ADR 0061（既存コメント中の「ADR 0058」は旧番号。新規コメントでは 0061・0065 を引く）。
- `.cs` を変えたタスクは `uloop compile --project-path ./moorestech_client` で ErrorCount 0 を確認する。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`（v3 の既定は EditMode。`TestResults.xml` はマシン共通パスなので start-time で自分の結果か確かめる）。「Domain Reload in progress」は 45 秒待って再試行。
- webui は `moorestech_web/webui` で `pnpm install --frozen-lockfile`（`cp -Rc` で node_modules を持ち込まない）→ `pnpm exec tsc -b` → `pnpm exec tsc -p e2e/tsconfig.json --noEmit` → `pnpm lint` → `pnpm test` → `pnpm test:e2e`（ポート 5273 は他セッションと共有。失敗 spec が毎回変わったらポート衝突を疑う）。
- コミットは実行セッションの attribution 規定の末尾行を付ける。タスクごとにコミットする。

---

## File Structure

### Client.PlaytestReceiver（識別の受け渡し）

- Modify `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/Responses/PlaytestSessionResponse.cs` — `steamId` を必須で読み、`SteamId` を持つ。
- Modify `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/PlaytestSession.cs` — `VerifiedSteamId { get; private set; }` を Allowed の認証で設定する。
- Create `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Gate/ReceiverVerifiedSessionIdentity.cs` — `IPlaytestSessionIdentity` の実体（受け口の検証済みSteamID）。
- Modify `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Gate/PlaytestLaunchGate.cs` — `Allowed` を配る直前に識別を据える。

### Client.Starter（タイトルのゲートと起動時の退避）

- Modify `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PreviousSessionStartupTasks.cs` — 退避と印の消費を「タイトル（`SalvageAtTitle`）か直接起動のパイプライン（`RunAtStartup`）のどちらか1回」にし、書き手の設置と進行記録の回収をパイプライン側に残す。
- Modify `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PlaytestRecordCollection.cs` — WebUiHost の条件を外す。
- Delete `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PlaytestStartGates.cs`（+ `.meta`）。
- Create `moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/PlaytestTitleGateStep.cs` — 段階の列挙。
- Create `moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/PlaytestTitleGateSequence.cs` — 順序・送信要求の位置・応答の受付。
- Create `moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/PlaytestTitleGates.cs` — static の窓口（1回だけ始める・開始経路の関所・ゲート一式の組み立て）。
- Move `Client.WebUiHost/Game/Playtest/PlaytestConsentGate.cs` → `moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/PlaytestConsentGate.cs`（WebUI の待機通知を外す）。
- Move `Client.WebUiHost/Game/Playtest/CrashReportGate.cs` → `moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/CrashReportGate.cs`（同上）。
- Modify `moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs` — 開始ゲートの待ちと `_isRemoteConnection` を外す。
- Modify `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs` — 判定・退避の呼び出しと Finalizer の引数を更新し、200 行以下にする。

### Client.Game（コメントだけ）

- Modify `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/Marks/CleanExitMarkWriter.cs:9-12` — 設置位置の説明を ADR 0065 の配置に合わせる（コードは変えない）。

### Client.WebUiHost（撤去）

- Delete `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestGateBinder.cs`・`PlaytestStartGateHandles.cs`（+ `.meta`、空になったディレクトリの `.meta`）。
- Delete `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Playtest/CrashReportGateActions.cs`・`PlaytestConsentGateActions.cs`（+ `.meta`、ディレクトリの `.meta`）。
- Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/StartGates/StartGateTopics.cs` — 言語ゲートだけにする。
- Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/StartGates/WaitingGateTopic.cs:9-12` — 「3枚」のコメントを直す。

### Client.MainMenu（Assembly-CSharp。uGUI）

- Modify `moorestech_client/Assets/Scripts/Client.MainMenu/Playtest/PlaytestLaunchGateView.cs` — タイトルの合成ルートとして `PlaytestTitleGates.Begin` を呼び、段階をポップアップへ映す。
- Create `moorestech_client/Assets/Scripts/Client.MainMenu/Playtest/PlaytestConsentPopup.cs` — 同意の表示と了解ボタン。
- Create `moorestech_client/Assets/Scripts/Client.MainMenu/Playtest/CrashReportPopup.cs` — 前回異常終了の確認（説明入力・送る／送らない・書けなかった表示）。
- Modify `moorestech_client/Assets/Scripts/Client.MainMenu/StartLocal.cs`・`ConnectServer.cs` — `PlaytestTitleGates.TryPassStart` を関所に加える。
- Modify `moorestech_client/Assets/Scenes/Game/MainMenu.unity` — `uloop execute-dynamic-code` でポップアップ2枚を追加し、`PlaytestLaunchGate` の参照を配線する。

### テスト（Client.Tests）

- Modify `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestSessionTest.cs`。
- Create `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Gate/PlaytestLaunchGateIdentityTest.cs`。
- Move `Client.Tests/BugReport/Playtest/CrashReportGateTest.cs`・`PlaytestConsentGateTest.cs` → `moorestech_client/Assets/Scripts/Client.Tests/Playtest/TitleGates/`（namespace と using を更新）。
- Create `moorestech_client/Assets/Scripts/Client.Tests/Playtest/TitleGates/PlaytestTitleGateSequenceTest.cs`・`PlaytestTitleGatesTest.cs`。
- Delete `moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/PlaytestStartGatesTest.cs`・`PlaytestGateActionResultMappingTest.cs`（+ `.meta`）。
- Modify `moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventLanguageGateTopicTest.cs:38-46` — 3枚の順序テスト（空行・コメント2行・テスト本体）を削除。
- Modify `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/error_codes.json`・`moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs:134-136` — ゲート専用コードを外す。

### webui（`moorestech_web/webui`）

- Delete `src/features/playtestGate/`（全ファイル）、`e2e/tests/system/playtestGate.spec.ts`。
- Modify `src/app/App.tsx`、`src/app/startGates/useFrontmostStartGate.ts`、`src/app/startGates/pickFrontmostStartGate.test.ts`、`src/app/tokens.css`、`src/bridge/contract/payloadTypes.ts`、`src/bridge/contract/schemas/ui.ts`、`src/bridge/contract/validators.ts`、`src/bridge/contract/startGateContract.test.ts`、`src/bridge/transport/actionContract.ts`、`src/bridge/transport/actions.ts`、`src/bridge/transport/actions.test.ts`、`src/bridge/transport/protocol.ts`、`src/shared/i18n/preDictionaryText.ts`、`src/shared/ui/FullScreenGate/useGateAnswer.test.ts`、`e2e/mock-host/fixtures/startGateFixtures.ts`、`e2e/mock-host/topics/topicControls.ts`、`e2e/mock-host/topics/topicFixtures.ts`。

### 文書

- Modify `.agents/skills/webui-design/SKILL.md`（§8.20・§8.21・§1 の例外列挙・§9）— WebUI の開始ゲートを言語選択1枚に直す。
- Modify `scripts/playtest/README.md:105-107` — 検証機の同意既読化をタイトルで行う手順に直す。

---

## Task 1: 受け口の検証済みSteamIDを識別に据える（Client.PlaytestReceiver）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/Responses/PlaytestSessionResponse.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/PlaytestSession.cs:39-41,110-114`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Gate/ReceiverVerifiedSessionIdentity.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Gate/PlaytestLaunchGate.cs:1-10,43-49`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestSessionTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Gate/PlaytestLaunchGateIdentityTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Gate/PlaytestLaunchGateCheckingTest.cs:39-56`（Allowed を通すようになるので、識別を既定へ戻す後始末を足す）

**Interfaces:**
- Consumes: `IPlaytestSessionIdentity`（`Client.Game.InGame.BugReport.Playtest`、`string SteamId { get; }`）、`PlaytestSessionIdentityProvider.SetCurrent(IPlaytestSessionIdentity)`、既存 asmdef 参照 `Client.PlaytestReceiver → Client.Game`（`Client.PlaytestReceiver.asmdef` の references に `"Client.Game"` あり。依存方向は Receiver → Game で、逆向きは作らない）。
- Produces: `PlaytestSession.VerifiedSteamId : string`（`{ get; private set; }`。Allowed の認証の後だけ読む）、`ReceiverVerifiedSessionIdentity(string steamId) : IPlaytestSessionIdentity`、`PlaytestSessionResponse.SteamId : string`（internal）。

- [ ] **Step 1: `PlaytestSessionTest` に steamId の契約を足す**

`許可されればトークンを保持する` の末尾に1行足す:

```csharp
            Assert.AreEqual("7656", session.VerifiedSteamId, "受け口が検証したSteamIDを保持していない");
```

`形の欠けた200では許可しない` を次に置き換える:

```csharp
        [Test]
        public void 形の欠けた200では許可しない()
        {
            // トークン・期限・steamIdの欠落、空のsteamId、JSONでない本文（キャプティブポータル）はいずれも到達不能と混ぜず契約違反として返す
            // A missing token, expiry or steamId, an empty steamId, or a non-JSON body (captive portal) all come back as a contract breach, not as unreachability
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"steamId\":\"7656\",\"allowed\":true,\"expiresAt\":\"2999-01-01T00:00:00Z\"}"), PlaytestSessionOutcome.MalformedResponse);
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}"), PlaytestSessionOutcome.MalformedResponse);
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"allowed\":true,\"token\":\"tok-1\",\"expiresAt\":\"2999-01-01T00:00:00Z\"}"), PlaytestSessionOutcome.MalformedResponse);
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"steamId\":\"\",\"allowed\":true,\"token\":\"tok-1\",\"expiresAt\":\"2999-01-01T00:00:00Z\"}"), PlaytestSessionOutcome.MalformedResponse);
            AssertOutcome(PlaytestApiResult.Responded(200, "<html>sign in to the wifi</html>"), PlaytestSessionOutcome.MalformedResponse);
        }
```

`allowedが立っていない200では許可しない` の本文に steamId を足す（steamId 欠落の契約違反と区別するため）:

```csharp
            AssertOutcome(PlaytestApiResult.Responded(200, "{\"steamId\":\"7656\",\"allowed\":false,\"token\":\"tok-1\",\"expiresAt\":\"2999-01-01T00:00:00Z\"}"), PlaytestSessionOutcome.NotAllowed);
```

- [ ] **Step 2: `PlaytestLaunchGateIdentityTest` を書く**

```csharp
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.PlaytestReceiver.Gate;
using Client.PlaytestReceiver.Http;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlaytestReceiver
{
    // 照合がAllowedになったときだけ受け口の検証済みSteamIDが識別に入ることを押さえる（ADR 0065）
    // Pins that the receiver's verified SteamID becomes the identity only when the launch check settles as Allowed (ADR 0065)
    public class PlaytestLaunchGateIdentityTest
    {
        private static readonly DateTime Now = new(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);

        private byte[] _originalBuildInfo;
        private byte[] _originalBuildInfoMeta;

        // 実物の build-info.json を置き換えるので元の中身を退避し、識別も既定へ戻してから始める
        // The real build-info.json is replaced, so its content is kept aside and the identity is reset before each test
        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
            var path = GameSystemPaths.BuildInfoFilePath;
            _originalBuildInfo = File.Exists(path) ? File.ReadAllBytes(path) : null;
            _originalBuildInfoMeta = File.Exists(path + ".meta") ? File.ReadAllBytes(path + ".meta") : null;
            PlaytestSessionIdentityProvider.SetCurrent(new EmptyPlaytestSessionIdentity());
        }

        [TearDown]
        public void TearDown()
        {
            Restore(GameSystemPaths.BuildInfoFilePath, _originalBuildInfo);
            Restore(GameSystemPaths.BuildInfoFilePath + ".meta", _originalBuildInfoMeta);
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.NotEvaluated);
            PlaytestSessionIdentityProvider.SetCurrent(new EmptyPlaytestSessionIdentity());
        }

        [Test]
        public void 許可されると受け口の検証済みSteamIDが識別に入る()
        {
            PlaceBuildInfoMarker();
            Evaluate(PlaytestApiResult.Responded(200, PlaytestSessionBodies.AllowedFarFuture));

            Assert.AreEqual(PlaytestGateStatus.Allowed, PlaytestLaunchGate.Current.Value.Status);
            Assert.AreEqual("7656", PlaytestSessionIdentityProvider.Current.SteamId);
        }

        [Test]
        public void 許可されなければ識別は空のまま()
        {
            PlaceBuildInfoMarker();
            LogAssert.Expect(LogType.Error, new Regex(@"\[PlaytestReceiver\] launch blocked: NotAllowed"));
            Evaluate(PlaytestApiResult.Responded(403, "{\"reason\":\"not-allowed\"}"));

            Assert.IsNull(PlaytestSessionIdentityProvider.Current.SteamId);
        }

        // steamIdの無い200で通すと、報告・進行記録・異常終了箱が誰のものか分からないまま走る
        // Passing a 200 without steamId would run with reports, progress records and crash boxes that name nobody
        [Test]
        public void steamIdの欠けた200は止めて識別は空のまま()
        {
            PlaceBuildInfoMarker();
            LogAssert.Expect(LogType.Error, new Regex(@"\[PlaytestReceiver\] launch blocked: Unreachable malformed session response"));
            Evaluate(PlaytestApiResult.Responded(200, "{\"allowed\":true,\"token\":\"tok\",\"expiresAt\":\"2999-01-01T00:00:00.000Z\"}"));

            Assert.IsTrue(PlaytestLaunchGate.Current.Value.IsBlocked);
            Assert.IsNull(PlaytestSessionIdentityProvider.Current.SteamId);
        }

        [Test]
        public void 開発者モードでは照合せず識別は空のまま()
        {
            RemoveBuildInfoMarker();
            Evaluate(PlaytestApiResult.Responded(200, PlaytestSessionBodies.AllowedFarFuture));

            Assert.AreEqual(PlaytestGateStatus.DeveloperMode, PlaytestLaunchGate.Current.Value.Status);
            Assert.IsNull(PlaytestSessionIdentityProvider.Current.SteamId);
        }

        private static void Evaluate(PlaytestApiResult sessionResponse)
        {
            var api = new FakeApi();
            api.SessionResponses.Add(sessionResponse);
            PlaytestLaunchGate.EvaluateAsync(new FakeTicketProvider("aabb"), api, Now, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static void PlaceBuildInfoMarker()
        {
            var path = GameSystemPaths.BuildInfoFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{}");
        }

        private static void RemoveBuildInfoMarker()
        {
            if (File.Exists(GameSystemPaths.BuildInfoFilePath)) File.Delete(GameSystemPaths.BuildInfoFilePath);
        }

        // 元から在ったものは戻し、無かったものだけを消す
        // Whatever existed is restored, and only what did not exist is deleted
        private static void Restore(string target, byte[] original)
        {
            if (original != null) File.WriteAllBytes(target, original);
            else if (File.Exists(target)) File.Delete(target);
        }
    }
}
```

- [ ] **Step 2b: 既存の `PlaytestLaunchGateCheckingTest` の後始末に識別の復元を足す**

Allowed まで通すこのテストは、Task 1 以後は識別 `"7656"` を据える。静的な識別が後続の EditMode テスト（`CleanExitMarkWriter.InstallAtStartup` を呼ぶ `CleanExitMarkerTest` 等）へ漏れないよう、`RestoreBuildInfo` の `PlaytestLaunchGate.SetCurrent(PlaytestGateResult.NotEvaluated);` の直後に1行足し、using に `using Client.Game.InGame.BugReport.Playtest;` を足す:

```csharp
            PlaytestSessionIdentityProvider.SetCurrent(new EmptyPlaytestSessionIdentity());
```

- [ ] **Step 3: `PlaytestSessionResponse` に `steamId` を足す**

```csharp
using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver.Http.Responses
{
    // POST /v1/session の200応答。生成はParse経由だけに閉じ、steamId・token・期限が揃った応答しか作れない
    // The 200 body of POST /v1/session; Parse is the only constructor, so an instance always carries a steamId, a token and its expiry
    internal sealed class PlaytestSessionResponse
    {
        private PlaytestSessionResponse(string steamId, string token, bool allowed, DateTime expiresAtUtc)
        {
            SteamId = steamId;
            Token = token;
            Allowed = allowed;
            ExpiresAtUtc = expiresAtUtc;
        }

        // 受け口がSteam Web APIで検証したSteamID。報告・進行記録・異常終了箱の識別になる（ADR 0065）
        // The SteamID the receiver verified through the Steam Web API; it becomes the identity of reports, progress records and crash boxes (ADR 0065)
        public string SteamId { get; }
        public string Token { get; }
        public bool Allowed { get; }
        public DateTime ExpiresAtUtc { get; }

        public static PlaytestSessionResponse Parse(string body)
        {
            string steamId;
            string token;
            bool allowed;
            string expiresAtText;

            // 受け口の本文は外部入力のJSON。キャプティブポータルは200でHTMLを返すため、この境界で畳んでnullにする
            // The receiver's body is external-input JSON; captive portals answer 200 with HTML, so this boundary folds it to null
            try
            {
                var parsed = JsonConvert.DeserializeObject<JObject>(body, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
                steamId = (string)parsed["steamId"];
                token = (string)parsed["token"];
                allowed = (bool?)parsed["allowed"] ?? false;
                expiresAtText = (string)parsed["expiresAt"];
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlaytestReceiver] session response was not the expected JSON: {exception.GetBaseException().Message}");
                return null;
            }

            // 誰の記録かを載せられない200で通すと、識別が空のまま報告と進行記録が走る。欠落も空文字も許可しない（ADR 0065）
            // A 200 that cannot name the tester would run reports and progress records with no identity; neither a missing nor an empty value is accepted (ADR 0065)
            if (string.IsNullOrEmpty(steamId))
            {
                Debug.LogWarning("[PlaytestReceiver] session response lacked steamId");
                return null;
            }

            // トークンの無い200で通すと、照合だけ通ってアップロードが全滅する。欠落は許可しない
            // A 200 without a token would pass the gate and then fail every upload, so a missing field is refused
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[PlaytestReceiver] session response lacked token");
                return null;
            }

            if (!DateTimeOffset.TryParse(expiresAtText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiresAt))
            {
                Debug.LogWarning($"[PlaytestReceiver] session response had no readable expiresAt: '{expiresAtText}'");
                return null;
            }

            return new PlaytestSessionResponse(steamId, token, allowed, expiresAt.UtcDateTime);
        }
    }
}
```

- [ ] **Step 4: `PlaytestSession` に `VerifiedSteamId` を足す**

フィールド `_inFlight` の宣言（41行目）の直後に足す:

```csharp

        // 受け口が検証したSteamID。Allowedを返した認証の後だけ読む（ADR 0065）
        // The SteamID the receiver verified; read only after an authentication that returned Allowed (ADR 0065)
        public string VerifiedSteamId { get; private set; }
```

`AuthenticateOnceAsync` の `_token = parsed.Token;`（112行目）の直前に足す:

```csharp
                VerifiedSteamId = parsed.SteamId;
```

- [ ] **Step 5: `ReceiverVerifiedSessionIdentity` を作る**

```csharp
using Client.Game.InGame.BugReport.Playtest;

namespace Client.PlaytestReceiver.Gate
{
    // 起動時照合で受け口が検証したSteamIDを、報告・進行記録・異常終了箱の識別として差し込む実体（ADR 0065）
    // The identity that carries the SteamID the receiver verified at the launch check into reports, progress records and crash boxes (ADR 0065)
    public sealed class ReceiverVerifiedSessionIdentity : IPlaytestSessionIdentity
    {
        public string SteamId { get; }

        public ReceiverVerifiedSessionIdentity(string steamId)
        {
            SteamId = steamId;
        }
    }
}
```

- [ ] **Step 6: `PlaytestLaunchGate.EvaluateAsync` で識別を据える**

`using` に `using Client.Game.InGame.BugReport.Playtest;` を足し、43〜49行目を次に置き換える:

```csharp
            var result = PlaytestGateDecision.Decide(true, true, authenticated.Outcome, authenticated.Detail, session);
            if (result.IsBlocked)
            {
                Debug.LogError($"[PlaytestReceiver] launch blocked: {result.Status} {result.Detail}");
            }

            // 識別は結果を配る前に据える。購読側（タイトルのゲート・開始経路）が読む時点で検証済みSteamIDが揃っている（ADR 0065）
            // The identity is set before the verdict goes out, so subscribers (title gates, start paths) already see the verified SteamID (ADR 0065)
            if (result.TryGetAllowedSession(out var allowedSession)) PlaytestSessionIdentityProvider.SetCurrent(new ReceiverVerifiedSessionIdentity(allowedSession.VerifiedSteamId));

            SetCurrent(result);
```

- [ ] **Step 7: コンパイルしてテストを通す**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaytestReceiver\.(PlaytestSessionTest|PlaytestLaunchGateIdentityTest|PlaytestLaunchGateCheckingTest|PlaytestLaunchGateBlockTest|PlaytestGateDecisionTest)"`
Expected: 全件 PASS

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.PlaytestReceiver moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver
git commit -m "feat(playtest): 起動時照合で受け口の検証済みSteamIDを識別に据える (ADR 0065)"
```

---

## Task 2: 前回セッションの退避をタイトル／直接起動の1回に束ね、記録の判定から WebUiHost を外す（Client.Starter）

**前提:** 「## 機能の死活表」の D1・D2 は案 A で裁定済み（2026-09-20）。なお D-C8 の反映で、直接起動の明示通過は `RunAtStartup` の分岐ではなく `PlaytestTitleGates` の起動シーン判定が行う。

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PreviousSessionStartupTasks.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PlaytestRecordCollection.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:88-91,192-194`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/Marks/CleanExitMarkWriter.cs:9-12`
- Test（回帰）: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Playtest/PlaytestReportAndProgressTest.cs`、`Client.Tests/BugReport/LastSession/*`

**Interfaces:**
- Consumes: `PreviousSessionSalvage.RunAtStartup(bool isRemoteConnection, string worldSnapshotDirectory) : PreviousSessionArtifacts`、`PreviousSessionSalvage.RequireArtifacts()`、`PlaytestStartGateBypass.UnattendedReason() : string`、`CliConvert.Parse<StartServerSettings>(string[])`（`Server.Boot` / `Server.Boot.Args`）。
- Produces: `PreviousSessionStartupTasks.SalvageAtTitle() : PreviousSessionArtifacts`（1起動に1回。2回目は `InvalidOperationException`）、`PreviousSessionStartupTasks.RunAtStartup(bool collectsPlaytestRecords, bool isRemoteConnection, string worldDirectory) : void`（署名は不変）、`PlaytestRecordCollection.Decide(bool isRemoteConnection) : bool`。

- [ ] **Step 1: `PreviousSessionStartupTasks` を書き換える**

```csharp
using System;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Recording.ProcessScope;
using Client.Game.InGame.Playtest.Progress.Storage;
using Game.Paths;
using Server.Boot;
using Server.Boot.Args;
using UnityEngine;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// 前回セッションの印を読む処理（退避と印の消費）を起動1回に1度、1箇所で行う（ADR 0060 裁定5・ADR 0065）。
    /// タイトルを通る起動はタイトルの照合通過で、タイトルを通らない直接起動（テスト・DSL・出展モードの自動開始）はパイプライン先頭で行う。
    /// 正常終了の書き手と進行記録の回収は、識別（検証済みSteamID）が確定した後のパイプライン先頭に置く。
    /// Reads the previous session's marks (salvage and consumption) once per boot at a single spot (ADR 0060 adjudication 5, ADR 0065).
    /// A boot through the title does it when the launch check passes there; a direct boot that skips the title (tests, the DSL, event-mode auto start) does it at the head of the pipeline.
    /// The clean-exit writer and the progress recovery sit at the head of the pipeline, after the identity (verified SteamID) is settled.
    /// </summary>
    public static class PreviousSessionStartupTasks
    {
        private static bool _salvagedThisBoot;

        // Editorの再生し直しは同じプロセスで起動をやり直すため、再生ごとに未退避へ戻す（前例: PlaytestLaunchGate）
        // An Editor replay restarts the boot in the same process, so each play resets to "not salvaged" (precedent: PlaytestLaunchGate)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            _salvagedThisBoot = false;
        }

        // タイトルの照合通過で1回だけ呼ぶ。Play locally が起動する既定ワールドのスナップショットを退避対象にする
        // Called once when the launch check passes at the title; the default world that Play locally boots is the snapshot source
        public static PreviousSessionArtifacts SalvageAtTitle()
        {
            if (_salvagedThisBoot) throw new InvalidOperationException("PreviousSessionStartupTasks: この起動の退避は済んでいます（タイトルの退避が2回目に到達しました）");
            var defaultWorldDirectory = CliConvert.Parse<StartServerSettings>(Array.Empty<string>()).WorldDirectory;
            return Salvage(false, defaultWorldDirectory);
        }

        // パイプライン先頭で呼ぶ。タイトルで退避済みなら書き手の設置と回収だけを行う
        // Called at the head of the pipeline; when the title already salvaged, it only installs the writer and recovers
        public static void RunAtStartup(bool collectsPlaytestRecords, bool isRemoteConnection, string worldDirectory)
        {
            if (!_salvagedThisBoot)
            {
                Debug.Log("PreviousSessionStartupTasks: タイトルを経由しない起動のため、ここで前回セッションを退避します");
                Salvage(isRemoteConnection, worldDirectory);
            }

            // 記録を集めない起動は今回の印を書かず、前回の進行記録も回収しない。回収は次に集める起動が行う（理由はPlaytestRecordCollectionがログ済み）
            // A boot that collects nothing writes no marks of its own and leaves the leftover progress records to the next collecting boot (PlaytestRecordCollection logged why)
            if (!collectsPlaytestRecords) return;

            // 書き手は設置時に識別を読む。照合がAllowedで検証済みSteamIDを据えた後のここに置く（ADR 0065）
            // The writer reads the identity at installation, so it sits here, after the launch check set the verified SteamID on Allowed (ADR 0065)
            CleanExitMarkWriter.InstallAtStartup(RecordingProcessDirectories.CurrentProcessId(), ProcessSessionScope.CurrentSessionName);

            // 前回の書きかけの進行記録を、消費した印の結果で畳む（F19）
            // Folds the half-written progress records by the consumed marks' outcome (F19)
            ProgressSessionRecovery.RecoverLeftoverSessions(PreviousSessionSalvage.RequireArtifacts().ExitedCleanlyByProcessId);
        }

        // この起動のセッション名を先に確定してから退避する。退避は「今回以外」を畳み、書き手は全員この名前の下へ書く（F05）
        // This boot's session name is fixed before salvaging: the salvage folds everything else, and every writer writes under this name (F05)
        private static PreviousSessionArtifacts Salvage(bool isRemoteConnection, string worldDirectory)
        {
            ProcessSessionScope.BeginNewSession();
            var artifacts = PreviousSessionSalvage.RunAtStartup(isRemoteConnection, WorldDataDirectory.FromWorldRoot(worldDirectory).SnapshotDirectory);
            _salvagedThisBoot = true;
            return artifacts;
        }
    }
}
```

（この時点では Play locally 後の WebUI 開始ゲート（`PlaytestStartGates`）がまだ残り、迂回印を読んで消費するのはそちら。ここで印を読むと、Editor の手動テスト実行で WebUI のゲートが無人判定を失って応答待ちで止まるため、印の消費は Task 3 Step 3b でゲート撤去と同時に移す。`Client.Game.InGame.BugReport.Playtest` の using は Task 3 で使うまで未使用になるので、この Step では付けず Task 3 Step 3b で足す。）

（`CliConvert` / `StartServerSettings` の名前空間は `InitializeScenePipeline.cs` が使っている `Server.Boot` / `Server.Boot.Args` と同じ。コンパイルで違えばその using に合わせる。）

- [ ] **Step 2: `PlaytestRecordCollection` を書き換える**

```csharp
using UnityEngine;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// プレイテストの記録（ログ・録画・バグ報告の確保・進行記録・正常終了の印）をこの起動で集めるかを1箇所で決める。
    /// Decides in one place whether this boot collects playtest records (logs, recording, bug-report capture, progress record, clean-exit marks).
    /// </summary>
    public static class PlaytestRecordCollection
    {
        // 集めないのはリモート接続だけ。同意はタイトルで済んでいるので WebUiHost の起動有無は条件にしない（ADR 0065）
        // Only a remote connection collects nothing; the consent was settled at the title, so whether WebUiHost started is no longer a condition (ADR 0065)
        public static bool Decide(bool isRemoteConnection)
        {
            if (!isRemoteConnection) return true;
            Debug.Log("PlaytestRecordCollection: リモート接続のためプレイテストの記録（ログ・録画・進行記録・終了の印）を集めません");
            return false;
        }
    }
}
```

- [ ] **Step 3: `InitializeScenePipeline` の呼び出しを更新し 200 行以下にする**

88〜91行目（「前回セッションの印を読む処理はここ1箇所へ…」のコメント2行と `collectsPlaytestRecords` / `RunAtStartup` の2行）を次の4行に置き換える:

```csharp
            // 退避はタイトル（直接起動ならここ）、書き手の設置はここ。記録を集めるかもここで1度だけ決める（ADR 0060 裁定5・ADR 0065）
            // Salvage happens at the title (here for a direct boot) and the writers are installed here; whether to collect is decided once here too (ADR 0060 adjudication 5, ADR 0065)
            var collectsPlaytestRecords = Playtest.PlaytestRecordCollection.Decide(_proprieties.IsRemoteConnection);
            Playtest.PreviousSessionStartupTasks.RunAtStartup(collectsPlaytestRecords, _proprieties.IsRemoteConnection, args.WorldDirectory);
```

`MainGameSceneLoaded` 内の `GameShutdownEvent.FireGameShutdown(GameShutdownReason.InitializationFailed);` と `SceneManager.LoadScene(SceneConstant.MainMenuSceneName);` の間の空行（193行目）を削除する（ファイルを 200 行にする。Task 3 の Finalizer 引数変更は行数を変えない）。

- [ ] **Step 4: `CleanExitMarkWriter` の説明コメントを配置に合わせる**

9〜12行目の4行を次に置き換える（コードは変えない）:

```csharp
    // 書き手はパイプライン先頭で据える。前回の印の消費はタイトル（直接起動ならパイプライン）で先に済み、その間この起動は印も録画も書かないので偽の異常終了は生まれない（ADR 0065）
    // The writer is installed at the head of the pipeline; the previous marks were consumed earlier at the title (or the pipeline for a direct boot), and this boot writes no mark or recording in between, so no false crash arises (ADR 0065)
    // 設置時に識別を読むため、照合がAllowedで検証済みSteamIDを据えた後でなければならない
    // It reads the identity at installation, so it must come after the launch check set the verified SteamID on Allowed
```

- [ ] **Step 5: コンパイルと回帰テスト**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `wc -l moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs` → 200 以下
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.BugReport\.(CleanExitMarkerTest|PreviousSessionSalvageTest|PlaytestStartGatesTest)"` → PASS（`PlaytestStartGatesTest` はこの時点ではまだ残っていて通る）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaytestReportAndProgressTest" --timeout-seconds 900` → PASS（直接起動で退避→書き手設置→正常終了マーカー→進行記録が一巡する）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/Playtest moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/Marks/CleanExitMarkWriter.cs
git commit -m "refactor(playtest): 前回セッションの退避をタイトル／直接起動の1回に束ね、記録の判定からWebUiHostを外す (ADR 0065)"
```

---

## Task 3: タイトルのゲートの本体を Client.Starter に置き、WebUI 側の C# ゲート一式を撤去する

**Files:**
- Move: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestConsentGate.cs` → `moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/PlaytestConsentGate.cs`
- Move: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/CrashReportGate.cs` → `moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/CrashReportGate.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/PlaytestTitleGateStep.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/PlaytestTitleGateSequence.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/PlaytestTitleGates.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestGateBinder.cs`、`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestStartGateHandles.cs`、`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Playtest/CrashReportGateActions.cs`、`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Playtest/PlaytestConsentGateActions.cs`、`moorestech_client/Assets/Scripts/Client.Starter/Playtest/PlaytestStartGates.cs`（各 `.meta` と、空になった `Game/Playtest`・`Game/Actions/Playtest` ディレクトリの `.meta`）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/StartGates/StartGateTopics.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/StartGates/WaitingGateTopic.cs:9-12`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs:26-58`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:179`（`new MainGameInitializationFinalizer(...)` の行）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PreviousSessionStartupTasks.cs`（直接起動の迂回印の消費）
- Move（テスト）: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/CrashReportGateTest.cs`・`PlaytestConsentGateTest.cs` → `moorestech_client/Assets/Scripts/Client.Tests/Playtest/TitleGates/`
- Create（テスト）: `moorestech_client/Assets/Scripts/Client.Tests/Playtest/TitleGates/PlaytestTitleGateSequenceTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Playtest/TitleGates/PlaytestTitleGatesTest.cs`
- Delete（テスト）: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/PlaytestStartGatesTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/PlaytestGateActionResultMappingTest.cs`（+ `.meta`）
- Modify（テスト）: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventLanguageGateTopicTest.cs:38-46`

**Interfaces:**
- Consumes: Task 2 の `PreviousSessionStartupTasks.SalvageAtTitle() : PreviousSessionArtifacts`。`IPlaytestUploadRequester.RequestUpload()`（`Client.Game.InGame.BugReport.Submit`）、`ICrashBundleWriter` / `CrashBundleWriter`、`PendingCrashReportMark`、`PlaytestConsentFlag`、`PlaytestStartGateBypass.UnattendedReason()`、`PlaytestGateResult`（`IsBlocked` / `Status`）。
- Produces（Task 4 が使う。すべて `Client.Starter.Playtest.TitleGates`）:
  - `enum PlaytestTitleGateStep { NotStarted, Consent, CrashReport, Passed }`
  - `enum PlaytestConsentResult { Acknowledged, AlreadyAcknowledged }`、`enum CrashReportResponseResult { Sent, Skipped, WriteFailed, AlreadyResponded }`
  - `sealed class PlaytestTitleGateSequence`: `IReadOnlyReactiveProperty<PlaytestTitleGateStep> Step`、`PlaytestConsentResult AcknowledgeConsent()`、`UniTask<CrashReportResponseResult> RespondCrashReportAsync(bool send, string description)`（ctor と `RunAsync(CancellationToken)` は internal）
  - `static class PlaytestTitleGates`: `IReadOnlyReactiveProperty<PlaytestTitleGateStep> Step`、`PlaytestTitleGateSequence Begin(PlaytestGateResult verdict, IPlaytestUploadRequester uploadRequester, CancellationToken ct)`、`bool TryPassStart(string callerName)`、internal `Compose(PreviousSessionArtifacts, bool launchAllowed, IPlaytestUploadRequester, string unattendedReason)`・internal `SetStep(PlaytestTitleGateStep)`

- [ ] **Step 1: ゲート2つを Client.Starter へ移す**

```bash
mkdir -p moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates
git mv moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestConsentGate.cs moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/PlaytestConsentGate.cs
git mv moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestConsentGate.cs.meta moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/PlaytestConsentGate.cs.meta
git mv moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/CrashReportGate.cs moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/CrashReportGate.cs
git mv moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/CrashReportGate.cs.meta moorestech_client/Assets/Scripts/Client.Starter/Playtest/TitleGates/CrashReportGate.cs.meta
```

（`TitleGates` ディレクトリ自体の `.meta` は Unity がコンパイル時に生成する。手で作らない。）

移した `PlaytestConsentGate.cs` の中身を次にする（WebUI の待機通知 `IStartGateWaitState`・`Subject` を外す）:

```csharp
using Client.Game.InGame.BugReport.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Playtest.TitleGates
{
    /// <summary>
    /// 初回だけ「送られる内容」を出して開始を止める（ADR 0061・0065）。表示はタイトルの uGUI が持ち、ここは待機と了解の規則だけを持つ。
    /// Shows what will be sent and holds the start on the first boot only (ADR 0061, 0065); the title's uGUI owns the display and this owns only the wait and acknowledgement rules.
    /// </summary>
    public sealed class PlaytestConsentGate
    {
        private readonly UniTaskCompletionSource _acknowledgeSource = new();

        internal bool IsWaitingAcknowledgement { get; private set; }

        // 待つかどうかは初期状態で決める。既読フラグの読み取りは組み立て側（PlaytestTitleGates.Compose）が持つ
        // Whether to wait is fixed at construction; reading the read flag belongs to the assembler (PlaytestTitleGates.Compose)
        internal PlaytestConsentGate(bool startsWaiting)
        {
            IsWaitingAcknowledgement = startsWaiting;
            if (!startsWaiting) _acknowledgeSource.TrySetResult();
        }

        internal UniTask WaitForAcknowledgementAsync()
        {
            return _acknowledgeSource.Task;
        }

        // 了解は1回だけ効く。既読フラグはここで書き、次回以降は待機せず素通りする
        // Only the first acknowledgement takes effect; the read flag is written here so later boots pass straight through
        internal PlaytestConsentResult Acknowledge()
        {
            if (!IsWaitingAcknowledgement)
            {
                Debug.LogWarning("PlaytestConsentGate: 了解済みまたは待機していないゲートへ了解が届いたため無視します");
                return PlaytestConsentResult.AlreadyAcknowledged;
            }
            IsWaitingAcknowledgement = false;

            // フラグ書き込みに関わらずゲートは必ず閉じる。例外で抜けても起動が永久に止まらないよう解除はfinallyに置く
            // The gate always closes regardless of the flag write; releasing in finally keeps an escaping exception from halting the startup forever
            try
            {
                PlaytestConsentFlag.Acknowledge();
            }
            finally
            {
                _acknowledgeSource.TrySetResult();
            }
            return PlaytestConsentResult.Acknowledged;
        }
    }

    public enum PlaytestConsentResult
    {
        Acknowledged,
        AlreadyAcknowledged
    }
}
```

移した `CrashReportGate.cs` の中身を次にする:

```csharp
using Client.Game.InGame.BugReport.LastSession;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Playtest.TitleGates
{
    /// <summary>
    /// 前回異常終了の送信確認。応答があるまで開始を止める（ADR 0061・0065）。表示はタイトルの uGUI が持つ。
    /// The previous-crash send confirmation that holds the start until it is answered (ADR 0061, 0065); the title's uGUI owns the display.
    /// </summary>
    public sealed class CrashReportGate
    {
        private readonly UniTaskCompletionSource _responseSource = new();
        private readonly ICrashBundleWriter _writer;
        private readonly PreviousSessionArtifacts _artifacts;

        internal bool IsWaitingResponse { get; private set; }

        // 異常終了なら常に確認する。退避物ゼロでも説明文だけの箱には価値があるので待機条件から外さない
        // Always ask after an unclean exit; a description-only box still has value, so an empty salvage does not skip the wait
        internal CrashReportGate(ICrashBundleWriter writer, PreviousSessionArtifacts artifacts) : this(writer, artifacts, !artifacts.PreviousExitWasClean)
        {
        }

        private CrashReportGate(ICrashBundleWriter writer, PreviousSessionArtifacts artifacts, bool isWaitingResponse)
        {
            _writer = writer;
            _artifacts = artifacts;
            IsWaitingResponse = isWaitingResponse;
            if (!IsWaitingResponse) _responseSource.TrySetResult();
        }

        // 無人起動向けの閉じたゲート。退避結果を「正常終了」に偽装して借りず、閉じた状態そのものとして作る（F13）
        // A closed gate for unattended boots, built as closed in its own right instead of borrowing a salvage result disguised as a clean exit (F13)
        internal static CrashReportGate Closed()
        {
            return new CrashReportGate(null, null, false);
        }

        internal UniTask WaitForResponseAsync()
        {
            return _responseSource.Task;
        }

        // 応答は1回だけ効く。二重クリックと再送は「応答済み」として区別し、成功と一律に丸めない
        // Only the first answer takes effect; double clicks and resends are distinguished instead of folded into success
        internal async UniTask<CrashReportResponseResult> RespondAsync(bool send, string description)
        {
            if (!IsWaitingResponse)
            {
                Debug.LogWarning("CrashReportGate: 応答済みまたは待機していないゲートへ応答が届いたため無視します");
                return CrashReportResponseResult.AlreadyResponded;
            }
            IsWaitingResponse = false;

            // 「送らない」でも退避物は消さない。答えた時点で未応答の印を消す（F04）
            // Skipping keeps the salvage; answering clears the pending mark (F04)
            if (!send)
            {
                Debug.Log("前回異常終了の記録は送らないと選ばれました");
                PendingCrashReportMark.Clear(_artifacts.LastSessionDirectory);
                _responseSource.TrySetResult();
                return CrashReportResponseResult.Skipped;
            }

            // 箱を書けなかったら待機へ戻す。唯一の証跡なので、閉じてしまうと二度と送り直せないまま無音で消える
            // A failed write returns the gate to waiting: this is the only evidence, and closing would drop it silently with no way to resend
            // 書き出しが例外で抜けても起動が永久に止まらないよう、解除はfinallyに置く
            // Releasing sits in finally so an escaping exception never halts the startup forever
            var written = false;
            try
            {
                written = await _writer.WriteAsync(_artifacts, description ?? "") != null;
            }
            finally
            {
                if (written)
                {
                    PendingCrashReportMark.Clear(_artifacts.LastSessionDirectory);
                    _responseSource.TrySetResult();
                }
                else ReturnToWaiting();
            }
            return written ? CrashReportResponseResult.Sent : CrashReportResponseResult.WriteFailed;
        }

        // 「送らない」は常に押せるため、書けないまま待機へ戻しても起動が恒久停止することはない
        // "Do not send" is always available, so returning to waiting after a failed write never halts the boot permanently
        private void ReturnToWaiting()
        {
            Debug.LogError("前回異常終了の箱を書けなかったため確認を閉じません（送り直すか、送らないを選べます）");
            IsWaitingResponse = true;
        }
    }

    public enum CrashReportResponseResult
    {
        Sent,
        Skipped,
        WriteFailed,
        AlreadyResponded
    }
}
```

- [ ] **Step 2: 段階・順序・窓口を作る**

`PlaytestTitleGateStep.cs`:

```csharp
namespace Client.Starter.Playtest.TitleGates
{
    // タイトルのゲートの段階。Passed になるまで Play locally とサーバー接続は開始しない（ADR 0065）
    // The title gates' step; Play locally and server connection do not start until it reaches Passed (ADR 0065)
    public enum PlaytestTitleGateStep
    {
        NotStarted,
        Consent,
        CrashReport,
        Passed,
    }
}
```

`PlaytestTitleGateSequence.cs`:

```csharp
using System.Threading;
using Client.Game.InGame.BugReport.Submit;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Starter.Playtest.TitleGates
{
    /// <summary>
    /// タイトルのゲートを「同意 → 前回異常終了の確認」の順に進め、持ち越した記録の送信要求を同意の後ろへ置く（ADR 0065）。
    /// Advances the title gates in the order consent then previous-crash confirmation, and places the carried-over upload request behind the consent (ADR 0065).
    /// </summary>
    public sealed class PlaytestTitleGateSequence
    {
        private readonly ReactiveProperty<PlaytestTitleGateStep> _step;
        private readonly PlaytestConsentGate _consent;
        private readonly CrashReportGate _crashReport;
        private readonly IPlaytestUploadRequester _uploadRequester;
        private readonly bool _uploadsEnabled;

        public IReadOnlyReactiveProperty<PlaytestTitleGateStep> Step => _step;

        // 段階の器は持ち主（PlaytestTitleGates）から受け取る。開始経路はそちらの静的窓口から同じ値を読む
        // The step holder comes from its owner (PlaytestTitleGates), whose static window the start paths read the same value from
        internal PlaytestTitleGateSequence(ReactiveProperty<PlaytestTitleGateStep> step, PlaytestConsentGate consent, CrashReportGate crashReport, IPlaytestUploadRequester uploadRequester, bool uploadsEnabled)
        {
            _step = step;
            _consent = consent;
            _crashReport = crashReport;
            _uploadRequester = uploadRequester;
            _uploadsEnabled = uploadsEnabled;
        }

        // 待たない段階は同期で抜けるので、既読かつ正常終了なら呼んだその場で Passed になる
        // A step that does not wait completes synchronously, so a read consent with a clean exit reaches Passed within this call
        internal async UniTask RunAsync(CancellationToken ct)
        {
            // 未読なら了解まで止める。持ち越しの送信は了解の後ろ（既読ならこの直後）に置く（ADR 0065）
            // Unread: hold until acknowledged. The carried-over upload sits after the acknowledgement (right here when already read) (ADR 0065)
            if (_consent.IsWaitingAcknowledgement)
            {
                EnterStep(PlaytestTitleGateStep.Consent);
                await _consent.WaitForAcknowledgementAsync().AttachExternalCancellation(ct);
            }
            RequestUploadIfEnabled("consent settled");

            // 何が送られるかを見せてから送信可否を聞く
            // Ask about sending only after showing what gets sent
            if (_crashReport.IsWaitingResponse)
            {
                EnterStep(PlaytestTitleGateStep.CrashReport);
                await _crashReport.WaitForResponseAsync().AttachExternalCancellation(ct);
            }
            EnterStep(PlaytestTitleGateStep.Passed);
        }

        public PlaytestConsentResult AcknowledgeConsent()
        {
            return _consent.Acknowledge();
        }

        public async UniTask<CrashReportResponseResult> RespondCrashReportAsync(bool send, string description)
        {
            var result = await _crashReport.RespondAsync(send, description);

            // 箱を書いたら同じ窓口へ送信をもう一度要求する。照合直後の走行はもう終わっていることがある（ADR 0065）
            // After writing the box, request an upload again through the same port; the run started after the check may already be over (ADR 0065)
            if (result == CrashReportResponseResult.Sent) RequestUploadIfEnabled("crash box written");
            return result;
        }

        // 待ちは上限を持たない。画面が出ないと無音で止まるため、段階の変化は必ずログに残す
        // The wait is unbounded; a missing screen would stall silently, so every step change is logged
        private void EnterStep(PlaytestTitleGateStep step)
        {
            Debug.Log($"[PlaytestTitleGates] step {step}");
            _step.Value = step;
        }

        private void RequestUploadIfEnabled(string trigger)
        {
            if (!_uploadsEnabled)
            {
                Debug.Log($"[PlaytestTitleGates] upload not requested after {trigger}: uploads are not enabled for this boot (developer mode, or an unattended boot whose consent is unread)");
                return;
            }
            _uploadRequester.RequestUpload();
        }
    }
}
```

`PlaytestTitleGates.cs`:

```csharp
using System;
using System.Threading;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver.Gate;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Starter.Playtest.TitleGates
{
    /// <summary>
    /// タイトルのゲートの唯一の窓口。照合通過で1回だけ始め、開始経路（Play locally・サーバー接続）はここへ問い合わせる（ADR 0065）。
    /// The single window onto the title gates; begun once when the launch check passes, and the start paths (Play locally, server connection) ask here (ADR 0065).
    /// </summary>
    public static class PlaytestTitleGates
    {
        // MainMenuシーンにはDIコンテナが無く、開始経路と表示が別のMonoBehaviourなのでstaticで持つ（前例: PlaytestLaunchGate）
        // The MainMenu scene has no DI container and the start paths and the view are separate MonoBehaviours, so it is held statically (precedent: PlaytestLaunchGate)
        private static readonly ReactiveProperty<PlaytestTitleGateStep> StepProperty = new(PlaytestTitleGateStep.NotStarted);
        public static IReadOnlyReactiveProperty<PlaytestTitleGateStep> Step => StepProperty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            SetStep(PlaytestTitleGateStep.NotStarted);
        }

        // 照合がAllowedか開発者モードに決まった直後に、タイトルの合成ルートから1回だけ呼ぶ
        // Called once from the title's composition root right after the launch check settles as Allowed or developer mode
        public static PlaytestTitleGateSequence Begin(PlaytestGateResult verdict, IPlaytestUploadRequester uploadRequester, CancellationToken ct)
        {
            if (StepProperty.Value != PlaytestTitleGateStep.NotStarted) throw new InvalidOperationException($"PlaytestTitleGates: タイトルのゲートは起動1回に1度だけ始めます（現在 {StepProperty.Value}）");
            if (verdict.IsBlocked) throw new InvalidOperationException($"PlaytestTitleGates: 照合を通っていない結果（{verdict.Status}）ではタイトルのゲートを始めません");

            var artifacts = PreviousSessionStartupTasks.SalvageAtTitle();
            var sequence = Compose(artifacts, verdict.Status == PlaytestGateStatus.Allowed, uploadRequester, PlaytestStartGateBypass.UnattendedReason());
            sequence.RunAsync(ct).Forget();
            return sequence;
        }

        public static bool TryPassStart(string callerName)
        {
            var step = StepProperty.Value;
            if (step == PlaytestTitleGateStep.Passed) return true;

            // 断った理由は開発者ログへ出す。答えるべき確認は画面に出ているので、テスター向けの文言は足さない
            // The refusal goes to the developer log; the pending confirmation is already on screen, so no tester-facing text is added
            Debug.LogWarning($"[PlaytestTitleGates] {callerName} refused: the title gates are at {step} (answer the consent / previous-crash confirmation first)");
            return false;
        }

        // 退避結果・照合・無人の理由からゲート一式を組む。CIはバッチモードで常に無人なので、無人の理由は引数で受けて対話起動もテストで組めるようにする
        // Builds the gate set from the salvage result, the check and the unattended reason; CI is always unattended in batch mode, so the reason is a parameter and tests can build an attended boot too
        internal static PlaytestTitleGateSequence Compose(PreviousSessionArtifacts artifacts, bool launchAllowed, IPlaytestUploadRequester uploadRequester, string unattendedReason)
        {
            var consentAcknowledged = PlaytestConsentFlag.IsAcknowledged();
            if (unattendedReason == null)
            {
                return new PlaytestTitleGateSequence(StepProperty, new PlaytestConsentGate(!consentAcknowledged), new CrashReportGate(new CrashBundleWriter(), artifacts), uploadRequester, launchAllowed);
            }

            // 無人起動には応答者が居ない。閉じたゲートで進め、未読のままなら持ち越しも送らない（「了解まで送らない」を無人でも守る）
            // An unattended boot has nobody to answer: proceed with closed gates, and ship nothing carried over while the consent is unread (the hold applies unattended too)
            Debug.LogWarning($"[PlaytestTitleGates] 無人起動のためタイトルのゲートを出さずに進みます reason:{unattendedReason} previousExitWasClean:{artifacts.PreviousExitWasClean} consentAcknowledged:{consentAcknowledged}（退避物は last-session に残り次回の対話起動で聞き直せます）");
            return new PlaytestTitleGateSequence(StepProperty, new PlaytestConsentGate(false), CrashReportGate.Closed(), uploadRequester, launchAllowed && consentAcknowledged);
        }

        internal static void SetStep(PlaytestTitleGateStep step)
        {
            StepProperty.Value = step;
        }
    }
}
```

- [ ] **Step 3: WebUI 側の C# ゲート一式と Play locally 後の待ちを撤去する**

```bash
git rm moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestGateBinder.cs moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestGateBinder.cs.meta
git rm moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestStartGateHandles.cs moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestStartGateHandles.cs.meta
git rm -r moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Playtest moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Playtest.meta
git rm moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest.meta
git rm moorestech_client/Assets/Scripts/Client.Starter/Playtest/PlaytestStartGates.cs moorestech_client/Assets/Scripts/Client.Starter/Playtest/PlaytestStartGates.cs.meta
ls moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest 2>/dev/null && echo "空でなければ中身を確認してから消す"
```

- [ ] **Step 3b: 直接起動の迂回印の消費を `PreviousSessionStartupTasks.RunAtStartup` へ移す**

WebUI の開始ゲートが消えたので、Editor の迂回印を読む者が直接起動に居なくなる。読まないと印が次の手動のタイトル起動へ持ち越され、確認が1回消える。`PreviousSessionStartupTasks.cs` に `using Client.Game.InGame.BugReport.Playtest;` を足し、Task 2 で書いた直接起動の分岐を次にする:

```csharp
            if (!_salvagedThisBoot)
            {
                // Editorの迂回印は読んだ時点で消費される。ここで読まないと次の手動のタイトル起動へ持ち越され、確認が1回消える
                // The Editor bypass mark is consumed on read; leaving it unread would carry it to the next manual title boot and skip its confirmations once
                var unattendedReason = PlaytestStartGateBypass.UnattendedReason();
                Debug.Log($"PreviousSessionStartupTasks: タイトルを経由しない起動のため、ここで前回セッションを退避します。同意と前回異常終了の確認はこの起動では出さず、未応答の印は次にタイトルを通る起動で聞き直します unattended:{unattendedReason ?? "none"}");
                Salvage(isRemoteConnection, worldDirectory);
            }
```

`StartGateTopics.cs` を次にする:

```csharp
namespace Client.WebUiHost.Game.StartGates
{
    // 開始ゲートのtopic名と答えさせる順。プレイテストの同意と前回異常終了の確認はタイトル（uGUI）へ移り、WebUIに残るのは出展モードの言語選択だけ（ADR 0065）
    // Start-gate topic names and answer order; the playtest consent and crash confirmation moved to the title (uGUI), leaving only event mode's language selection in the WebUI (ADR 0065)
    internal static class StartGateTopics
    {
        public const string EventLanguageName = "event_mode.language_gate";

        public const int EventLanguagePrecedence = 0;
    }
}
```

`WaitingGateTopic.cs` 9〜12行目の summary を次にする:

```csharp
    /// <summary>
    /// 開始ゲート1枚の待機をsnapshotとeventで配信する。違うのはtopic名・順番・ゲート本体だけ
    /// Publishes one start gate's wait as a snapshot and events; gates differ only in topic name, precedence and gate
    /// </summary>
```

`MainGameInitializationFinalizer.cs` の 26〜58行目を次にする（`_isRemoteConnection` を外し、開始ゲートの待ちを消す。以降の `var starter = ...` から下は変えない）:

```csharp
        private readonly ServerConnectionResult _serverResult;
        private readonly string _localMasterDirectory;
        private readonly bool _collectsPlaytestRecords;

        public MainGameInitializationFinalizer(ServerConnectionResult serverResult, string localMasterDirectory, bool collectsPlaytestRecords)
        {
            _serverResult = serverResult;
            _localMasterDirectory = localMasterDirectory;
            _collectsPlaytestRecords = collectsPlaytestRecords;
        }

        // 出展モードの言語ゲートは人の応答を上限なく待つため、Play終了・アプリ終了のキャンセルを最後まで渡す
        // Event mode's language gate waits for a human answer without a bound, so the play-exit / quit cancellation is threaded all the way down
        public async UniTask RunAsync(CancellationToken exitToken)
        {
            await FinalizeAsync(exitToken);
            GameInitializedEvent.FireGameInitialized();
        }

        private async UniTask FinalizeAsync(CancellationToken exitToken)
        {
            // 出展モードは言語が決まるまで開始を止める。スキットとチュートリアルが英語で走り出す前に挟む
            // Event mode holds the start until a language is chosen, ahead of skits and tutorials starting in English
            // プレイテストの同意と前回異常終了の確認はタイトルで済んでいる（ADR 0065）
            // The playtest consent and the previous-crash confirmation were settled at the title (ADR 0065)
            await EventMode.EventModeStartGate.WaitForLanguageSelectionAsync(exitToken);
```

（`using UnityEngine;` は後段の `UnityEngine.Object` のため残す。未使用 using が出たら消す。）

`InitializeScenePipeline.cs` の Finalizer 生成行（179行目）を次にする:

```csharp
                new MainGameInitializationFinalizer(serverResult, serverDirectory, collectsPlaytestRecords).RunAsync(exitToken).Forget(exception =>
```

- [ ] **Step 4: テストを移し、消し、直す**

```bash
mkdir -p moorestech_client/Assets/Scripts/Client.Tests/Playtest/TitleGates
for f in CrashReportGateTest PlaytestConsentGateTest; do
  git mv moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/$f.cs moorestech_client/Assets/Scripts/Client.Tests/Playtest/TitleGates/$f.cs
  git mv moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/$f.cs.meta moorestech_client/Assets/Scripts/Client.Tests/Playtest/TitleGates/$f.cs.meta
done
git rm moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/PlaytestStartGatesTest.cs moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/PlaytestStartGatesTest.cs.meta
git rm moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/PlaytestGateActionResultMappingTest.cs moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/PlaytestGateActionResultMappingTest.cs.meta
```

移した2ファイルの先頭の using と namespace を次にする（本文のテストは変えない）。`CrashReportGateTest.cs`:

```csharp
using System;
using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Starter.Playtest.TitleGates;
using Client.Tests.BugReport;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest.TitleGates
```

`PlaytestConsentGateTest.cs`:

```csharp
using System.IO;
using Client.Game.InGame.BugReport.Playtest;
using Client.Starter.Playtest.TitleGates;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.Playtest.TitleGates
```

`EventLanguageGateTopicTest.cs` の 38〜46行目（`開始ゲートの順番は言語_同意_前回異常終了の順に小さい` とその上のコメント2行）を削除する。

- [ ] **Step 5: `PlaytestTitleGateSequenceTest` を書く**

```csharp
using System.IO;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Starter.Playtest.TitleGates;
using Client.Tests.BugReport;
using Client.Tests.PlaytestReceiver;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Playtest.TitleGates
{
    // 「照合 → 同意 → 前回異常終了の確認 → 開始を受け付ける」の順序と、持ち越しの送信要求を置く位置を最小構成ごとに押さえる（ADR 0065）
    // Pins the order check → consent → previous-crash confirmation → accept the start, and where the carried-over upload sits, per minimal configuration (ADR 0065)
    public class PlaytestTitleGateSequenceTest
    {
        private const string WrittenDirectory = "/tmp/crash-bundle-double";

        private bool _consentExisted;

        // 了解は本番と同じ場所へ既読フラグを書く。自分が作った分だけ後始末する
        // Acknowledging writes the read flag to the production location, so only what this test creates is cleaned up
        [SetUp]
        public void SetUp()
        {
            _consentExisted = PlaytestConsentFlag.IsAcknowledged();
        }

        [TearDown]
        public void TearDown()
        {
            if (!_consentExisted && File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
        }

        [Test]
        public void 既読かつ正常終了なら始めた時点で通過し送信を1回要求する()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(false, new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Clean()), uploads, true);

            sequence.RunAsync(CancellationToken.None).Forget();

            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }

        [Test]
        public void 未読なら同意で止まり了解するまで送信を要求しない()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(true, new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Clean()), uploads, true);

            sequence.RunAsync(CancellationToken.None).Forget();
            Assert.AreEqual(PlaytestTitleGateStep.Consent, sequence.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount, "了解の前に持ち越しを送っている");

            Assert.AreEqual(PlaytestConsentResult.Acknowledged, sequence.AcknowledgeConsent());
            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }

        [Test]
        public void 未読かつ異常終了なら同意の次に確認を出し箱を書いたら送信を再要求する()
        {
            var uploads = new RecordingUploadRequester();
            var writer = new RecordingCrashBundleWriter(WrittenDirectory);
            var sequence = Sequence(true, new CrashReportGate(writer, TestPreviousSessionArtifacts.Unclean()), uploads, true);

            sequence.RunAsync(CancellationToken.None).Forget();
            Assert.AreEqual(PlaytestTitleGateStep.Consent, sequence.Step.Value, "何が送られるかを見せる前に送信可否を聞いている");

            sequence.AcknowledgeConsent();
            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);

            Assert.AreEqual(CrashReportResponseResult.Sent, sequence.RespondCrashReportAsync(true, "落ちた").GetAwaiter().GetResult());
            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(2, uploads.RequestCount, "異常終了の箱を書いた後に送信を再要求していない");
            CollectionAssert.AreEqual(new[] { "落ちた" }, writer.Descriptions);
        }

        [Test]
        public void 既読かつ異常終了なら確認だけを出し送らないでも通過して再要求しない()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(false, new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Unclean()), uploads, true);

            sequence.RunAsync(CancellationToken.None).Forget();
            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);

            Assert.AreEqual(CrashReportResponseResult.Skipped, sequence.RespondCrashReportAsync(false, "").GetAwaiter().GetResult());
            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }

        // 書けなかった応答は答えたことにならない。確認に留まり、送らないを選べば先へ進める
        // A failed write is not an answer: the confirmation stays, and choosing not to send moves on
        [Test]
        public void 箱を書けなかったら確認に留まり送信を再要求しない()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(false, new CrashReportGate(new RecordingCrashBundleWriter(null), TestPreviousSessionArtifacts.Unclean()), uploads, true);
            sequence.RunAsync(CancellationToken.None).Forget();
            LogAssert.Expect(LogType.Error, "前回異常終了の箱を書けなかったため確認を閉じません（送り直すか、送らないを選べます）");

            Assert.AreEqual(CrashReportResponseResult.WriteFailed, sequence.RespondCrashReportAsync(true, "書けない").GetAwaiter().GetResult());
            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, sequence.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);

            Assert.AreEqual(CrashReportResponseResult.Skipped, sequence.RespondCrashReportAsync(false, "").GetAwaiter().GetResult());
            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
        }

        [Test]
        public void 開発者モードでは箱を書いても送信を要求しない()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(true, new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Unclean()), uploads, false);

            sequence.RunAsync(CancellationToken.None).Forget();
            sequence.AcknowledgeConsent();
            sequence.RespondCrashReportAsync(true, "開発者").GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestTitleGateStep.Passed, sequence.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount);
        }

        // 打ち切られた待ち（タイトルの破棄）の後に届いた了解では通過しない
        // An acknowledgement arriving after the wait was cancelled (the title was destroyed) does not pass the gates
        [Test]
        public void 打ち切られた後の了解では通過しない()
        {
            var uploads = new RecordingUploadRequester();
            var sequence = Sequence(true, new CrashReportGate(new RecordingCrashBundleWriter(WrittenDirectory), TestPreviousSessionArtifacts.Clean()), uploads, true);
            var cancellation = new CancellationTokenSource();

            sequence.RunAsync(cancellation.Token).Forget();
            cancellation.Cancel();
            sequence.AcknowledgeConsent();

            Assert.AreEqual(PlaytestTitleGateStep.Consent, sequence.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount);
        }

        private static PlaytestTitleGateSequence Sequence(bool consentWaiting, CrashReportGate crashReport, RecordingUploadRequester uploads, bool uploadsEnabled)
        {
            return new PlaytestTitleGateSequence(new ReactiveProperty<PlaytestTitleGateStep>(PlaytestTitleGateStep.NotStarted), new PlaytestConsentGate(consentWaiting), crashReport, uploads, uploadsEnabled);
        }
    }
}
```

- [ ] **Step 6: `PlaytestTitleGatesTest` を書く**

```csharp
using System.IO;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Starter.Playtest.TitleGates;
using Client.Tests.BugReport;
using Client.Tests.PlaytestReceiver;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.Playtest.TitleGates
{
    // 開始経路の関所と、無人・対話ごとのゲート一式の組み方を押さえる（ADR 0065）
    // Pins the start-path checkpoint and how the gate set is assembled for unattended and attended boots (ADR 0065)
    public class PlaytestTitleGatesTest
    {
        private bool _consentExisted;

        [SetUp]
        public void SetUp()
        {
            _consentExisted = PlaytestConsentFlag.IsAcknowledged();
        }

        // 段階は静的に持つので毎回未開始へ戻す。既読フラグは元の有無に戻す
        // The step is held statically, so it returns to NotStarted every time; the read flag returns to its original presence
        [TearDown]
        public void TearDown()
        {
            PlaytestTitleGates.SetStep(PlaytestTitleGateStep.NotStarted);
            var exists = File.Exists(PlaytestConsentFlag.FilePath);
            if (_consentExisted && !exists) PlaytestConsentFlag.Acknowledge();
            if (!_consentExisted && exists) File.Delete(PlaytestConsentFlag.FilePath);
        }

        [Test]
        public void 通過するまで開始を断り通過したら通す()
        {
            PlaytestTitleGates.SetStep(PlaytestTitleGateStep.NotStarted);
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test"));
            PlaytestTitleGates.SetStep(PlaytestTitleGateStep.Consent);
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test"));
            PlaytestTitleGates.SetStep(PlaytestTitleGateStep.CrashReport);
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test"));
            PlaytestTitleGates.SetStep(PlaytestTitleGateStep.Passed);
            Assert.IsTrue(PlaytestTitleGates.TryPassStart("test"));
        }

        [Test]
        public void 無人起動は異常終了があっても即通過し既読なら送る()
        {
            PlaytestConsentFlag.Acknowledge();
            var uploads = new RecordingUploadRequester();

            PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Unclean(), true, uploads, "batchMode").RunAsync(CancellationToken.None).Forget();

            Assert.AreEqual(PlaytestTitleGateStep.Passed, PlaytestTitleGates.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }

        [Test]
        public void 無人起動で未読なら通過するが持ち越しを送らない()
        {
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
            var uploads = new RecordingUploadRequester();

            PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Clean(), true, uploads, "batchMode").RunAsync(CancellationToken.None).Forget();

            Assert.AreEqual(PlaytestTitleGateStep.Passed, PlaytestTitleGates.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount);
        }

        [Test]
        public void 対話起動で未読なら同意から始める()
        {
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
            var uploads = new RecordingUploadRequester();

            PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Clean(), true, uploads, null).RunAsync(CancellationToken.None).Forget();

            Assert.AreEqual(PlaytestTitleGateStep.Consent, PlaytestTitleGates.Step.Value);
            Assert.AreEqual(0, uploads.RequestCount);
            Assert.IsFalse(PlaytestTitleGates.TryPassStart("test"));
        }

        [Test]
        public void 対話起動で既読かつ異常終了なら確認から始める()
        {
            PlaytestConsentFlag.Acknowledge();
            var uploads = new RecordingUploadRequester();

            PlaytestTitleGates.Compose(TestPreviousSessionArtifacts.Unclean(), true, uploads, null).RunAsync(CancellationToken.None).Forget();

            Assert.AreEqual(PlaytestTitleGateStep.CrashReport, PlaytestTitleGates.Step.Value);
            Assert.AreEqual(1, uploads.RequestCount);
        }
    }
}
```

（`RecordingUploadRequester` は `Client.Tests.PlaytestReceiver` の internal 既存ダブル、`RecordingCrashBundleWriter`・`TestPreviousSessionArtifacts` は `Client.Tests.BugReport` の既存ダブル。）

- [ ] **Step 7: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0（`Client.WebUiHost` に `Client.WebUiHost.Game.Playtest` への参照が残っていないこと）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.(Playtest\.TitleGates\.|EventMode\.|WebUi\.WireContractTest)"` → 全件 PASS
Run: `git grep -n "Client.WebUiHost.Game.Playtest\|PlaytestStartGates\b\|PlaytestGateBinder\|StartGateTopics.Consent\|StartGateTopics.CrashReport" -- moorestech_client/Assets/Scripts` → 0 件

- [ ] **Step 8: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(playtest): タイトルのゲート本体をClient.Starterに置き、WebUI側のC#開始ゲートを撤去する (ADR 0065)"
```

---

## Task 4: タイトルの uGUI ポップアップと開始経路の関所（Client.MainMenu と MainMenu シーン）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/Playtest/PlaytestLaunchGateView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.MainMenu/Playtest/PlaytestConsentPopup.cs`
- Create: `moorestech_client/Assets/Scripts/Client.MainMenu/Playtest/CrashReportPopup.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/StartLocal.cs:19-30`
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/ConnectServer.cs:36-44`
- Modify（uloop 経由のみ）: `moorestech_client/Assets/Scenes/Game/MainMenu.unity`

**Interfaces:**
- Consumes: Task 3 の `PlaytestTitleGates.Begin / Step / TryPassStart`、`PlaytestTitleGateSequence.AcknowledgeConsent / RespondCrashReportAsync`、`PlaytestTitleGateStep`、`PlaytestConsentResult`、`CrashReportResponseResult`。文言キー `LocalizationKeys.Ui.Playtest.Consent.{Title,Body,Agree}`、`LocalizationKeys.Ui.Playtest.CrashGate.{Title,Body,Placeholder,Send,Skip}`、`LocalizationKeys.Ui.Playtest.Gate.RespondFailed`（`Localization/localization.csv` 286〜294行の既存キー）。
- Produces: `PlaytestConsentPopup.Initialize(PlaytestTitleGateSequence)`・`SetVisible(bool)`、`CrashReportPopup.Initialize(PlaytestTitleGateSequence)`・`SetVisible(bool)`（Assembly-CSharp。テストからは参照できないため、振る舞いの正しさは Task 3 のテストと Task 6 の実機通しで担保する）。

- [ ] **Step 1: `PlaytestConsentPopup` を作る**

```csharp
using Client.Localization;
using Client.Starter.Playtest.TitleGates;
using Mooresmaster.Localization.Generated;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu.Playtest
{
    // 初回だけ出る参加同意の表示。文言は既存キーのまま、了解は段階の持ち主へ渡すだけ（ADR 0061・0065）
    // The first-boot consent notice; the wording keeps the existing keys and the acknowledgement is only handed to the step owner (ADR 0061, 0065)
    public class PlaytestConsentPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button agreeButton;
        [SerializeField] private TMP_Text agreeButtonText;

        private PlaytestTitleGateSequence _sequence;

        public void Initialize(PlaytestTitleGateSequence sequence)
        {
            _sequence = sequence;
            agreeButton.onClick.AddListener(Acknowledge);
        }

        // 表示のたびに文言を引き直す。タイトルの言語設定を変えた後に出ても現在の言語で読める
        // The texts are resolved on every show, so it reads in the current language even after the title's language setting changed
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                titleText.text = Localize.Get(LocalizationKeys.Ui.Playtest.Consent.Title);
                bodyText.text = Localize.Get(LocalizationKeys.Ui.Playtest.Consent.Body);
                agreeButtonText.text = Localize.Get(LocalizationKeys.Ui.Playtest.Consent.Agree);
            }
            gameObject.SetActive(visible);
        }

        private void Acknowledge()
        {
            var result = _sequence.AcknowledgeConsent();
            if (result != PlaytestConsentResult.Acknowledged) Debug.LogWarning($"[PlaytestTitleGates] consent button pressed but the gate answered {result}");
        }
    }
}
```

- [ ] **Step 2: `CrashReportPopup` を作る**

```csharp
using Client.Localization;
using Client.Starter.Playtest.TitleGates;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.MainMenu.Playtest
{
    // 前回異常終了の確認。説明を添えて送る／送らないを選ばせ、書けなかったときは閉じずにその旨を出す（ADR 0061・0065）
    // The previous-crash confirmation: send with a description or decline, and on a failed write stay open and say so (ADR 0061, 0065)
    public class CrashReportPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_InputField descriptionInput;
        [SerializeField] private TMP_Text descriptionPlaceholderText;
        [SerializeField] private Button sendButton;
        [SerializeField] private TMP_Text sendButtonText;
        [SerializeField] private Button skipButton;
        [SerializeField] private TMP_Text skipButtonText;
        [SerializeField] private TMP_Text statusText;

        private PlaytestTitleGateSequence _sequence;

        public void Initialize(PlaytestTitleGateSequence sequence)
        {
            _sequence = sequence;
            sendButton.onClick.AddListener(() => RespondAsync(true).Forget());
            skipButton.onClick.AddListener(() => RespondAsync(false).Forget());
        }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                titleText.text = Localize.Get(LocalizationKeys.Ui.Playtest.CrashGate.Title);
                bodyText.text = Localize.Get(LocalizationKeys.Ui.Playtest.CrashGate.Body);
                descriptionPlaceholderText.text = Localize.Get(LocalizationKeys.Ui.Playtest.CrashGate.Placeholder);
                sendButtonText.text = Localize.Get(LocalizationKeys.Ui.Playtest.CrashGate.Send);
                skipButtonText.text = Localize.Get(LocalizationKeys.Ui.Playtest.CrashGate.Skip);
                statusText.text = "";
            }
            gameObject.SetActive(visible);
        }

        private async UniTask RespondAsync(bool send)
        {
            // 応答中は両ボタンを閉じる。箱の書き出し（数十MBの同期コピー）の間に二重に押させない
            // Both buttons close while answering, so the box write (a synchronous copy of tens of MB) cannot be pressed twice
            SetButtonsInteractable(false);
            statusText.text = "";

            // 書き出しが例外で抜けてもボタンを戻す。戻さないと「送らない」も押せず確認から出られない
            // The buttons come back even if the write throws; otherwise "do not send" stays disabled and the confirmation can never be left
            try
            {
                var result = await _sequence.RespondCrashReportAsync(send, descriptionInput.text);
                if (result == CrashReportResponseResult.WriteFailed) statusText.text = Localize.Get(LocalizationKeys.Ui.Playtest.Gate.RespondFailed);
                if (result == CrashReportResponseResult.AlreadyResponded) Debug.LogWarning("[PlaytestTitleGates] crash confirmation pressed after it was already answered");
            }
            finally
            {
                SetButtonsInteractable(true);
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            sendButton.interactable = interactable;
            skipButton.interactable = interactable;
        }
    }
}
```

- [ ] **Step 3: `PlaytestLaunchGateView` を合成ルートにする**

```csharp
using System;
using Client.Game.InGame.BugReport.Submit;
using Client.Localization;
using Client.MainMenu.PopUp;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Gate;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Upload;
using Client.Starter.Playtest.TitleGates;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.MainMenu.Playtest
{
    // タイトルの合成ルート。照合結果を購読して理由を出し、通ったらタイトルのゲート（同意・前回異常終了の確認）を始めて段階をポップアップへ映す（ADR 0065）
    // The title's composition root: mirrors the launch verdict, and once it passes begins the title gates (consent, previous-crash confirmation) and reflects their step onto the popups (ADR 0065)
    public class PlaytestLaunchGateView : MonoBehaviour
    {
        [SerializeField] private ServerConnectPopup messagePopup;
        [SerializeField] private PlaytestConsentPopup consentPopup;
        [SerializeField] private CrashReportPopup crashReportPopup;

        private IPlaytestUploadRequester _uploadRequester;

        private void Start()
        {
            // MainMenuにはDIコンテナが無いので、ここを合成ルートとして受け口と走行役を組む
            // The MainMenu scene has no DI container, so this is the composition root for the receiver client and the runner
            var receiver = new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl);
            _uploadRequester = new PlaytestUploadRunner(receiver, PlaytestOutboxDirectories.FromGameSystemPaths());

            PlaytestLaunchGate.Current.Subscribe(Show).AddTo(this);
            PlaytestLaunchGate.EvaluateAsync(new PlaytestSteamTicketProvider(), receiver, DateTime.UtcNow, destroyCancellationToken).Forget();
        }

        private void Show(PlaytestGateResult result)
        {
            if (result.Status == PlaytestGateStatus.NotEvaluated) return;
            if (result.IsBlocked)
            {
                messagePopup.SetText(Localize.Get(result.ReasonKey));
                return;
            }

            // 照合しない起動（Editor・自作ビルド）は待ち文言を出していないので閉じない
            // A launch that is never checked showed no waiting message, so there is nothing to close
            if (result.Status == PlaytestGateStatus.Allowed) messagePopup.gameObject.SetActive(false);
            BeginTitleGates(result);
        }

        private void BeginTitleGates(PlaytestGateResult result)
        {
            // 初期化失敗でタイトルへ戻った再訪では始め直さない。退避と確認は起動1回に1度（ADR 0060 裁定5）
            // A revisit after a failed initialization does not restart them; salvage and confirmations happen once per boot (ADR 0060 adjudication 5)
            if (PlaytestTitleGates.Step.Value != PlaytestTitleGateStep.NotStarted)
            {
                Debug.Log($"[PlaytestTitleGates] already {PlaytestTitleGates.Step.Value}; the title gates are not restarted on this title visit");
                return;
            }

            var sequence = PlaytestTitleGates.Begin(result, _uploadRequester, destroyCancellationToken);
            consentPopup.Initialize(sequence);
            crashReportPopup.Initialize(sequence);

            // 表示は段階を映すだけ。購読はこの常時有効な合成ルートが持つ（非アクティブのポップアップにAddToしない）
            // The display only mirrors the step; this always-active root owns the subscription (never AddTo an inactive popup)
            sequence.Step.Subscribe(step =>
            {
                consentPopup.SetVisible(step == PlaytestTitleGateStep.Consent);
                crashReportPopup.SetVisible(step == PlaytestTitleGateStep.CrashReport);
            }).AddTo(this);
        }
    }
}
```

- [ ] **Step 4: 開始経路に関所を足す**

`StartLocal.StartLocalGame` を次にする（`using Client.Starter.Playtest.TitleGates;` を足す）:

```csharp
        private void StartLocalGame()
        {
            // 開始可否と拒否理由の文言はゲートが決める。ここは表示するだけ
            // The gate decides whether to start and resolves the refusal text; this only displays it
            if (!PlaytestLaunchGate.TryPassStart(nameof(StartLocal), out var denyReasonText))
            {
                messagePopup.SetText(denyReasonText);
                return;
            }

            // 同意と前回異常終了の確認に答えるまで開始しない。拒否理由はゲートがログへ出す（ADR 0065）
            // Nothing starts until the consent and previous-crash confirmation are answered; the gate logs the refusal (ADR 0065)
            if (!PlaytestTitleGates.TryPassStart(nameof(StartLocal))) return;

            LocalGameLauncher.StartLocalGame();
        }
```

`ConnectServer.Connect` の照合チェック（38〜44行目）の直後に足す（`using Client.Starter.Playtest.TitleGates;` を足す）:

```csharp

            // 接続先がリモートでもタイトルの確認はタイトル全体の関所。答えるまで接続しない（ADR 0065）
            // The title confirmations gate the whole title even for a remote server; nothing connects until they are answered (ADR 0065)
            if (!PlaytestTitleGates.TryPassStart(nameof(Connect))) return;
```

- [ ] **Step 5: コンパイルする（シーン編集の前にフィールドを存在させる）**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0

- [ ] **Step 6: MainMenu シーンにポップアップ2枚を足す（uloop execute-dynamic-code）**

Editor が PlayMode でないこと・開いているシーンに未保存の変更が無いことを先に確かめる（スニペットが検査して拒否する）。スニペットを scratchpad に `title-gate-popups.cs` として保存し、`uloop execute-dynamic-code --project-path ./moorestech_client --code-file <path>` で実行する:

```csharp
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

if (EditorApplication.isPlaying) return "PlayMode 中は編集しない";
for (var i = 0; i < EditorSceneManager.sceneCount; i++)
{
    if (EditorSceneManager.GetSceneAt(i).isDirty) return "未保存のシーンがあるため中止: " + EditorSceneManager.GetSceneAt(i).path;
}

var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game/MainMenu.unity", OpenSceneMode.Single);
var roots = scene.GetRootGameObjects();
var canvas = roots.First(g => g.name == "Canvas").transform;
var gateView = roots.First(g => g.name == "PlaytestLaunchGate");
if (canvas.Find("PlaytestConsentPopup") != null || canvas.Find("CrashReportPopup") != null) return "既にポップアップがある。二重実行しない";

var resetPopup = canvas.Find("ResetAllDataConfirmPopup").gameObject;
var serverIp = canvas.Find("TitleMenu/ServerIp").gameObject;
var localizeType = Type.GetType("Client.Localization.TextMeshProLocalize, Client.Localization");
var consentType = Type.GetType("Client.MainMenu.Playtest.PlaytestConsentPopup, Assembly-CSharp");
var crashType = Type.GetType("Client.MainMenu.Playtest.CrashReportPopup, Assembly-CSharp");
var viewType = Type.GetType("Client.MainMenu.Playtest.PlaytestLaunchGateView, Assembly-CSharp");
if (localizeType == null || consentType == null || crashType == null || viewType == null) return "型が見つからない（コンパイル未完了）";

// ---- 同意ポップアップ: 全面の背景とPanelを持つ ResetAllDataConfirmPopup を複製する ----
var consent = UnityEngine.Object.Instantiate(resetPopup, canvas);
consent.name = "PlaytestConsentPopup";
UnityEngine.Object.DestroyImmediate(consent.GetComponent("ResetAllDataConfirmPopup"));
foreach (var localize in consent.GetComponentsInChildren(localizeType, true)) UnityEngine.Object.DestroyImmediate(localize);
var consentPanel = (RectTransform)consent.transform.Find("Panel");
consentPanel.sizeDelta = new Vector2(900, 520);
var consentBody = consentPanel.Find("Message").GetComponent<TextMeshProUGUI>();
var consentTitle = UnityEngine.Object.Instantiate(consentBody.gameObject, consentPanel).GetComponent<TextMeshProUGUI>();
consentTitle.name = "Title";
consentTitle.fontStyle = FontStyles.Bold;
UnityEngine.Object.DestroyImmediate(consentPanel.Find("CancelButton").gameObject);
var agree = consentPanel.Find("ConfirmButton");
agree.name = "AgreeButton";
var placements = new (RectTransform rect, Vector2 position, Vector2 size)[]
{
    ((RectTransform)consentTitle.transform, new Vector2(0, 210), new Vector2(820, 60)),
    ((RectTransform)consentBody.transform, new Vector2(0, 20), new Vector2(820, 300)),
    ((RectTransform)agree, new Vector2(0, -200), ((RectTransform)agree).sizeDelta),
};
foreach (var placement in placements)
{
    placement.rect.anchorMin = placement.rect.anchorMax = placement.rect.pivot = new Vector2(0.5f, 0.5f);
    placement.rect.anchoredPosition = placement.position;
    placement.rect.sizeDelta = placement.size;
}
consentBody.enableWordWrapping = true;
var consentComponent = consent.AddComponent(consentType);
var consentSo = new SerializedObject(consentComponent);
consentSo.FindProperty("titleText").objectReferenceValue = consentTitle;
consentSo.FindProperty("bodyText").objectReferenceValue = consentBody;
consentSo.FindProperty("agreeButton").objectReferenceValue = agree.GetComponent<Button>();
consentSo.FindProperty("agreeButtonText").objectReferenceValue = agree.GetComponentInChildren<TextMeshProUGUI>(true);
consentSo.ApplyModifiedPropertiesWithoutUndo();
consent.SetActive(false);

// ---- 前回異常終了の確認: 同じ複製に説明入力（ServerIp の複製）と状態行を足す ----
var crash = UnityEngine.Object.Instantiate(resetPopup, canvas);
crash.name = "CrashReportPopup";
UnityEngine.Object.DestroyImmediate(crash.GetComponent("ResetAllDataConfirmPopup"));
foreach (var localize in crash.GetComponentsInChildren(localizeType, true)) UnityEngine.Object.DestroyImmediate(localize);
var crashPanel = (RectTransform)crash.transform.Find("Panel");
crashPanel.sizeDelta = new Vector2(900, 560);
var crashBody = crashPanel.Find("Message").GetComponent<TextMeshProUGUI>();
var crashTitle = UnityEngine.Object.Instantiate(crashBody.gameObject, crashPanel).GetComponent<TextMeshProUGUI>();
crashTitle.name = "Title";
crashTitle.fontStyle = FontStyles.Bold;
var crashStatus = UnityEngine.Object.Instantiate(crashBody.gameObject, crashPanel).GetComponent<TextMeshProUGUI>();
crashStatus.name = "Status";
crashStatus.color = new Color(1f, 0.45f, 0.45f);
crashStatus.text = "";
var description = UnityEngine.Object.Instantiate(serverIp, crashPanel);
description.name = "Description";
foreach (var localize in description.GetComponentsInChildren(localizeType, true)) UnityEngine.Object.DestroyImmediate(localize);
var descriptionInput = description.GetComponent<TMP_InputField>();
descriptionInput.contentType = TMP_InputField.ContentType.Standard;
descriptionInput.lineType = TMP_InputField.LineType.MultiLineNewline;
descriptionInput.characterLimit = 0;
descriptionInput.text = "";
var send = crashPanel.Find("ConfirmButton");
send.name = "SendButton";
var skip = crashPanel.Find("CancelButton");
skip.name = "SkipButton";
var crashPlacements = new (RectTransform rect, Vector2 position, Vector2 size)[]
{
    ((RectTransform)crashTitle.transform, new Vector2(0, 230), new Vector2(820, 60)),
    ((RectTransform)crashBody.transform, new Vector2(0, 140), new Vector2(820, 120)),
    ((RectTransform)description.transform, new Vector2(0, 10), new Vector2(700, 110)),
    ((RectTransform)crashStatus.transform, new Vector2(0, -80), new Vector2(820, 40)),
    ((RectTransform)send, new Vector2(-160, -200), ((RectTransform)send).sizeDelta),
    ((RectTransform)skip, new Vector2(160, -200), ((RectTransform)skip).sizeDelta),
};
foreach (var placement in crashPlacements)
{
    placement.rect.anchorMin = placement.rect.anchorMax = placement.rect.pivot = new Vector2(0.5f, 0.5f);
    placement.rect.anchoredPosition = placement.position;
    placement.rect.sizeDelta = placement.size;
}
crashBody.enableWordWrapping = true;
var crashComponent = crash.AddComponent(crashType);
var crashSo = new SerializedObject(crashComponent);
crashSo.FindProperty("titleText").objectReferenceValue = crashTitle;
crashSo.FindProperty("bodyText").objectReferenceValue = crashBody;
crashSo.FindProperty("descriptionInput").objectReferenceValue = descriptionInput;
crashSo.FindProperty("descriptionPlaceholderText").objectReferenceValue = descriptionInput.placeholder;
crashSo.FindProperty("sendButton").objectReferenceValue = send.GetComponent<Button>();
crashSo.FindProperty("sendButtonText").objectReferenceValue = send.GetComponentInChildren<TextMeshProUGUI>(true);
crashSo.FindProperty("skipButton").objectReferenceValue = skip.GetComponent<Button>();
crashSo.FindProperty("skipButtonText").objectReferenceValue = skip.GetComponentInChildren<TextMeshProUGUI>(true);
crashSo.FindProperty("statusText").objectReferenceValue = crashStatus;
crashSo.ApplyModifiedPropertiesWithoutUndo();
crash.SetActive(false);

// ---- 合成ルートへ配線して保存する ----
var viewSo = new SerializedObject(gateView.GetComponent(viewType));
viewSo.FindProperty("consentPopup").objectReferenceValue = consentComponent;
viewSo.FindProperty("crashReportPopup").objectReferenceValue = crashComponent;
viewSo.ApplyModifiedPropertiesWithoutUndo();

EditorSceneManager.MarkSceneDirty(scene);
EditorSceneManager.SaveScene(scene);
return "ok";
```

CS8421 等の変換制約エラーが出たら `uloop-execute-dynamic-code` スキルの `references/transpiler-constraints.md` に従ってタプル配列を個別代入へ直す（振る舞いは変えない）。

- [ ] **Step 7: 配置を確かめる**

Run: `uloop find-game-objects --project-path ./moorestech_client --name-pattern "PlaytestConsentPopup|CrashReportPopup" --search-mode Regex --include-inactive`（どちらも Canvas 直下・非アクティブ・`PlaytestConsentPopup` / `CrashReportPopup` コンポーネント付き）
Run: `git diff --stat moorestech_client/Assets/Scenes/Game/MainMenu.unity`（追加が主体で、既存オブジェクトの変更は `PlaytestLaunchGate` の2フィールドだけ。`ResetAllDataConfirmPopup`・`ServerIp` の元オブジェクトに差分が無い）
見た目の確認は Task 6 のスクリーンショットで行い、はみ出し・重なりがあれば同じ手順（execute-dynamic-code）で `anchoredPosition` / `sizeDelta` だけを直して保存し直す。

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.MainMenu moorestech_client/Assets/Scenes/Game/MainMenu.unity
git commit -m "feat(title): 参加同意と前回異常終了の確認をタイトルのuGUIポップアップで出し、答えるまで開始しない (ADR 0065)"
```

---

## Task 5: WebUI の同意・異常終了ゲートを撤去する（webui・ワイヤ契約・設計文書）

**Files:**
- Delete: `moorestech_web/webui/src/features/playtestGate/`（全ファイル）、`moorestech_web/webui/e2e/tests/system/playtestGate.spec.ts`
- Modify: `moorestech_web/webui/src/app/App.tsx:24,164-170`
- Modify: `moorestech_web/webui/src/app/startGates/useFrontmostStartGate.ts`
- Modify: `moorestech_web/webui/src/app/startGates/pickFrontmostStartGate.test.ts`
- Modify: `moorestech_web/webui/src/app/tokens.css:147-150`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts:54-55,92-93`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts:40-45`
- Modify: `moorestech_web/webui/src/bridge/contract/validators.ts:17-18,54-55`
- Modify: `moorestech_web/webui/src/bridge/contract/startGateContract.test.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts:69-70,118-119`
- Modify: `moorestech_web/webui/src/bridge/transport/actions.ts:5-13,33-38,46-55`
- Modify: `moorestech_web/webui/src/bridge/transport/actions.test.ts:92-114`
- Modify: `moorestech_web/webui/src/bridge/transport/protocol.ts:6,78-83,102-103`
- Modify: `moorestech_web/webui/src/shared/i18n/preDictionaryText.ts:6-7,13-27`
- Modify: `moorestech_web/webui/src/shared/ui/FullScreenGate/useGateAnswer.test.ts:22-23,56`
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures/startGateFixtures.ts`、`moorestech_web/webui/e2e/mock-host/topics/topicControls.ts:189-192`、`moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts:44-45`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/error_codes.json:43-46`、`moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs:134-136`
- Modify: `.agents/skills/webui-design/SKILL.md:108,557-580,592-611,613`

**Interfaces:**
- Consumes: Task 3 で C# 側から `playtest.consent_gate` / `playtest.crash_report_gate` topic と `playtest.consent.acknowledge` / `playtest.crash_report.respond` action が消えていること。
- Produces: WebUI の開始ゲートは `eventLanguage` 1枚。`GATE_ALREADY_ANSWERED_ERRORS` は `event_mode.select_language` だけ。

- [ ] **Step 1: 機能ディレクトリと e2e spec を消す**

```bash
git rm -r moorestech_web/webui/src/features/playtestGate
git rm moorestech_web/webui/e2e/tests/system/playtestGate.spec.ts
```

- [ ] **Step 2: app 層を言語ゲートだけにする**

`App.tsx`: 24行目 `import { CrashReportGate, PlaytestConsentGate } from "@/features/playtestGate";` を削除。164〜170行目を次にする:

```tsx
      {/* 開始ゲート（出展モードの言語選択）。再接続表示より前へ出し、待機中の操作を全て塞ぐ */}
      {/* The start gate (event mode's language selection); it sits ahead of the reconnect overlay and blocks every input while waiting */}
      {/* プレイテストの同意と前回異常終了の確認はタイトル（uGUI）へ移した（ADR 0065） */}
      {/* The playtest consent and previous-crash confirmation moved to the title (uGUI) (ADR 0065) */}
      <EventLanguageGate visible={frontmostStartGate === "eventLanguage"} />
```

`useFrontmostStartGate.ts` を次にする:

```ts
// 開始ゲートの待機を購読し、今見せる1枚を決める。順序はC#がpayloadのprecedenceで配り、ここは比べるだけ
// Subscribes to the start gates' waiting and picks the one to show; C# ships the order as precedence and this only compares
import { Topics, useTopicSelector } from "@/bridge";

export type StartGate = "eventLanguage";

// topicは同形。precedenceが小さいほど先に答えさせる
// Topics share this shape; a smaller precedence is answered first
type StartGateWaiting = { waiting: boolean; precedence: number };

export function useFrontmostStartGate(): StartGate | null {
  const eventLanguage = useTopicSelector(Topics.eventLanguageGate, (data) => data);
  return pickFrontmostStartGate({ eventLanguage });
}

// 待機中のうちprecedence最小の1枚。ゲートは無条件マウントなので、同時に待っても重ねて描かせない
// The waiting gate with the smallest precedence; gates mount unconditionally, so simultaneous waits never stack
export function pickFrontmostStartGate(gates: Readonly<Record<StartGate, StartGateWaiting | null>>): StartGate | null {
  let frontmost: { gate: StartGate; precedence: number } | null = null;
  for (const gate of Object.keys(gates) as StartGate[]) {
    const data = gates[gate];
    if (data === null || !data.waiting) continue;
    if (frontmost === null || data.precedence < frontmost.precedence) frontmost = { gate, precedence: data.precedence };
  }
  return frontmost?.gate ?? null;
}
```

`pickFrontmostStartGate.test.ts` を次にする:

```ts
// 順序の正本はC#のprecedence。Web側は比べるだけで、並び順の知識を持たない
// C#'s precedence owns the order; the Web side only compares and holds no knowledge of the sequence
import { describe, expect, it } from "vitest";
import { pickFrontmostStartGate } from "./useFrontmostStartGate";

describe("pickFrontmostStartGate", () => {
  it("待っていなければ何も選ばない", () => {
    expect(pickFrontmostStartGate({ eventLanguage: { waiting: false, precedence: 0 } })).toBeNull();
    expect(pickFrontmostStartGate({ eventLanguage: null })).toBeNull();
  });

  it("待っている1枚を選ぶ", () => {
    expect(pickFrontmostStartGate({ eventLanguage: { waiting: true, precedence: 0 } })).toBe("eventLanguage");
  });
});
```

`tokens.css` の 147〜150行目（「前回異常終了ゲートの記述欄」のコメント2行と `--playtest-gate-textarea-width` / `--playtest-gate-textarea-height`）を削除する。

- [ ] **Step 3: bridge 契約からゲート2枚を外す**

- `payloadTypes.ts`: import の `CrashReportGateDataSchema,` と `PlaytestConsentGateDataSchema,`、型 `CrashReportGateData` と `PlaytestConsentGateData` の2行を削除。
- `schemas/ui.ts`: 40〜45行目を次にする:

```ts
// 開始ゲートの待機。precedence は C# の起動順を正本とする答えさせる順で、小さいほど先（出展モードの言語選択0）
// A start gate's waiting; precedence is the answer order owned by the C# boot order, smaller first (event mode's language selection is 0)
const StartGateWaitingSchema = z.object({ waiting: z.boolean(), precedence: z.number().int().nonnegative() });
export const EventLanguageGateDataSchema = StartGateWaitingSchema;
```

- `validators.ts`: import の `CrashReportGateDataSchema,`・`PlaytestConsentGateDataSchema,` と、表の `[Topics.crashReportGate]: ...`・`[Topics.consentGate]: ...` の2行を削除。
- `protocol.ts`: 6行目の import から `CrashReportGateData, PlaytestConsentGateData, ` を削除。78〜83行目（`crashReportGate` と `consentGate` とそれぞれのコメント2行）を削除。`TopicPayloads` の `[Topics.crashReportGate]: CrashReportGateData;`・`[Topics.consentGate]: PlaytestConsentGateData;` を削除。
- `actionContract.ts`: `ActionPayloads` の `"playtest.crash_report.respond": ...`・`"playtest.consent.acknowledge": ...` と、`ACTION_TYPES` の同名2行を削除。
- `actions.ts`: 5〜13行目を次にする:

```ts
// 全画面ゲートの「すでに応答済み」を表す拒否コード。ゲートはこの1表から文言を選ぶ
// The rejection code meaning "already answered" for a full-screen gate; the gate picks its copy from this single table
export const GATE_ALREADY_ANSWERED_ERRORS = {
  "event_mode.select_language": "already_selected",
} as const;

export type GateAnswerActionType = keyof typeof GATE_ALREADY_ANSWERED_ERRORS;
```

`BENIGN_ERRORS` から `"playtest.crash_report.respond"`・`"playtest.consent.acknowledge"` の2行を削除。`ACTION_TIMEOUTS_MS` の上のコメント「playtest.crash_report.respond は録画リング…」2行と、表の `"playtest.crash_report.respond": 120000,` を削除。

- `actions.test.ts`: `playtest.crash_report.respond も 120 秒の待ち時間で送る`（上のコメント2行ごと）を削除し、`dispatchActionOutcome は拒否理由と到達不能を区別して返す` を次にする:

```ts
  it("dispatchActionOutcome は拒否理由と到達不能を区別して返す", async () => {
    vi.spyOn(webSocketClient, "sendAction").mockResolvedValue({ ok: false, error: "already_selected" });
    expect(await dispatchActionOutcome("event_mode.select_language", { locale: "ja" }))
      .toEqual({ kind: "rejected", error: "already_selected" });

    vi.spyOn(webSocketClient, "sendAction").mockRejectedValue(new Error("timeout"));
    expect(await dispatchActionOutcome("event_mode.select_language", { locale: "ja" }))
      .toEqual({ kind: "unreachable", reason: "timeout" });

    vi.spyOn(webSocketClient, "sendAction").mockResolvedValue({ ok: true });
    expect(await dispatchActionOutcome("event_mode.select_language", { locale: "ja" })).toEqual({ kind: "accepted" });
  });
```

- `startGateContract.test.ts`: 20〜21行目（`Topics.consentGate` と `Topics.crashReportGate` の2行）を削除し、冒頭コメントの「開始ゲート3枚」を「開始ゲート」に直す。

- [ ] **Step 4: 辞書前文言・共有フックのテスト・mock host を直す**

- `preDictionaryText.ts`: 13〜27行目（`ui.playtest.consent.*`・`ui.playtest.crashGate.*`・「応答の結末は…」コメント2行・`ui.playtest.gate.*`）を削除。6〜7行目のコメントを次にする:

```ts
// 辞書を配る WebUiGameBinder より前に描かれる画面の文言。値はlocalization.csvのenglish / japanese / germanを併記したもの
// Copy for screens that render before WebUiGameBinder publishes the dictionary; each value joins the csv's english / japanese / german
```

- `useGateAnswer.test.ts`: 22行目を `const { answer } = useGateAnswer("event_mode.select_language", copy);`、23行目を `pressAnswer = () => answer({ locale: "ja" });`、56行目の期待値を `"[event_mode.select_language] gate closed before the answer settled: accepted"` にする。
- `e2e/mock-host/fixtures/startGateFixtures.ts` を次にする:

```ts
// 開始ゲートの答えさせる順。本物はC#の起動順が配るprecedenceで、mockは同じ値（言語0）を返す
// The start gates' answer order; the real host ships precedence from the C# boot order and the mock returns the same value (language 0)
export const StartGatePrecedence = {
  eventLanguage: 0,
} as const;
```

- `e2e/mock-host/topics/topicControls.ts` の 189〜192行目（`consentGateWaiting`・`consentGateClosed`・`crashReportGateWaiting`・`crashReportGateClosed`）を削除。
- `e2e/mock-host/topics/topicFixtures.ts` の 44〜45行目（`[Topics.crashReportGate]`・`[Topics.consentGate]`）を削除。

- [ ] **Step 5: ワイヤ契約の共有エラーコードからゲート専用コードを外す**

`error_codes.json` から `"already_responded"`・`"already_acknowledged"`・`"invalid_send"`・`"unknown_result"` を削除（直前の `"submit_in_flight",` の末尾カンマを JSON として正しく直す）。
`WireContractTest.cs` の 134〜136行目（コメント2行とコード列1行）を次にする:

```csharp
                // プレイ報告（plan G）: ポーズメニューの送信。同意と前回異常終了の確認はタイトル（uGUI）へ移り、WebUIのactionは無い（ADR 0065）
                // Play reports (plan G): the pause-menu send; the consent and crash confirmation moved to the title (uGUI) and have no WebUI action (ADR 0065)
                "empty_description", "invalid_kind", "bundle_write_failed", "no_capture_session", "capture_pending", "already_submitted", "submit_in_flight",
```

- [ ] **Step 6: 設計文書（webui-design）を直す**

`.agents/skills/webui-design/SKILL.md`:
- 108行目付近の「例外（前回異常終了の確認ゲート・§8.21）…」の項を削除する（前後の列挙の体裁を保つ）。
- §8.20 の「開始を止める全画面ゲート3枚（言語選択・プレイテスト同意・前回異常終了）」を「開始を止める全画面ゲート（現在は出展モードの言語選択1枚。プレイテストの同意と前回異常終了の確認は ADR 0065 でタイトルの uGUI へ移した）」に、「3トピック（…）を購読し」を「`event_mode.language_gate` を購読し」に、「言語0・同意1・前回異常終了2」を「言語0」に、「3ゲートが共有する」「3枚が共有する」を「全画面ゲートが共有する」に、「プレイテスト2枚は `L.ui.playtest.gate.*` を渡す」を削除する。
- §8.21 全体を次の2行に置き換える:

```markdown
## 8.21 （撤去）前回異常終了の確認ゲート・プレイテスト同意ゲート

- ADR 0065 でタイトル（MainMenu、uGUI）へ移した。WebUI 側の `features/playtestGate`・topic・action は存在しない。新たに WebUI で同種の確認を作らない（作り直すならメインメニュー作り変え `moorestech-zohw` と一緒に設計する）。
```

- §9 の「例外は §8.12 のスキット暗転・§8.20a の出展モード言語選択ゲート・§8.21 の前回異常終了確認ゲート・プレイテスト同意ゲートだけ」を「例外は §8.12 のスキット暗転・§8.20a の出展モード言語選択ゲートだけ」にする。

- [ ] **Step 7: webui の検証**

```bash
cd moorestech_web/webui
pnpm install --frozen-lockfile
pnpm exec tsc -b
pnpm exec tsc -p e2e/tsconfig.json --noEmit
pnpm lint
pnpm test
pnpm test:e2e
git grep -n "playtestGate\|consentGate\|crashReportGate\|playtest.consent\|playtest.crash_report\|playtest-gate-textarea" -- src e2e
```

Expected: tsc・lint・test・e2e がすべて成功。最後の grep は 0 件。e2e が毎回違う spec で落ちるならポート 5273 の衝突（別セッションの mock host）を疑い、`lsof -i :5273` で確かめてから再実行する。

- [ ] **Step 8: C# 側のワイヤ契約テスト**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.WebUi\.WireContractTest"` → PASS

- [ ] **Step 9: コミットする**

```bash
cd "$(git rev-parse --show-toplevel)"
git add -A moorestech_web/webui moorestech_client/Assets/Scripts/Client.Tests/WebUi .agents/skills/webui-design/SKILL.md
git commit -m "chore(webui): 同意と前回異常終了のWebUIゲートとbridge契約を撤去する (ADR 0065)"
```

---

## Task 6: unityプレイ録画テスト（タイトル経路の通し。unity-playmode-recorded-playtest スキル）

**Files:**
- （コード変更なし。成果物は scratchpad のスクリーンショット・ログと、本 plan の判断記録への結果転記）
- Modify: `docs/superpowers/plans/2026-09-20-playtest-identity-and-title-start-gates.md`（`## 判断記録（ADR）` へ結果を追記）

**Interfaces:**
- Consumes: Task 1〜5 の完成物。

**DSL を使わない理由:** プレイテストDSL（`Client.Playtest/PlaytestBoot.cs:42`）は `EditorSceneManager.playModeStartScene` を GameInitializer に固定して起動するため、タイトル（MainMenu）を通れない。タイトルは uGUI なので、`uloop simulate-mouse-ui`（EventSystem のレイキャスト経由。OS 入力ではないので unity-playmode-recorded-playtest の「OS入力禁止」に当たらない）でボタンを押す。ポップアップの全面背景がレイキャストを塞ぐことも同時に確かめられる。`simulate-keyboard` / `simulate-mouse-input` は使わない。レイキャストで押せない要素があった場合だけ、`execute-dynamic-code` で `Button.onClick.Invoke()` を呼び、その理由を判断記録に書く。録画の代わりに各段のスクリーンショットとログを証跡にする（DSL の録画器はシナリオ起動に結び付いており、タイトル起動では使えないため）。

- [ ] **Step 1: 前提を整える（Editor は本 worktree のもの、PlayMode 外、ErrorCount 0）**

```bash
uloop compile --project-path ./moorestech_client
```

`execute-dynamic-code` で、同意の既読フラグを退避し、死んだ pid の「開始印だけのセッション」を置いて前回異常終了を作り、出力先を控える:

```csharp
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Game.Paths;

var flag = PlaytestConsentFlag.FilePath;
if (File.Exists(flag)) File.Move(flag, flag + ".e2e-backup");
const int deadPid = 999999;
if (System.Diagnostics.Process.GetProcesses().Any(p => p.Id == deadPid)) return "pid 999999 が生きている。別の番号にする";
CleanExitMarker.MarkSessionStarted(deadPid, "session_1", new SessionOriginSnapshot(null, BuildOriginReading.Editor()));
return $"outbox={GameSystemPaths.BugReportOutboxDirectory} flagBackedUp={File.Exists(flag + ".e2e-backup")}";
```

outbox の現状を控える: `ls "<outbox>" > <scratchpad>/e2e/outbox-before.txt`

- [ ] **Step 2: タイトルから再生する**

`execute-dynamic-code`:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Game/MainMenu.unity");
return EditorSceneManager.playModeStartScene.name;
```

```bash
uloop clear-console --project-path ./moorestech_client
uloop control-play-mode --project-path ./moorestech_client --action Play
```

5 秒待ってから `uloop screenshot --project-path ./moorestech_client --capture-mode rendering --annotate-elements --elements-only` → `PlaytestConsentPopup` が見え、`AgreeButton` が注釈に出る。`uloop get-logs --project-path ./moorestech_client` に `[PlaytestTitleGates] step Consent` がある。

- [ ] **Step 3: 確認中の Play locally が開始しないこと（意図した拒否。合否区間の外）**

`execute-dynamic-code` で押す（背景に塞がれて `simulate-mouse-ui` では届かないのが正しいため、関所そのものを叩く）:

```csharp
using UnityEngine;
using UnityEngine.UI;
GameObject.Find("Canvas/TitleMenu/Start local game").GetComponent<Button>().onClick.Invoke();
return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
```

Expected: 戻り値 `MainMenu`、ログに `[PlaytestTitleGates] StartLocal refused: the title gates are at Consent`。あわせて `simulate-mouse-ui --action Click` を `Start local game` の座標へ撃ち、何も起きない（ポップアップの背景がレイキャストを塞ぐ）ことを確かめる。確認後に `uloop clear-console` する（ここから先が合否判定区間）。

- [ ] **Step 4: 同意 → 前回異常終了の確認 → 送る**

1. `simulate-mouse-ui --action Click` を `AgreeButton` の座標へ → スクリーンショットで `CrashReportPopup` が出ている、ログに `step CrashReport`、既読フラグが出来ている。
2. 説明の入力は `execute-dynamic-code` で行う（キーボード注入は使わない）:

```csharp
using TMPro;
using UnityEngine;
GameObject.Find("Canvas/CrashReportPopup/Panel/Description").GetComponent<TMP_InputField>().text = "e2e: タイトルの前回異常終了確認";
return "ok";
```

3. `simulate-mouse-ui --action Click` を `SendButton` の座標へ → ログに `step Passed`、outbox に新しい箱が1つ増え、その `manifest.json` の `kind` が `crash`、`description` が上の文字列（`ls` と Step 1 の控えを比べて新しいディレクトリを特定し `cat manifest.json`）。
4. スクリーンショットを各段で `<scratchpad>/e2e/` へ保存する（同意・確認・通過後）。

- [ ] **Step 5: Play locally からゲーム開始まで**

`simulate-mouse-ui --action Click` を `Start local game` の座標へ → 最大 120 秒、`execute-dynamic-code` で `UnityEngine.SceneManagement.SceneManager.GetActiveScene().name` が `MainGame` になり、ログに `GameInitialized` 相当の初期化完了が出るまで待つ（前景待ち。バックグラウンドで放置しない）。スクリーンショットを保存。

- [ ] **Step 6: 合否判定（警告・拒否語がゼロ）**

```bash
uloop get-logs --project-path ./moorestech_client --log-type Warning --max-count 1000 > <scratchpad>/e2e/warnings.txt
uloop get-logs --project-path ./moorestech_client --log-type Error --max-count 1000 > <scratchpad>/e2e/errors.txt
uloop get-logs --project-path ./moorestech_client --max-count 1000 > <scratchpad>/e2e/all.txt
grep -n -i -E "refused|拒否|fail|失敗|書けなかった|could not|できません|not requested|lacked|malformed|Exception|無人起動" <scratchpad>/e2e/all.txt
```

合格条件: Step 3 の `clear-console` 以降の区間で Warning・Error が 0 件、上の grep が 0 件（`upload not requested` は開発者モードで出る Debug.Log だが、Editor はここで必ず開発者モードなので**この語だけは 1 件以上出るのが正しい**。それ以外の語は 0 件）。0 件でない語があれば1件ずつ出所を調べ、本変更に起因するものは直して Step 1 からやり直す。本変更と無関係と示せたものは Task 8 で起票する（「無関係」を合格理由にせず、起票番号を判断記録に並べる）。

- [ ] **Step 7: 既読・正常終了の起動ではポップアップが出ないこと**

PlayMode を止め（`uloop control-play-mode --action Stop`）、もう一度 Step 2 の Play だけを行う → ポップアップが出ず、ログに `step Passed`（`step Consent` / `step CrashReport` が無い）。`Start local game` をクリックしてゲームが始まる。

- [ ] **Step 8: 後片付け**

PlayMode を止め、`execute-dynamic-code` で戻す:

```csharp
using System.IO;
using Client.Game.InGame.BugReport.Playtest;
using UnityEditor.SceneManagement;
EditorSceneManager.playModeStartScene = null;
var flag = PlaytestConsentFlag.FilePath;
if (File.Exists(flag + ".e2e-backup"))
{
    if (File.Exists(flag)) File.Delete(flag);
    File.Move(flag + ".e2e-backup", flag);
}
return "restored";
```

Step 4 で出来た箱のディレクトリを outbox から削除する（開発者の rsync 経路へ検証用の箱を流さない）。

- [ ] **Step 9: 結果を判断記録へ転記してコミット**

`## 判断記録（ADR）` に「Task 6 実施: 日時・各段のスクリーンショットのパス・合否・押下経路（simulate-mouse-ui で押せた要素／onClick.Invoke にした要素とその理由）」を追記する。

```bash
git add docs/superpowers/plans/2026-09-20-playtest-identity-and-title-start-gates.md
git commit -m "docs(plan): タイトル経路のunityプレイ録画テストの結果を記録する"
```

---

## Task 7: 検証機 smoke（Windows）をやり直す（配布を伴うためユーザー承認のゲート付き）

**Files:**
- Modify: `scripts/playtest/README.md:105-107`
- Modify: `docs/superpowers/plans/2026-09-20-playtest-identity-and-title-start-gates.md`（判断記録へ結果）

**Interfaces:**
- Consumes: Task 1〜6 の完成物（push 済みの本ブランチ）。

**前提の読み:** `scripts/playtest/release-playtest.sh` は ①使い捨て worktree で Windows 配布ビルドを焼き ②`steam/app_build_playtest.vdf` の `"setlive" "playtest"` で Steam の `playtest` ブランチへ即時公開し ③`verify-on-windows.sh` で検証機 smoke（phase1/phase2）を回す。検証機は Steam 経由（`steam.exe -applaunch`）でしか起動できないため、**smoke をやり直すには `playtest` ブランチへの公開が避けられない**（パスワードを知るテスターにも配られる）。配布は別途ユーザー判断なので、承認が無い限り ② 以降へ進まない。

- [ ] **Step 1: README の検証機準備を直す**

`scripts/playtest/README.md` 105〜107行目を次にする:

```markdown
7. 初回だけ手でゲームを起動し、タイトルに出る同意告知（consent notice）の「了解」を押しておく（`PlaytestConsentFlag`。未承諾のまま
   自動運転すると起動前提の確認で理由付きに失敗する。ADR 0065 で同意はタイトルの uGUI に移り、ワールドに入る前に押せる）。
   あわせて Steam のオーバーレイ初期化と受け口の起動時照合が通ることを確認する。
```

```bash
git add scripts/playtest/README.md
git commit -m "docs(playtest): 検証機の同意既読化をタイトルで行う手順に直す (ADR 0065)"
```

- [ ] **Step 2: 公開を伴わない Windows ビルドの確認（承認不要）**

本 worktree とは別の使い捨て worktree で焼くのが release-playtest.sh の流儀なので、ここではコンパイルが通ることだけを見る: `uloop compile --project-path ./moorestech_client` が ErrorCount 0 で、Task 8 の前に全タスクがコミット済みであること。Windows ターゲットでの成果物確認は Step 4 の release-playtest.sh が担う（`moorestech.exe`・`game/mods`・`build-info.json` の検査が組み込まれている）。

- [ ] **Step 3: ユーザーへ公開の承認を取る（AskUserQuestion）**

質問: 「検証機 smoke をやり直すには、本ブランチのビルドを Steam の `playtest` ブランチへ公開する必要があります（release-playtest.sh が setlive します。パスワードを知るテスターにも届きます）。公開して smoke を回しますか？」 選択肢: 「公開して回す」／「今は回さない（PR 後に別途）」。
「今は回さない」なら Step 4〜6 を飛ばし、Task 8 の残課題起票に「検証機 smoke 未実施（公開待ち）」を入れる。

- [ ] **Step 4: （承認時のみ）公開と smoke**

本ブランチを push し（`git push -u origin feature/playtest-title-gates`）、メインクローンの `scripts/playtest` から回す（release-playtest.sh 冒頭の規約。封じ込め env は読まない。資格情報は env.sh から）:

```bash
. ~/hermes-agent/data/services/playtest/env.sh
cd ~/hermes-agent/data/repos/moorestech
MOORESTECH_BUILD_BRANCH=feature/playtest-title-gates scripts/playtest/release-playtest.sh origin/feature/playtest-title-gates
```

取り込み（plan H の `playtest-ingest`）が smoke の報告を拾わないよう、実行前に `services.json` から外すか止める（README「注意: 取り込み（plan H）との競合」）。

- [ ] **Step 5: （承認時のみ）検証機の同意既読が無くて phase1 が preconditions で落ちた場合**

`runs/<label>/verify/` の phase1 `result.json` の失敗理由が `the playtest consent notice has not been acknowledged` なら、ユーザーに検証機で1回だけ Steam から起動してタイトルの「了解」を押してもらい（9/18 に WebUI で押せなかった操作が、タイトルでは押せることの実機確認を兼ねる）、`scripts/playtest/verify-on-windows.sh <label>` だけを回し直す。押せたかどうかと結果を `bd note` に残す。

- [ ] **Step 6: （承認時のみ）合否と転記**

合格条件: phase1・phase2 の `result.json` が `success: true`、`verify-on-windows.sh` が READY を取得して ACK まで終える（exit 0）。検証機の Player.log（取得できる範囲）に `[PlaytestTitleGates] 無人起動のためタイトルのゲートを出さずに進みます reason:playtestSmoke` が出ていること（smoke がタイトルの迂回を通った証跡）。`refused|lacked steamId|malformed` が 0 件。結果（ラベル・phase1/phase2・READY の fileCount）を `## 判断記録（ADR）` に書き、コミットする。

```bash
git add docs/superpowers/plans/2026-09-20-playtest-identity-and-title-start-gates.md
git commit -m "docs(plan): 検証機smokeの結果を記録する"
```

---

## Task 8: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] **Step 1:** moores-code-review スキルを `origin/master...HEAD` の全ブランチ差分に対して実行し、Critical を反映する。
- [ ] **Step 2:** 反映がゲートの経路（`PlaytestTitleGates`・`PlaytestTitleGateSequence`・`PlaytestConsentGate`・`CrashReportGate`・`PreviousSessionStartupTasks`・`PlaytestLaunchGate`・`PlaytestSessionResponse`・`PlaytestLaunchGateView`・`StartLocal`・`ConnectServer` の判定・条件式・その評価時点）に触れたら、反映後のバイナリで Task 6 を Step 1 からやり直し、Task 3 のテストと `PlaytestReportAndProgressTest` を再実行する。テスト通過・ログ無音は代替にならない。
- [ ] **Step 3: e2e の合否をログの警告・拒否語で判定し直す** — Task 6 Step 6 と同じ grep を最終バイナリの区間で行い、`upload not requested` 以外 0 件であることを確かめる。
- [ ] **Step 4: 残課題を1件ずつ起票する（起票されるまで残課題と呼ばない）。** 少なくとも次を `bd create "<title>" --description="<why/what>" --type=task --priority=<n>` で起票し、番号を `## 判断記録（ADR）` に列挙する（`bd` を含む文章は heredoc に入れず、Write で用意した文を引数に渡す）:
  - `localization.csv` の WebUI 専用だった未使用キー `ui.playtest.gate.answerAccepted` / `ui.playtest.gate.disconnected` / `ui.playtest.gate.notClosed` の削除（force-recompile を伴う）。
  - タイトルの退避が Editor の `PlayModeLaunchOverrides`（別ワールド指定）を反映せず既定ワールドのスナップショットを見る件。
  - 配布版での `manifest.steamId`・進行記録ヘッダ・異常終了箱の実値の目視確認（Editor は開発者モードで Allowed 経路を通れず、smoke に検査を足さない裁定のため、単体テスト以外の実機確認が未了）。
  - Task 7 を回さなかった場合: 検証機 smoke の未実施（公開待ち）。
  - Task 6 Step 6 で本変更と無関係と判定した警告があれば、その各1件。
  - `moorestech-zohw` へ `bd note`: タイトル作り変え時に `Client.MainMenu/Playtest/PlaytestConsentPopup`・`CrashReportPopup` と `PlaytestTitleGates` の段階購読を移すこと。
  - `moorestech-zzns` へ `bd note`: 同意・異常終了確認は Play locally 後から消えたので、カーソル中央ロックの影響を受けるのは出展モードの言語ゲートだけになった。
- [ ] **Step 5:** 反映をコミットする。

## Task 9: セッション終了可能状態にすること

- [ ] **Step 1:** pr-create スキルで PR を作成する（マージコミット運用。Squash しない）。master とのコンフリクトがあれば master をマージして解消し、`uloop compile` と Task 3・Task 5 のテストを再実行して push する。
- [ ] **Step 2:** PR 本文に「要ユーザー裁定の結果（D1・D2）」「Task 7 の実施有無（未実施なら理由と起票番号）」「ユーザー作業（検証機での同意既読化）」を残す。
- [ ] **Step 3:** 全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態であることを確認し、CLAUDE.local.md の規約どおり `moores-wt rm playtest-title-gates` で worktree と Editor を畳む。

---

## 機能の死活表

撤去・移設で「同じ機構にぶら下がる操作」を全部並べる。

| # | 操作 | 計画後 | 根拠 |
|---|---|---|---|
| L1 | 初回起動の参加同意（通常の Play locally） | 生きる（場所がタイトルへ移る） | Task 3・4。文言キーと `PlaytestConsentFlag` は不変（R5） |
| L2 | 前回異常終了の確認（説明入力・送る／送らない・書けなかったら閉じない） | 生きる（タイトルへ移る） | `CrashReportGate` の規則をそのまま移し、書けなかった表示は `ui.playtest.gate.respondFailed` |
| L3 | 出展モードの言語選択（WebUI） | 生きる | `EventLanguageGate`・`StartGateTopics.EventLanguage*`・`WaitingGateTopic`・`useGateAnswer`・`FullScreenGate` を残す |
| L4 | ポーズメニューのバグ報告（WebUI） | 生きる | `bug_report.submit` と `bundle_write_failed` 等のコードは残す |
| L5 | 進行記録・正常終了マーカー | 生きる | 書き手の設置はパイプライン先頭のまま（Task 2）。`PlaytestReportAndProgressTest` で確認 |
| L6 | 起動時照合の表示・持ち越し送信 | 生きる（送信は同意の後ろへ） | R6。ADR 0065 裁定 |
| L7 | smoke（検証機）の無人迂回 | 生きる | タイトルで `DeclareUnattendedProcess` の理由を読み閉じたゲートで組む。smoke は `StartLocal` を経由しない |
| L8 | テスト・DSL の無人迂回 | 生きる | 直接起動はタイトルを通らず、`RunAtStartup` が迂回印を消費してログする |
| L9 | リモート接続（Connect server） | 変わる（生きる） | 接続前にタイトルの同意・確認へ答える必要が出る（従来はリモートでは出なかった）。記録はリモートでは集めないまま。注目点として PR に書く |
| L10 | WebUiHost が起動に失敗したときの記録収集 | 変わる | 従来は集めなかった。ADR 0065 agent前提どおり集める（同意はタイトルで済むため） |
| **D1** | **Editor で GameInitializer シーンを直接再生したとき（開発者の対話起動）の同意・前回異常終了の確認** | **消える** | 直接起動はタイトルを通らないため出ない。退避と未応答の印は残り、次にタイトルを通る起動で聞く。**2026-09-20 ユーザー裁定で案 A に確定** |
| **D2** | **出展モードの自動開始（`EventModeAutoStart`）での同意・前回異常終了の確認** | **消える** | 自動開始は `AfterSceneLoad` で `PlaytestStartGateBypass.DeclareUnattendedProcess` を宣言してから `LocalGameLauncher.StartLocalGame()` を呼ぶので、タイトルのゲートは閉じたまま即通過する。**2026-09-20 ユーザー裁定で案 A に確定** |

**D1・D2 の裁定の選択肢（2026-09-20 に A で裁定済み。以下は棄却案の記録）:**
- A（推奨）: 消えてよい。D1 は開発者の Editor だけの経路、D2 は出展会場の開発者ビルドで、どちらもプレイテスト配布の対象外。未応答の印は `PendingCrashReportMark` で残り、次にタイトルを通る起動で聞き直せる。plan はこの前提で書いてある。
- B: D2 だけ残す。`EventModeAutoStart.AutoStartIfEventMode` を「`PlaytestTitleGates.Step` が `Passed` になってから `LocalGameLauncher.StartLocalGame()`」に変え、タイトルのポップアップに答えてから自動開始する（出展会場でテスターでない来場者に同意画面が出る点を受け入れる）。Task 4 に 1 ステップと `EventModeLaunchLanguageTest` 系の追加テストが要る。
- C: D1 も残す。直接起動でもタイトル相当のゲートを出す仕組みが要るが、表示の場（uGUI）が GameInitializer / MainGame に無く、WebUI へ戻すのは ADR 0065 の裁定と矛盾する。事実上 ADR の再裁定になる。

## 保留経路と解消条件（Self-Review 4）

| 保留 | 解消する条件・主体 | 到達不能になる入力と、その代替経路 | テスト |
|---|---|---|---|
| 同意の待ち（`Consent`） | タイトルで「了解」を押す（常に押せる） | 無人起動は応答者がいない → 閉じたゲートで即 `Passed`（R8） | 未読なら同意で止まる／無人は即通過 |
| 異常終了確認の待ち（`CrashReport`） | 送る（書けた）か送らない | 書き出しが恒久に失敗する → 「送らない」は常に押せる（`CrashReportGate` の規則） | 書けなかったら確認に留まり、送らないで通過 |
| 開始の拒否（`TryPassStart` false） | 段階が `Passed` になる | 照合が Blocked → タイトルのゲートは始まらず、照合の拒否表示が出る（従来どおり）。`NotStarted` のまま押された → 照合結果の購読で同期に始まるので、次の押下で通る | `PlaytestTitleGatesTest` の全段階 |
| 持ち越し送信の見送り | 了解（対話）／既読（無人） | 無人起動で未読 → この起動では送らずログ。次の対話起動で了解すれば送る | 無人で未読なら送らない |
| 直接起動で確認を出さない（D1・D2） | 次にタイトルを通る起動 | 印の寿命は `last-session` の `pending_crash_report`（ディスク。プロセス再起動を越える）。退避物も `last-session` に1世代残る | `PlaytestReportAndProgressTest`（直接起動の一巡） |
| タイトルの待ちの打ち切り | 打ち切り後は通過しない | 待ちの寿命は `Application.exitCancellationToken`（D-C1）。タイトルを破棄しても打ち切られず、初期化失敗で戻った再訪では同じ列に答え直せる。実際に打ち切られるのはアプリ終了だけで、未応答の印は残る | 打ち切られた後の了解では通過しない |

## 配置と前例

| # | 項目 | 配置 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `PlaytestSessionResponse.SteamId` | `Client.PlaytestReceiver/Http/Responses` | 外部JSONの必須フィールド検査（欠落は null＝契約違反） | 同ファイルの `token`・`expiresAt` の欠落検査 |
| 2 | `PlaytestSession.VerifiedSteamId` | `Client.PlaytestReceiver` | `{ get; private set; }` | 同クラスの `_token` / `_tokenRefreshAtUtc`（受け口応答から得る状態の保持者） |
| 3 | `ReceiverVerifiedSessionIdentity` | `Client.PlaytestReceiver/Gate` | `IPlaytestSessionIdentity` 実装 | `EmptyPlaytestSessionIdentity`（`Client.Game/InGame/BugReport/Playtest/PlaytestSessionIdentity.cs`）。実装を受け口側に置くのは ADR 0060 裁定4 の「plan D の Steam 認証が SetCurrent で差し込む」の配置。依存方向は既存の `Client.PlaytestReceiver → Client.Game` |
| 4 | 識別を据える場所 | `PlaytestLaunchGate.EvaluateAsync` | 結果を配る直前の同期プッシュ | 同メソッドの `SetCurrent(result)`（照合結果の唯一の書き手） |
| 5 | `PlaytestTitleGates`（static・段階の配布） | `Client.Starter/Playtest/TitleGates` | static `ReactiveProperty` + `SubsystemRegistration` リセット | `PlaytestLaunchGate`（`Client.PlaytestReceiver/Gate/PlaytestLaunchGate.cs`。MainMenu に DI が無いので static、開始経路が問い合わせる関所） |
| 6 | `PlaytestTitleGateSequence` | `Client.Starter/Playtest/TitleGates` | `UniTaskCompletionSource` を順に待つ | 置換対象 `PlaytestStartGateHandles.WaitInOrderAsync`（同じ順序・同じ待ち方。駆動は合成ルートからの明示呼び出しで、置換対象と同じ） |
| 7 | `PlaytestConsentGate` / `CrashReportGate` | `Client.Starter/Playtest/TitleGates`（WebUiHost から移設） | 1回だけ効く応答・書けなければ待機へ戻す | 移設元そのもの。WebUI 通知（`IStartGateWaitState`）だけを外す |
| 8 | ゲート本体を `Client.Starter` に置く理由 | `Client.Starter` | — | `Client.MainMenu` は asmdef を持たず Assembly-CSharp で、`Client.Tests` から参照できない。タイトルから呼ばれる起動系（`LocalGameLauncher`・`EventModeAutoStart`・`StandalonePlaytestSmokeBootstrap`）が既に `Client.Starter` にあり、`Client.Starter` は `Client.PlaytestReceiver`・`Client.Game` を参照済み |
| 9 | uGUI ポップアップ | `Client.MainMenu/Playtest` | `[SerializeField]` + `Initialize` + 段階の購読は合成ルート側 | `PlaytestLaunchGateView`（表示専用・照合結果の購読）、`ResetAllDataConfirmPopup`（確認ポップアップの形） |
| 10 | 開始経路の関所 | `StartLocal` / `ConnectServer` | 同期の `TryPassStart` | 同2ファイルの `PlaytestLaunchGate.TryPassStart` |
| 11 | 退避の1回化 | `PreviousSessionStartupTasks` | static フラグ + `SubsystemRegistration` リセット | `PlaytestLaunchGate.ResetOnPlayMode`、ADR 0060 裁定5 の「1箇所」＝この1関数 |
| 12 | シーン編集 | `MainMenu.unity` | `uloop execute-dynamic-code` | AGENTS.md の正規ルート |

機構選択（検査4）: 能動介入（パイプラインの開始ゲートを「凍結・迂回」して残す）案と、受動的統合（既存の WebUI ゲートをタイトルで CEF 起動して使う）案は ADR 0065 でユーザーが棄却済み（「今のタイトルに合わせ uGUI」）。本 plan は既存のゲート本体（1回だけ効く応答・書けなければ待機へ戻す規則）を無傷で移し、表示と駆動元だけを差し替える。

データフロー（Phase 1.5）:
```
（照合 EvaluateAsync）→［PlaytestLaunchGate.Current ＋ 識別］→（合成ルート PlaytestLaunchGateView）→ PlaytestTitleGates.Begin →［PlaytestTitleGates.Step］→（ポップアップ表示・StartLocal/ConnectServer の関所）
```
新設の `PlaytestTitleGates.Step` は「照合の後段の書き手が1人（sequence）、読み手が表示と関所」の共有状態で、交差点（逆流・並行経路）を足していない。

## 判断記録（ADR）

- 設計 ADR: `docs/adr/0065-playtest-identity-from-receiver-and-title-start-gates.md`、裁定: `.decisions/2026-09-20-プレイテストの識別は受け口の検証済みSteamIDで確定する.md`、`.decisions/2026-09-20-参加同意と前回異常終了の確認はタイトルにuGUIで移し初回配布前に実施する.md`。
- **識別は `ReceiverVerifiedSessionIdentity` を `Client.PlaytestReceiver/Gate` に置き、`PlaytestLaunchGate` が `SetCurrent(result)` の直前に据える** — agent前提（ADR 0065 agent前提。`Client.PlaytestReceiver.asmdef` が `Client.Game` を参照済みで、呼び出し位置を合成ルートへ逃がす必要が無い）。
- **steamId は allowed の真偽に関わらず 200 応答の必須フィールドにする** — agent前提（`token` の前例と同形。受け口 `session.ts:54` は 200 で常に steamId を返す）。これに伴い既存テスト `allowedが立っていない200では許可しない` の本文に steamId を足す。
- **識別は起動時照合の1回で確定し、トークン再発行時には差し替えない** — agent前提（アップロードの置き場所は token の steamId で決まり、識別は記録の自己申告欄にすぎない。再発行で別人に変わる経路は受け口の許可リスト上ない）。
- **正常終了の書き手の設置と進行記録の回収はパイプライン先頭に残し、退避と印の消費だけをタイトルへ前倒しする** — agent前提（ADR 0065 agent前提の「退避と印の消費」の範囲どおり。書き手は設置時に識別を読むので照合通過後でなければならず、タイトルでは今回のセッションの印も録画も書かないため偽の異常終了の窓は開かない）。`CleanExitMarkWriter` の説明コメントを更新する。
- **退避はタイトル経由なら `SalvageAtTitle`、直接起動なら `RunAtStartup` のどちらか1回（`_salvagedThisBoot`）** — agent前提（ADR 0060 裁定5「1箇所」を「1関数・1起動1回」として保つ。テスト・DSL・出展モードの自動開始はタイトルを通らないため、パイプライン側の入口を残さないと退避も書き手も走らない）。
- **直接起動は迂回印を `RunAtStartup` で読んで消費し、理由をログへ出す** — agent前提（Editor の印は読んだ時点で消費される設計。読まないと次の手動のタイトル起動へ持ち越され、確認が1回消える）。
- **タイトルの退避は既定ワールド（`CliConvert.Parse<StartServerSettings>(空)`）のスナップショットを対象にし、`isRemoteConnection` は false** — agent前提（タイトル時点ではリモートかを知らない。Play locally が起動するのは既定ワールド。Editor の `PlayModeLaunchOverrides` は反映しない → 残課題として起票）。
- **ゲート本体は `Client.Starter/Playtest/TitleGates` に置き、uGUI は `Client.MainMenu/Playtest` に置く** — agent前提（配置と前例 #8）。
- **サーバー接続（Connect server）もタイトルのゲートを関所にする** — agent前提（ゲートはタイトル全体の関所。ポップアップは全面背景で他のボタンを塞ぐので、コード側の関所も揃える）。死活表 L9 で注目点として PR に書く。
- **無人起動で同意が未読なら持ち越しを送らない** — agent前提（ADR 0065「初回の了解が済むまで送らない」を無人でも守る。smoke は `StandalonePlaytestSmokePreconditions` が既読を要求するので影響しない）。
- **段階の変化は必ずログ（`[PlaytestTitleGates] step X`）、開始の拒否は `LogWarning`** — agent前提（fail-closed の無音縮退禁止。待ちは上限を持たない）。
- **確認中の Play locally はテスター向けの文言を足さずログだけ** — agent前提（答えるべきポップアップが全面に出ており、R5「文言を変えない」の範囲で新キーを足さない）。
- **書けなかった表示は既存キー `ui.playtest.gate.respondFailed` を流用** — agent前提（R5。WebUI 専用だった `answerAccepted`・`disconnected`・`notClosed` は未使用になるが、csv の整理は force-recompile を伴うため別 issue）。
- **WebUI の `useFrontmostStartGate` / `pickFrontmostStartGate` と C# の `StartGateTopics` の precedence 機構は、ゲート1枚になっても残す** — agent前提（出展モードの言語ゲートがそのまま使っており、作り替えは本 plan の範囲外。YAGNI の観点での畳み込みは zohw 以降に判断）。
- **WebUI の e2e spec `playtestGate.spec.ts` と C# の `PlaytestStartGatesTest`・`PlaytestGateActionResultMappingTest` は削除し、順序と縮退の検査は `PlaytestTitleGateSequenceTest`・`PlaytestTitleGatesTest` へ移す** — agent前提。
- **unityプレイ録画テストは DSL でなく `simulate-mouse-ui`（EventSystem レイキャスト）と `execute-dynamic-code` で行う** — agent前提（DSL は GameInitializer から起動しタイトルを通れない。`simulate-mouse-ui` は OS 入力ではない）。録画の代わりにスクリーンショットとログを証跡にする。
- **検証機 smoke は `playtest` ブランチへの公開を伴うため、ユーザー承認のゲートを置く** — agent前提（`app_build_playtest.vdf` の `setlive playtest`。配布は別途ユーザー判断という指示）。
- **smoke に SteamID 検査を足さない** — ユーザー裁定（ADR 0065）。
- **D1・D2（直接起動・出展モード自動開始で確認が出なくなる）** — 暫定 A（agent前提。ユーザー 2026-09-20「意思決定は最後にまとめてやる」により最終裁定は実装後にまとめて取る）。
- **Task 6 実施（2026-09-20 02:39〜02:46 JST、Editor 内の PlayMode、Game View 1920x1080、英語表示）** — 証跡は scratchpad `e2e/`（`01-consent.png`・`02-crash-report.png`・`03-crash-report-with-description.png`・`04-passed.png`・`05-maingame.png`・`06-second-boot-title.png`・`07-second-boot-maingame.png`、ログ `all.txt`・`warnings.txt`・`errors.txt`・`all-second-boot.txt`）。
  - **本変更に起因する不具合を1件発見・修正し、Step 1 からやり直した**: `MainMenu.unity` の `CrashReportPopup/Panel/Description`（説明の入力欄）が `m_IsActive: 0` で保存されており、前回異常終了の確認に入力欄が出ていなかった（`a9f7ee603`。Editor 経由で有効化して保存。シーン1行の差分）。
  - 押下経路: `AgreeButton`・`SendButton`・`Start local game`（通過後）は `simulate-mouse-ui`（レイキャスト）で押せた。確認中の `Start local game` は `onClick.Invoke()` で関所を直接叩いた（ブリーフどおり。背景に塞がれて届かないのが正しいため）。同じ座標（ボタンのパネル外の端 (110,846) と中央 (306,846)）へのレイキャストクリックは「no UI element hit」で開始しなかった。同じ (306,846) は通過後の起動で `Start local game` に当たったので、座標の取り違えではない。説明の入力は `execute-dynamic-code` で `TMP_InputField.text` を設定した（キーボード注入を使わないため）。
  - 合否（ゲートに関する項目）: 合格 — `step Consent` → 確認中の開始は `[PlaytestTitleGates] StartLocal refused: the title gates are at Consent` で拒否・シーンは MainMenu のまま → 同意で `step CrashReport`・既読フラグ作成 → 送るで `step Passed`、outbox に箱が1つ増え `kind: crash`・`description: e2e: タイトルの前回異常終了確認` → Play locally で MainGame まで初期化。2回目の起動（既読・正常終了）はポップアップが出ず `step Passed` のみ（`step Consent`/`step CrashReport` は 0 件）で、Play locally で MainGame まで進んだ。判定区間の grep は `upload not requested` 2件のみ（開発者モードで出るのが正しい語）。
  - 合否（Warning・Error 0件）: **未達**。判定区間に Warning 5件・Error 1件。いずれも master にある既存の出所で、本変更の差分に無い — ① Error `[WebUiHost] Node binary not found`＋Warning `[WebUiHost] Bind skipped: hub is null`（この worktree に `moorestech_web/node` が未配備。環境）、② Warning `進行記録の欠損 steamId` と `バグ報告バンドルに steamId を入れられませんでした`（Editor は開発者モードで起動時照合を飛ばすため識別が無い。欠損の表明は設計どおりだが、理由文言「plan D 未導入またはSteam未起動」は本 plan 後は古い）、③ Warning `Couldn't create a Convex Mesh`（円柱.009・Cylinder.003。アセット）。起票番号は Task 8 で並べる（未起票）。
  - 見た目: 同意・確認ポップアップの本文はパネル内に収まり、はみ出し・重なりは無い。パネル（900x520／900x560、参照解像度 1066x600）は画面を覆い切らず、左端にタイトルの言語ドロップダウン等が一部見えるが、全面の背景がレイキャストを塞ぐので押せない。入力欄の文字（14pt）は本文より小さい。
  - 片付け: 実施前は既読フラグが無かったため、終了後に削除して元へ戻した（退避ファイルは作られていない）。検証用の箱は outbox から削除し、`playModeStartScene` は null に戻した。
- **Task 6 やり直し（2026-09-20 13:59〜14:14 JST、裁定反映後の `acf2d96a0` で通し直し）** — 合格。詳細は `.superpowers/sdd/ptg-task-6-rerun-report.md`、証跡は scratchpad `e2e-r2/`（`01-consent.png`〜`08-second-boot-maingame.png`、`all.txt`・`all-second-boot.txt`）。順序（照合→同意→前回異常終了の確認→Play locally→MainGame）と2回目起動のポップアップ非表示を再確認し、新規挙動の「確認表示中は再照合の待ち文言を出さない」（`[PlaytestLaunchGate] 確認（Consent）に答える前なので再照合の待ち文言は出しません`）と送り手の繋ぎ直し（実物の `PlaytestUploadRunner` へ要求が届く）も実測した。判定区間の Warning 5・Error 1 は前回と同一で、すべて環境（`moorestech_web/node` 未配備）・アセット（Convex Mesh）・設計上必要な steamId 欠損表明に特定済み。拒否/喪失語の grep は本変更起因 0 件。出展モードの期限付き待ちは Editor の手動再生から発火しないため未実測。
