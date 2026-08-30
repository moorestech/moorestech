# 出展モードの言語選択ゲート Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 出展モードで、MainGame ロード完了後・オープニングスキット再生前に WebUI の全画面言語選択画面を出して永久に待機し、言語ボタンの押下で初めて無操作180秒タイマーを武装する。

**Architecture:** 「無操作監視オブジェクトを生成すること」自体を武装とみなし、言語選択が済むまで `EventIdleQuitWatcher` を生成しない。ゲートは `MainGameInitializationFinalizer.FinalizeAsync()` の先頭に `await` として挟むため、スキット（`SkitFireManager.PostInitialize`／VContainer 構築時）もチュートリアル（`ChallengeManager.ApplyInitialTutorials`）も、既存クラスへ「出展モード」の語彙を一切持ち込まずに保留される。画面は WebUI（React/Mantine）の新規 feature で、既存の topic/action ブリッジに `event_mode.language_gate` topic と `event_mode.select_language` action を1本ずつ足して実現する。

**Tech Stack:** Unity C#（UniTask / UniRx / VContainer / NUnit）、WebUI（React 18 + Mantine + zod + vitest）、WebSocket topic/action ブリッジ（`Client.WebUiHost`）

## Requirements

- R1: 出展モード（`MOORESTECH_EVENT_MODE=1`、Editor では加えて `MOORESTECH_EVENT_MODE_EDITOR=1`）でのみ言語選択ゲートが動く。それ以外の起動は現状と完全に同一挙動。受け入れ基準: `EventExhibitionSettings.FromEnvironment().IsEnabled` が false のとき `MainGameInitializationFinalizer` が1フレームも待たずに従来どおり進むことをテストで示す
- R2: ゲートは MainGame ロード完了後・`starter.StartGame()` の前に入る。受け入れ基準: `MainGameInitializationFinalizer.FinalizeAsync()` の最初の文が待機であり、オープニングスキットとチュートリアルの発火がその後段にあること
- R3: 待機中は無操作終了が絶対に起きない。受け入れ基準: 言語選択前に `EventIdleQuitWatcher` のインスタンスが存在しないことをテストで示す
- R4: 言語ボタンの押下だけが「最初の操作」である。マウス移動・空白クリック・キー入力は待機を打ち切らない。受け入れ基準: 待機中は入力を観測する主体（watcher）が存在しないこと（R3と同一の機構で満たす）
- R5: 言語ボタン押下で `Localize` の言語が切り替わり、続けて `EventIdleQuitWatcher` が生成される。受け入れ基準: フォールバック経路の watcher 生成を NUnit で示し、選択経路の生成は Task 6 の実機確認（選択後3分で終了し再起動する）で示す
- R6: 選択肢は `LanguageCatalog.Languages` の DisplayName（English / 日本語 / Deutsch）。既存の `/api/i18n-languages` エンドポイントを使い、新しい配信経路を作らない
- R7: 見出しは英語固定 `Select Language`。辞書（`t()`）を通さない。受け入れ基準: 辞書が空でも見出しが描画されることを vitest で示す
- R8: 背景は不透明の暗幕でゲーム画面を完全に隠す。受け入れ基準: `backgroundOpacity={1}` 相当の不透明 Overlay であること
- R9: 待機画面は再接続オーバーレイ（`--z-portal-reconnect: 2000`）より前面に立つ。受け入れ基準: `zLayerTokens.test.ts` に層序アサーションを追加
- R10: 選択は冪等。二重クリック・再送でゲートが二度開かない。受け入れ基準: 2回目以降の `TrySelectLanguage` が成功を返しつつ状態を変えないことをテストで示す
- R11: 未知の locale は失敗として拒否し、ゲートは開かない。受け入れ基準: `ActionResult.Fail("unknown_locale")` を返すことをテストで示す
- R12: WebUiHost が起動失敗（`Hub == null`）した場合は画面を出せないため、英語のまま即開始して watcher を生成する（無人ブースを止めない）。受け入れ基準: この分岐がコード上に存在しエラーログを残すこと
- R13: action 名・topic payload の C#⇔TS 契約が両側テストで一致する。受け入れ基準: `action_names.json` 更新後に C# `WireContractActionNamesTest` と TS `actionNames.test.ts` が通る
- R14: e2e mock host に既定 snapshot（`{ waiting: false }`）を追加する。受け入れ基準: `topicFixtures` の型検査が通る

**やらないこと（スコープ境界）:**
- 選び直し導線（選択後に言語を変える UI）は作らない。誤選択は Cmd+Q か3分待機で回収する
- ロード前（`InitializeScenePipeline` 段階）に画面を出すための CEF 構造改修はしない
- メインメニュー（`MainMenu` シーンの UI）には一切手を入れない
- 起動ごとの英語強制リセット（`EventModeAutoStart` の `Localize.TrySetLanguage(DefaultLanguageCode)`）は現状維持
- 無操作タイムアウト秒（既定180・`MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS`）の仕様は変えない

## 機能死活表（触る機構にぶら下がる操作）

本planが触るのは「出展モードの無操作終了」機構だけである。同機構にぶら下がる操作の生死:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 出展モードでプレイ中の3分無操作リセット | 生きる | 言語選択後に `EventIdleQuitWatcher` が生成され、判定ロジックは無変更 |
| 出展モードのロード完了直後からの3分カウント | **意図的に廃止** | 待機画面で無限に待つ裁定（ADR 0040）。退化ではなく裁定済みの仕様変更 |
| 出展モード起動時のワールド削除・英語リセット・自動開始 | 生きる | `EventModeAutoStart` からは watcher 生成の1行だけを外す |
| 通常起動（環境変数なし）の全操作 | 生きる | `EventExhibitionSettings.IsEnabled` が false のとき早期 return |
| ポーズメニューの言語設定（`LanguageSelect`） | 生きる | 触らない。ゲートは別 feature として並置する |
| `Cmd+Q` / ウィンドウ×による終了 | 生きる | 触らない。ADR 0030 のとおり次回起動で新規ワールドになる |

## Global Constraints

- **partial 禁止**（如何なる条件でも）。**`Func<>` 禁止**。**try-catch は外部境界の隔離目的のみ**（本planでは新規の try-catch は不要）
- **1ファイル200行以下**、1ディレクトリ10ファイルまで
- コメントは日本語1行→英語1行のセットを約3〜10行ごと。各言語1行に収める（折り返し禁止）
- **イベント発火に `Action` を使わない。UniRx を使う**（`Subject<Unit>` + `IObservable<Unit>`。前例: `Client.WebUiHost/Game/Topics/LocalizationTopic.cs`、`WebUiModalService.OnPendingChanged`）
- **単純な getter/setter プロパティ禁止**。値の設定は `public void SetHoge` 形式。`{ get; private set; }` は許容
- **デフォルト引数禁止**。引数追加時は呼び出し側を全て変更する
- **`.meta` ファイルを手で作らない**（Unity が生成する）。**Prefab/シーン/ScriptableObject をテキスト編集しない**（本planでは Unity 固有ファイルの編集は発生しない）
- **.cs を変更したら必ずコンパイルする**: `uloop compile --project-path ./moorestech_client`
- 新規 topic は `docs/webui/topic-conventions.md` の「新規 Topic テンプレート」9項目に従う
- 汎用基盤（`SkitFireManager`・`ChallengeManager`・`Localize`）に「出展モード」の語彙を持ち込まない。判断は出展モード側で行う
- 作業ブランチ: `feature/event-mode-language-select-gate`（`moores-wt new feature/event-mode-language-select-gate` で使い捨て worktree を作る）
- タスク台帳: bd の `moorestech-s0tk` を着手時に自分へ割り当て、完了時に close する

---

### Task 1: 言語選択ゲートのサービス本体

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/EventMode/EventLanguageGate.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventLanguageGateTest.cs`

**Interfaces:**
- Consumes: `Client.Localization.Localize.TrySetLanguage(string) -> bool`
- Produces:
  - `Client.WebUiHost.Game.EventMode.EventLanguageGate`
  - `bool IsWaitingSelection { get; private set; }`（初期値 true）
  - `IObservable<Unit> OnWaitingChanged`
  - `UniTask WaitForSelectionAsync()`
  - `bool TrySelectLanguage(string languageCode)`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventLanguageGateTest.cs`:

```csharp
using Client.WebUiHost.Game.EventMode;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.EventMode
{
    public class EventLanguageGateTest
    {
        [Test]
        public void 生成直後は選択待ちである()
        {
            var gate = new EventLanguageGate();

            Assert.IsTrue(gate.IsWaitingSelection);
            Assert.IsFalse(gate.WaitForSelectionAsync().Status.IsCompleted());
        }

        [Test]
        public void 選択可能な言語を選ぶと待機が解けて待ち合わせが完了する()
        {
            var gate = new EventLanguageGate();

            Assert.IsTrue(gate.TrySelectLanguage("japanese"));

            Assert.IsFalse(gate.IsWaitingSelection);
            Assert.IsTrue(gate.WaitForSelectionAsync().Status.IsCompleted());
        }

        [Test]
        public void 未知の言語は拒否しゲートを開かない()
        {
            var gate = new EventLanguageGate();

            Assert.IsFalse(gate.TrySelectLanguage("klingon"));

            Assert.IsTrue(gate.IsWaitingSelection);
        }

        // 二重クリックと再送でゲートが二度開かないことを保証する
        // Guarantees a double click or a resend cannot open the gate twice
        [Test]
        public void 二度目の選択は成功を返しつつ状態を変えない()
        {
            var gate = new EventLanguageGate();
            gate.TrySelectLanguage("japanese");

            var changedCount = 0;
            gate.OnWaitingChanged.Subscribe(_ => changedCount++);

            Assert.IsTrue(gate.TrySelectLanguage("english"));
            Assert.IsFalse(gate.IsWaitingSelection);
            Assert.AreEqual(0, changedCount);
        }

        [Test]
        public void 選択で待機変化が一度だけ通知される()
        {
            var gate = new EventLanguageGate();
            var changedCount = 0;
            gate.OnWaitingChanged.Subscribe(_ => changedCount++);

            gate.TrySelectLanguage("english");

            Assert.AreEqual(1, changedCount);
        }
    }
}
```

`Localize` は `[RuntimeInitializeOnLoadMethod]` で辞書を張るため、EditMode テストで `TrySetLanguage("japanese")`
が false を返すようなら、テスト冒頭で `Localize.Initialize();` を呼んでから検証すること。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EventLanguageGateTest"`
Expected: FAIL（`EventLanguageGate` が存在せずコンパイルエラー）

- [ ] **Step 3: 最小限の実装を書く**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/EventMode/EventLanguageGate.cs`:

```csharp
using System;
using Client.Localization;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.EventMode
{
    /// <summary>
    /// 言語が選ばれるまでゲーム開始を止める出展モードの開始ゲート。
    /// The event-mode start gate that holds the game start until a language is chosen.
    /// </summary>
    public class EventLanguageGate
    {
        private readonly UniTaskCompletionSource _selectionSource = new();
        private readonly Subject<Unit> _onWaitingChanged = new();

        public bool IsWaitingSelection { get; private set; } = true;
        public IObservable<Unit> OnWaitingChanged => _onWaitingChanged;

        public UniTask WaitForSelectionAsync()
        {
            return _selectionSource.Task;
        }

        // 選択を1回だけ効かせる。二重クリックと再送は成功として捨て、ゲートを二度開けない
        // Only the first selection takes effect; double clicks and resends succeed as no-ops
        public bool TrySelectLanguage(string languageCode)
        {
            if (!IsWaitingSelection) return true;
            if (!Localize.TrySetLanguage(languageCode)) return false;

            IsWaitingSelection = false;
            _onWaitingChanged.OnNext(Unit.Default);
            _selectionSource.TrySetResult();
            return true;
        }
    }
}
```

`Client.Tests` の asmdef に `Client.WebUiHost` 参照があることを確認する（`WireContractActionNamesTest` が
同アセンブリを参照しているため既にあるはず。無ければ追加する）。

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 0 errors
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EventLanguageGateTest"`
Expected: PASS（5件）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/EventMode moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventLanguageGateTest.cs
git commit -m "feat: 出展モードの言語選択ゲートサービスを追加"
```

---

### Task 2: ゲートの topic / action / バインダ

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/EventMode/EventLanguageGateTopic.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/EventMode/EventLanguageGateActions.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/EventMode/EventLanguageGateBinder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventLanguageGateTopicTest.cs`

**Interfaces:**
- Consumes: `EventLanguageGate`（Task 1）、`Client.WebUiHost.Boot.WebSocketHub.RegisterTopic(string, ITopicHandler)` / `.RegisterAction(IActionHandler)` / `.Publish(string, string)`、`Client.WebUiHost.Game.Actions.IActionHandler`、`ActionResult.Success()` / `ActionResult.Fail(string)`
- Produces:
  - `EventLanguageGateTopic.TopicName = "event_mode.language_gate"`、payload `{ "waiting": bool }`
  - `SelectEventLanguageActionHandler.ActionType = "event_mode.select_language"`、payload `{ "locale": string }`
  - `EventLanguageGateBinder.Bind(WebSocketHub hub) -> EventLanguageGate`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventLanguageGateTopicTest.cs`:

```csharp
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions.EventMode;
using Client.WebUiHost.Game.EventMode;
using Client.WebUiHost.Game.Topics.EventMode;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    public class EventLanguageGateTopicTest
    {
        [Test]
        public void Snapshotは待機状態をwaitingとして配る()
        {
            var gate = new EventLanguageGate();
            var topic = new EventLanguageGateTopic(new WebSocketHub(), gate);

            var waitingJson = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
            Assert.IsTrue(waitingJson["waiting"].Value<bool>());

            gate.TrySelectLanguage("english");
            var selectedJson = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
            Assert.IsFalse(selectedJson["waiting"].Value<bool>());
        }

        [Test]
        public void 選択アクションはゲートを開き未知localeは失敗を返す()
        {
            var gate = new EventLanguageGate();
            var handler = new SelectEventLanguageActionHandler(gate);

            Assert.AreEqual("event_mode.select_language", handler.ActionType);

            var failed = handler.ExecuteAsync(JObject.Parse("{\"locale\":\"klingon\"}")).GetAwaiter().GetResult();
            Assert.IsFalse(failed.Ok);
            Assert.IsTrue(gate.IsWaitingSelection);

            var succeeded = handler.ExecuteAsync(JObject.Parse("{\"locale\":\"japanese\"}")).GetAwaiter().GetResult();
            Assert.IsTrue(succeeded.Ok);
            Assert.IsFalse(gate.IsWaitingSelection);
        }
    }
}
```

`ActionResult` の成否プロパティ名は `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/IActionHandler.cs`
を読んで実際の名前に合わせること（`Ok` でなければそのフィールド名へ置換する）。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EventLanguageGateTopicTest"`
Expected: FAIL（型が存在せずコンパイルエラー）

- [ ] **Step 3: topic を実装する**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/EventMode/EventLanguageGateTopic.cs`:

```csharp
using System;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.EventMode;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics.EventMode
{
    /// <summary>
    /// 出展モードの言語選択待ちを snapshot と revision 付き event で配信する。
    /// Publishes the event-mode language selection wait as a snapshot and revisioned events.
    /// </summary>
    public class EventLanguageGateTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "event_mode.language_gate";

        private readonly WebSocketHub _hub;
        private readonly EventLanguageGate _gate;
        private readonly IDisposable _waitingSubscription;

        public EventLanguageGateTopic(WebSocketHub hub, EventLanguageGate gate)
        {
            _hub = hub;
            _gate = gate;

            // 待機解除は1回だけ起きる離散状態。変化通知をそのまま event へ流す
            // Releasing the wait is a one-shot discrete change, so the notification maps straight to an event
            _waitingSubscription = gate.OnWaitingChanged.Subscribe(_ => _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _waitingSubscription.Dispose();
        }

        private string BuildJson()
        {
            return WebUiJson.Serialize(new EventLanguageGateData
            {
                Waiting = _gate.IsWaitingSelection,
            });
        }

        private class EventLanguageGateData
        {
            public bool Waiting;
        }
    }
}
```

- [ ] **Step 4: action を実装する**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/EventMode/EventLanguageGateActions.cs`:

```csharp
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.EventMode;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions.EventMode
{
    /// <summary>
    /// 出展モードの言語選択アクションを Hub へ登録する。
    /// Registers the event-mode language selection action with the Hub.
    /// </summary>
    public static class EventLanguageGateActions
    {
        public static void Register(WebSocketHub hub, EventLanguageGate gate)
        {
            hub.RegisterAction(new SelectEventLanguageActionHandler(gate));
        }
    }

    /// <summary>
    /// 来場者の言語選択をゲートへ渡し、可否の判断はゲートへ集約する。
    /// Hands the visitor's choice to the gate, which owns the accept/reject judgement.
    /// </summary>
    public class SelectEventLanguageActionHandler : IActionHandler
    {
        public string ActionType => "event_mode.select_language";

        private readonly EventLanguageGate _gate;

        public SelectEventLanguageActionHandler(EventLanguageGate gate)
        {
            _gate = gate;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var locale = payload?["locale"]?.ToString();

            // 選択可否の判定はゲート側に集約し、結果を失敗契約へ写す
            // Delegate the selectability judgement to the gate and map the result to the failure contract
            return UniTask.FromResult(_gate.TrySelectLanguage(locale)
                ? ActionResult.Success()
                : ActionResult.Fail("unknown_locale"));
        }
    }
}
```

- [ ] **Step 5: バインダを実装する**

`moorestech_client/Assets/Scripts/Client.WebUiHost/Game/EventMode/EventLanguageGateBinder.cs`:

```csharp
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions.EventMode;
using Client.WebUiHost.Game.Topics.EventMode;

namespace Client.WebUiHost.Game.EventMode
{
    /// <summary>
    /// ゲートの topic と action を Hub へ束ねる facade。WebUiGameBinder より前に呼ばれる。
    /// Facade binding the gate's topic and action to the Hub; called before WebUiGameBinder.
    /// </summary>
    public static class EventLanguageGateBinder
    {
        public static EventLanguageGate Bind(WebSocketHub hub)
        {
            var gate = new EventLanguageGate();
            hub.RegisterTopic(EventLanguageGateTopic.TopicName, new EventLanguageGateTopic(hub, gate));
            EventLanguageGateActions.Register(hub, gate);
            return gate;
        }
    }
}
```

名前空間が `Client.WebUiHost.Game.EventMode` / `...Game.Topics.EventMode` / `...Game.Actions.EventMode` の3つに割れるため、
`EventMode` という識別子を単体で書かず、型名を直接書いて `using` で解決すること（同名セグメントの曖昧解決を避ける）。

topic payload の C# 側 wire fixture（`WireFixtures/*.json`）は作らない。最も近い前例である
`localization.current` も fixture を持たず、payload が `{ waiting: bool }` 1フィールドで
C# 側は `EventLanguageGateTopicTest` が、TS 側は `validators.test.ts` がそれぞれ形状を固定するため。

- [ ] **Step 6: action 名フィクスチャへ登録する**

`moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json` の `actions` 配列末尾
（`"skit.set_ui_hidden"` の後）を次の形にする:

```json
    "skit.set_ui_hidden",
    "event_mode.select_language"
```

- [ ] **Step 7: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 0 errors
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EventLanguageGateTopicTest|WireContractActionNamesTest"`
Expected: PASS（`WireContractActionNamesTest` は実装ハンドラを自動走査するため、Step 6 の追加が無いと落ちる）

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat: 言語選択ゲートのtopicとactionを追加"
```

---

### Task 3: WebUI 側の topic 契約

**Files:**
- Modify: `moorestech_web/webui/src/bridge/transport/protocol.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/validators.ts`
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts`
- Test: `moorestech_web/webui/src/bridge/contract/validators.test.ts`（追記）

**Interfaces:**
- Consumes: Task 2 の wire 形状（topic `event_mode.language_gate` payload `{ waiting: boolean }`、action `event_mode.select_language` payload `{ locale: string }`）
- Produces:
  - `Topics.eventLanguageGate`
  - `EventLanguageGateData = { waiting: boolean }`
  - `ActionPayloads["event_mode.select_language"] = { locale: string }`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_web/webui/src/bridge/contract/validators.test.ts` の末尾へ追記する:

```ts
describe("event_mode.language_gate schema", () => {
  it("boolean の waiting だけを受理する", () => {
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: true }).valid).toBe(true);
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: false }).valid).toBe(true);
    expect(parseTopicPayload(Topics.eventLanguageGate, {}).valid).toBe(false);
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: "true" }).valid).toBe(false);
  });
});
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/bridge/contract/validators.test.ts`
Expected: FAIL（`Topics.eventLanguageGate` が存在せず型エラー）

- [ ] **Step 3: schema と型を足す**

`src/bridge/contract/schemas/ui.ts` の `LocalizationDataSchema` の直後へ:

```ts
export const EventLanguageGateDataSchema = z.object({ waiting: z.boolean() });
```

`src/bridge/contract/payloadTypes.ts` — import 一覧へ `EventLanguageGateDataSchema,` を追加し、
`export type LocalizationData = ...` の直後へ:

```ts
export type EventLanguageGateData = z.infer<typeof EventLanguageGateDataSchema>;
```

`src/bridge/contract/validators.ts` — import 一覧へ `EventLanguageGateDataSchema,` を追加し、
`[Topics.localization]: LocalizationDataSchema,` の直後へ:

```ts
  [Topics.eventLanguageGate]: EventLanguageGateDataSchema,
```

- [ ] **Step 4: topic と action を登録する**

`src/bridge/transport/protocol.ts` — `Topics` の `notification` 定義の直前へ:

```ts
  // 出展モードの言語選択ゲート。待機中だけ全画面で操作を塞ぐ
  // The event-mode language gate; blocks all input full-screen while waiting
  eventLanguageGate: "event_mode.language_gate",
```

同ファイルの `TopicPayloads` へ:

```ts
  [Topics.eventLanguageGate]: EventLanguageGateData;
```

`EventLanguageGateData` を同ファイル冒頭の payload 型 import へ追加する。

`src/bridge/transport/actionContract.ts` — `ActionPayloads` の `"localization.setLocale"` の直後へ:

```ts
  "event_mode.select_language": { locale: string };
```

同ファイルの `ACTION_TYPES` 配列の `"localization.setLocale",` の直後へ:

```ts
  "event_mode.select_language",
```

- [ ] **Step 5: mock host に既定 snapshot を足す**

`e2e/mock-host/topics/topicFixtures.ts` の `[Topics.localization]` 行の直後へ:

```ts
  // 通常のe2eは出展モードではないので待機しない。欠けると restoring のまま全操作が塞がる
  // Regular e2e is not event mode, so it never waits; a missing entry wedges everything in restoring
  [Topics.eventLanguageGate]: () => ({ waiting: false }),
```

- [ ] **Step 6: テストと型検査を実行して通ることを確認する**

Run: `cd moorestech_web/webui && npx tsc --noEmit`
Expected: エラーなし
Run: `cd moorestech_web/webui && npx vitest run src/bridge`
Expected: PASS（`validators.test.ts` の新規 describe と `actionNames.test.ts` を含む）

- [ ] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src/bridge moorestech_web/webui/e2e/mock-host
git commit -m "feat: 言語選択ゲートのweb側topic契約を追加"
```

---

### Task 4: 全画面言語選択画面（WebUI）

**Files:**
- Create: `moorestech_web/webui/src/features/eventLanguageGate/EventLanguageGate.tsx`
- Create: `moorestech_web/webui/src/features/eventLanguageGate/index.ts`
- Create: `moorestech_web/webui/src/features/eventLanguageGate/EventLanguageGate.test.ts`
- Modify: `moorestech_web/webui/src/app/App.tsx`
- Modify: `moorestech_web/webui/src/app/tokens.css`
- Modify: `moorestech_web/webui/src/app/zLayerTokens.test.ts`

**Interfaces:**
- Consumes: `Topics.eventLanguageGate`（Task 3）、`dispatchAction("event_mode.select_language", { locale })`、`localizationLanguagesUrl`（`@/bridge` から既出。`LanguageSelect.tsx` の前例と同じ `/api/i18n-languages`）
- Produces: `EventLanguageGate` コンポーネント（`@/features/eventLanguageGate` から export）

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_web/webui/src/features/eventLanguageGate/EventLanguageGate.test.ts`:

```ts
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const dispatchAction = vi.fn();
let waiting = true;

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  useTopicSelector: (_topic: unknown, select: (data: unknown) => unknown) => select({ waiting }),
  dispatchAction,
}));
vi.mock("@mantine/core", () => ({
  Button: ({ children, ...props }: { children: unknown }) => createElement("mock-button", props, children as never),
  Group: ({ children, ...props }: { children: unknown }) => createElement("mock-group", props, children as never),
  Overlay: ({ children, ...props }: { children: unknown }) => createElement("mock-overlay", props, children as never),
  Portal: ({ children }: { children: unknown }) => children as never,
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
  Title: ({ children, ...props }: { children: unknown }) => createElement("mock-title", props, children as never),
}));

import { EventLanguageGate } from "./EventLanguageGate";

const languagesResponse = [
  { code: "english", displayName: "English" },
  { code: "japanese", displayName: "日本語" },
  { code: "german", displayName: "Deutsch" },
];

afterEach(() => {
  vi.unstubAllGlobals();
  dispatchAction.mockReset();
  waiting = true;
});

describe("EventLanguageGate", () => {
  it("待機中は母国語表記のボタンと英語固定の見出しを描く", async () => {
    // 辞書は空のまま。見出しがt()経由なら壊れるので、リテラルであることの証明になる
    // The dictionary stays empty, so anything routed through t() would break: this proves the literal path
    setDictionaries("english", {}, {}, {});
    stubLanguagesFetch();

    const renderer = await renderGate();

    expect(optionLabels(renderer)).toEqual(["English", "日本語", "Deutsch"]);
    expect(headingTexts(renderer)).toEqual(["Select Language"]);
    act(() => renderer.unmount());
  });

  it("ボタン押下でlocale付きのselect_languageを送る", async () => {
    setDictionaries("english", {}, {}, {});
    stubLanguagesFetch();

    const renderer = await renderGate();
    await act(async () => { optionAt(renderer, 1).props.onClick(); });

    expect(dispatchAction).toHaveBeenCalledWith("event_mode.select_language", { locale: "japanese" });
    act(() => renderer.unmount());
  });

  it("待機していなければ何も描かない", async () => {
    waiting = false;
    setDictionaries("english", {}, {}, {});
    stubLanguagesFetch();

    const renderer = await renderGate();

    expect(renderer.toJSON()).toBeNull();
    act(() => renderer.unmount());
  });

  it("一覧取得に失敗したら辞書非依存リテラルのエラーと再試行ボタンを出す", async () => {
    setDictionaries("english", {}, {}, {});
    vi.stubGlobal("fetch", vi.fn(() => Promise.resolve({ ok: false, status: 500 })));

    const renderer = await renderGate();

    expect(testIds(renderer)).toContain("event-language-gate-error");
    expect(testIds(renderer)).toContain("event-language-gate-retry");
    act(() => renderer.unmount());
  });
});

function stubLanguagesFetch() {
  vi.stubGlobal("fetch", vi.fn(() =>
    Promise.resolve({ ok: true, json: () => Promise.resolve(languagesResponse) })));
}

async function renderGate(): Promise<ReactTestRenderer> {
  let renderer!: ReactTestRenderer;
  await act(async () => { renderer = create(createElement(EventLanguageGate)); });
  return renderer;
}

function optionNodes(renderer: ReactTestRenderer) {
  return renderer.root.findAllByType("mock-button" as never)
    .filter((node) => String(node.props["data-testid"]).startsWith("event-language-gate-option-"));
}

function optionAt(renderer: ReactTestRenderer, index: number) {
  return optionNodes(renderer)[index];
}

function optionLabels(renderer: ReactTestRenderer): string[] {
  return optionNodes(renderer).map((node) => String(node.props.children));
}

function headingTexts(renderer: ReactTestRenderer): string[] {
  return renderer.root.findAllByType("mock-title" as never).map((node) => String(node.props.children));
}

function testIds(renderer: ReactTestRenderer): string[] {
  return renderer.root.findAll(() => true)
    .map((node) => node.props?.["data-testid"])
    .filter((id): id is string => typeof id === "string");
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd moorestech_web/webui && npx vitest run src/features/eventLanguageGate`
Expected: FAIL（`./EventLanguageGate` が解決できない）

- [ ] **Step 3: コンポーネントを実装する**

`moorestech_web/webui/src/features/eventLanguageGate/EventLanguageGate.tsx`:

```tsx
import { useEffect, useState } from "react";
import { Button, Group, Overlay, Portal, Stack, Text, Title } from "@mantine/core";
import { dispatchAction, localizationLanguagesUrl, Topics, useTopicSelector } from "@/bridge";
import { DictionaryIndependentText } from "@/shared/i18n";

// 選ばせる対象が辞書そのものなのでt()を通さない。英語固定はADR 0040の裁定
// The dictionary itself is what gets chosen, so this bypasses t(); English-only is the ADR 0040 ruling
const HeadingText = "Select Language";

type LanguageEntry = {
  code: string;
  displayName: string;
};

type LanguageListState = {
  status: "loading" | "error" | "ready";
  entries: LanguageEntry[];
};

// 出展モードの開始ゲート。待機中だけ不透明な全画面で操作を塞ぎ、押下でゲームを始める
// The event-mode start gate; blocks input behind an opaque full screen while waiting and starts the game on press
export function EventLanguageGate() {
  const waiting = useTopicSelector(Topics.eventLanguageGate, (d) => d?.waiting ?? false);
  const [languages, setLanguages] = useState<LanguageListState>({ status: "loading", entries: [] });
  const [reloadCount, setReloadCount] = useState(0);

  useEffect(() => {
    // HTTP境界の失敗はerrorとして持ち、unmount時は遅延応答を破棄する
    // Keep HTTP boundary failures as an error state and discard late responses after unmount
    const abort = new AbortController();
    setLanguages({ status: "loading", entries: [] });
    void fetch(localizationLanguagesUrl, { signal: abort.signal })
      .then((response) => response.ok
        ? response.json() as Promise<unknown>
        : Promise.reject(new Error(`Failed to load languages: HTTP ${response.status}`)))
      .then((data) => {
        if (!abort.signal.aborted) setLanguages({ status: "ready", entries: toLanguageEntries(data) });
      })
      .catch(() => {
        if (!abort.signal.aborted) setLanguages({ status: "error", entries: [] });
      });
    return () => abort.abort();
  }, [reloadCount]);

  if (!waiting) return null;

  return (
    <Portal>
      <Overlay fixed center backgroundOpacity={1} color="#000" zIndex="var(--z-portal-event-language-gate)" data-testid="event-language-gate">
        <Stack align="center" gap="xl">
          <Title order={1} c="white">{HeadingText}</Title>
          {languages.status === "error"
            ? (
              <Stack align="center" gap="sm">
                {/* 一覧取得の失敗は辞書に依存しないリテラルで伝える */}
                {/* Report the list failure with copy that does not depend on the dictionary */}
                <Text c="white" data-testid="event-language-gate-error">
                  {DictionaryIndependentText.languageListLoadFailed}
                </Text>
                <Button onClick={() => setReloadCount((count) => count + 1)} data-testid="event-language-gate-retry">
                  {DictionaryIndependentText.retry}
                </Button>
              </Stack>
            )
            : (
              <Group justify="center" gap="lg">
                {languages.entries.map((language) => (
                  <Button
                    key={language.code}
                    size="xl"
                    data-testid={`event-language-gate-option-${language.code}`}
                    onClick={() => void dispatchAction("event_mode.select_language", { locale: language.code })}
                  >
                    {language.displayName}
                  </Button>
                ))}
              </Group>
            )}
        </Stack>
      </Overlay>
    </Portal>
  );
}

function toLanguageEntries(data: unknown): LanguageEntry[] {
  // 外部JSONは完全なcode/displayName組だけを表示候補として受理する
  // Accept only complete code/displayName pairs from external JSON as display candidates
  if (!Array.isArray(data)) return [];
  return data.filter((entry): entry is LanguageEntry =>
    typeof entry === "object"
    && entry !== null
    && "code" in entry
    && typeof entry.code === "string"
    && "displayName" in entry
    && typeof entry.displayName === "string");
}
```

`moorestech_web/webui/src/features/eventLanguageGate/index.ts`:

```ts
export { EventLanguageGate } from "./EventLanguageGate";
```

- [ ] **Step 4: z 層トークンを足して層序を固定する**

`moorestech_web/webui/src/app/tokens.css` の `--z-portal-reconnect: 2000;` の直後へ:

```css
  /* 出展モードの言語選択ゲートは再接続オーバーレイより前へ出す（待機中は再接続表示より優先） */
  /* The event-mode language gate stands ahead of the reconnect overlay while waiting */
  --z-portal-event-language-gate: 2500;
```

`moorestech_web/webui/src/app/zLayerTokens.test.ts` の「body直下Portal」describe へ追記:

```ts
  it("出展モードの言語選択ゲートは再接続オーバーレイより前に立つ", () => {
    // ゲート待機中にWSが切れると再接続オーバーレイが被さり、言語ボタンが押せなくなる
    // A WS drop during the wait would cover the language buttons with the reconnect overlay
    expect(portalLayer("reconnect")).toBeLessThan(portalLayer("event-language-gate"));
  });
```

- [ ] **Step 5: App へ組み込む**

`moorestech_web/webui/src/app/App.tsx`:
- import 追加: `import { EventLanguageGate } from "@/features/eventLanguageGate";`
- 辞書エラーオーバーレイ（`status === "error"` のブロック）の直後、最後の `</div>` の直前へ:

```tsx
      {/* 出展モードの開始ゲート。再接続表示より前へ出し、待機中の操作を全て塞ぐ */}
      {/* The event-mode start gate; sits ahead of the reconnect overlay and blocks every input while waiting */}
      <EventLanguageGate />
```

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `cd moorestech_web/webui && npx tsc --noEmit`
Expected: エラーなし
Run: `cd moorestech_web/webui && npx vitest run src/features/eventLanguageGate src/app/zLayerTokens.test.ts`
Expected: PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_web/webui/src/features/eventLanguageGate moorestech_web/webui/src/app
git commit -m "feat: 出展モードの全画面言語選択画面を追加"
```

---

### Task 5: 起動シーケンスへの配線と無操作監視の武装タイミング移設

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeStartGate.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeAutoStart.cs:29-33`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventIdleQuitWatcher.cs:28-33`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventIdleTimer.cs:20-24`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs:39-44`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventIdleTimerTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventModeStartGateTest.cs`

**Interfaces:**
- Consumes: `EventLanguageGateBinder.Bind(WebSocketHub) -> EventLanguageGate`（Task 2）、`EventExhibitionSettings.FromEnvironment()`、`EventIdleQuitWatcher.Create(int)`、`Client.WebUiHost.Boot.WebUiHost.Hub`
- Produces: `Client.Starter.EventMode.EventModeStartGate.WaitForLanguageSelectionAsync() -> UniTask`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventModeStartGateTest.cs`:

```csharp
using System;
using System.Text.RegularExpressions;
using Client.Starter.EventMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Client.Tests.EventMode
{
    public class EventModeStartGateTest
    {
        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("MOORESTECH_EVENT_MODE", null);
            Environment.SetEnvironmentVariable("MOORESTECH_EVENT_MODE_EDITOR", null);
            foreach (var watcher in Object.FindObjectsByType<EventIdleQuitWatcher>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(watcher.gameObject);
            }
        }

        // 出展モードでない起動は1フレームも待たず、無操作監視も作らない
        // A non-event-mode boot waits zero frames and creates no idle watcher
        [Test]
        public void 出展モードでなければ即座に完了し監視も作らない()
        {
            var task = EventModeStartGate.WaitForLanguageSelectionAsync();

            Assert.IsTrue(task.Status.IsCompletedSuccessfully());
            Assert.AreEqual(0, Object.FindObjectsByType<EventIdleQuitWatcher>(FindObjectsSortMode.None).Length);
        }

        // WebUiHost未起動では画面を出せない。無人ブースを止めないため即開始し監視だけ始める
        // With no WebUiHost there is no screen; keep the unattended booth alive by starting and watching immediately
        [Test]
        public void WebUiHost未起動なら即開始して監視を作る()
        {
            Environment.SetEnvironmentVariable("MOORESTECH_EVENT_MODE", "1");
            Environment.SetEnvironmentVariable("MOORESTECH_EVENT_MODE_EDITOR", "1");
            LogAssert.Expect(LogType.Error, new Regex("WebUiHost"));

            var task = EventModeStartGate.WaitForLanguageSelectionAsync();

            Assert.IsTrue(task.Status.IsCompletedSuccessfully());
            Assert.AreEqual(1, Object.FindObjectsByType<EventIdleQuitWatcher>(FindObjectsSortMode.None).Length);
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EventModeStartGateTest"`
Expected: FAIL（`EventModeStartGate` が存在せずコンパイルエラー）

- [ ] **Step 3: ゲートの起動側を実装する**

`moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeStartGate.cs`:

```csharp
using Client.WebUiHost.Game.EventMode;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.EventMode
{
    /// <summary>
    /// 出展モードの開始ゲート。言語が選ばれるまで開始を止め、選択と同時に無操作監視を始める。
    /// The event-mode start gate: holds the start until a language is chosen and begins idle watching at that moment.
    /// </summary>
    public static class EventModeStartGate
    {
        public static async UniTask WaitForLanguageSelectionAsync()
        {
            var settings = EventExhibitionSettings.FromEnvironment();
            if (!settings.IsEnabled) return;

            // 画面を出せないなら無人ブースを止めない方を採る。英語のまま開始し監視だけ始める
            // With no screen to show, keeping the unattended booth alive wins: start in English and only begin watching
            var hub = Client.WebUiHost.Boot.WebUiHost.Hub;
            if (hub == null)
            {
                Debug.LogError("EventModeStartGate: WebUiHostが起動しておらず言語選択を出せないため英語のまま開始します");
                EventIdleQuitWatcher.Create(settings.IdleTimeoutSeconds);
                return;
            }

            var gate = EventLanguageGateBinder.Bind(hub);
            await gate.WaitForSelectionAsync();

            // 監視の生成が武装そのもの。待機中は個体が存在しないので無操作終了は起こり得ない
            // Creating the watcher is the arming itself: no instance exists while waiting, so an idle quit cannot fire
            EventIdleQuitWatcher.Create(settings.IdleTimeoutSeconds);
        }
    }
}
```

- [ ] **Step 4: 起動時の武装と旧リセット経路を撤去する**

`EventModeAutoStart.cs` の `AutoStartIfEventMode()` から次の1行を削除する:

```csharp
            EventIdleQuitWatcher.Create(settings.IdleTimeoutSeconds);
```

`EventIdleQuitWatcher.cs` から `Start()` メソッド全体を削除する（武装点が生成へ移り `GameInitializedEvent` 購読が不要になるため）:

```csharp
        private void Start()
        {
            // 起動所要時間を無操作時間に数えない。ロード完了時点から計り直す
            // Boot time must not count as idle time, so restart the measurement when loading completes
            GameInitializedEvent.OnGameInitialized.Subscribe(_ => _idleTimer.Reset()).AddTo(this);
        }
```

削除にともない未使用になる `using UniRx;` を消す（`Client.Game.Common` は `GameShutdownEvent` で使い続けるため残す）。
`Create` のコメントを武装点の説明へ差し替える:

```csharp
        // 生成が武装そのもの。言語選択後にだけ作られるため、待機中は無操作終了が起こり得ない
        // Creation is the arming itself: built only after the language is chosen, so an idle quit cannot fire while waiting
        public static EventIdleQuitWatcher Create(int idleTimeoutSeconds)
```

`EventIdleTimer.cs` から `Reset()` を削除する（呼び出し元が消えるため。デバッグ/テスト専用publicを残さない規約）。
あわせて `EventIdleTimerTest.cs` の `Resetで積算が0へ戻る` テストを削除する。

- [ ] **Step 5: 起動シーケンスへ挟む**

`MainGameInitializationFinalizer.cs` の `FinalizeAsync()` 冒頭、`var starter = ...` の**前**へ:

```csharp
            // 出展モードは言語が決まるまで開始を止める。スキットとチュートリアルが英語で走り出す前に挟む
            // Event mode holds the start until a language is chosen, ahead of skits and tutorials starting in English
            await EventMode.EventModeStartGate.WaitForLanguageSelectionAsync();
```

同ファイルの名前空間は `Client.Starter.Initialization` なので `EventMode.EventModeStartGate` で相対解決される。
解決できなければ完全修飾 `Client.Starter.EventMode.EventModeStartGate` にする。

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 0 errors
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EventModeStartGateTest|EventIdleTimerTest|EventExhibitionModeTest|EventLanguageGate"`
Expected: PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Tests/EventMode
git commit -m "feat: 言語選択まで開始を止め選択で無操作監視を武装する"
```

---

### Task 6: 出展モード通し確認（実機・手動）

**Files:**
- Modify: `docs/adr/0040-event-mode-language-select-gate.md`（確認結果の追記のみ）

**Interfaces:**
- Consumes: Task 1〜5 の実装一式、`scripts/event/start-gamescom-loop.command`
- Produces: 受け入れ確認の記録（ADR 末尾へ「## 実機確認」節を追記）

出展モードは `RuntimeInitializeOnLoadMethod` と環境変数と CEF と内蔵サーバーが揃って初めて成立し、
かつ Editor で有効化すると既定ワールドを不可逆に削除するため、通し確認は**ブースで実際に使う経路と同じ
Release ビルド**で行う。ADR 0035 が確立した検証経路と同じである。

- [ ] **Step 1: macOS Release ビルドを作る**

Run: `uloop compile --project-path ./moorestech_client`（0 errors を確認）
Unity Editor のメニュー `moorestech > Build > Release (Local)`（`docs/adr/0036-release-local-build-menu-entry.md` の入口）で
macOS ビルドを出し、出力された `moorestech.app` を `scripts/event/start-gamescom-loop.command` と同じディレクトリへ置く。

- [ ] **Step 2: 起動ループを回して待機画面を確認する**

Run: `scripts/event/start-gamescom-loop.command`
Expected（順に確認する）:
1. ロード画面のあと、**不透明な黒背景に `Select Language` と English / 日本語 / Deutsch の3ボタン**が出る
2. **マウスを動かす・空白をクリックする・キーを叩く**を1分以上繰り返しても、画面がそのまま留まる
3. **5分以上放置**してもアプリが落ちず、待機画面のまま留まる（R3/R4 の実地確認）

- [ ] **Step 3: 言語選択後の挙動を確認する**

`日本語` を押す。
Expected:
1. 待機画面が消え、**オープニングスキットが日本語で再生される**（英語で先に流れていない）
2. スキット後にチュートリアルが日本語で始まる
3. その後**3分間何も触らない**とアプリが終了し、ループが再起動して再び `Select Language` が出る

- [ ] **Step 4: 通常起動の非退行を確認する**

Run: `uloop launch --project-path ./moorestech_client` で Editor を起動し、環境変数なしの通常ローカルプレイで PlayMode に入る。
Expected: 言語選択画面は出ず、従来どおり即座にゲームが始まりオープニングスキットが再生される。

- [ ] **Step 5: 確認結果を ADR へ追記してコミットする**

`docs/adr/0040-event-mode-language-select-gate.md` の末尾へ次を追記する（日付と `<sha>` は実行時の実値へ置き換える）:

```markdown
## 実機確認

2026-MM-DD、macOS Release ビルド（コミット `<sha>`）を `scripts/event/start-gamescom-loop.command` で起動し確認した。
- 待機画面でマウス移動・クリック・キー入力を繰り返しても終了しない（5分以上放置しても待機継続）
- 日本語を選ぶとオープニングスキットが日本語で再生される（英語での先行再生なし）
- プレイ開始後3分の無操作で終了し、ループが再起動して待機画面へ戻る
- 環境変数なしの通常起動では言語選択画面が出ない
```

```bash
git add docs/adr/0040-event-mode-language-select-gate.md
git commit -m "docs: ADR 0040に出展モードの実機確認結果を追記"
```

---

### Task 7: ブランチ全体のコードレビュー（省略不可）

**Files:**
- Modify: レビュー指摘に応じた全ファイル

- [ ] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、`master` からの全差分をレビュー対象にする。
これは**無条件に実行する最終タスク**であり、他タスクが全て通っていても省略できない。

- [ ] **Step 2: 機械的な指摘を適用する**

決定論チェック・reviewer 群の指摘のうち、機械的に確定するものを適用する。

- [ ] **Step 3: 設計判断の指摘をユーザーへ諮る**

設計判断を要する指摘は `AskUserQuestion` でまとめて裁定を仰ぐ。

- [ ] **Step 4: 再コンパイルとテストで健全性を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 0 errors
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EventMode|EventLanguageGate|WireContract"`
Expected: PASS
Run: `cd moorestech_web/webui && npx tsc --noEmit && npx vitest run src/bridge src/features/eventLanguageGate src/app`
Expected: PASS

- [ ] **Step 5: コミットして PR を作る**

```bash
git add -A
git commit -m "fix: コードレビュー指摘を反映"
```

`pr-create` スキルで PR を作成し、作成直後に `moores-wt rm <name>` で worktree と Unity Editor を畳む。

---

## 判断記録（ADR）

設計裁定の正本: [docs/adr/0040-event-mode-language-select-gate.md](../../adr/0040-event-mode-language-select-gate.md)
裁定台帳: `.decisions/2026-08-28-出展モードの言語選択はロード完了後の全画面ゲートにする.md`

planning 中に生じた判断:

- **武装の表現を「フラグ」ではなく「オブジェクトの生成」にした。** `EventIdleTimer` に armed フラグを足すのではなく、
  言語選択が済むまで `EventIdleQuitWatcher` を生成しない。待機中は入力を観測する主体そのものが存在しないため、
  「待機中に無操作終了が起きない」がフラグの取り違えでは破れない構造になる。
  出所: agent前提（R3/R4 を同一機構で満たす最小構成）
- **ゲートを `MainGameInitializationFinalizer.FinalizeAsync()` の先頭へ置いた。** `SkitFireManager` に suspend/flush を
  実装する案を退けた。スキットは `starter.StartGame()`（VContainer 構築）の中で発火し、チュートリアルは同メソッド後段の
  `ChallengeManager.ApplyInitialTutorials()` で発火するため、その手前で待てば両方が自動的に保留される。
  汎用基盤に出展モードの語彙を持ち込まない原則に合致する。
  出所: agent前提（AGENTS.md「汎用基盤にドメイン語彙を持ち込まない」）
- **ゲートサービスの置き場を `Client.WebUiHost` にした。** asmdef の参照方向が `Client.Starter → Client.WebUiHost` の
  一方向であるため、topic/action と同居させて Starter 側から await する。前例は `WebUiModalService`
  （`Client.Game` から `Client.WebUiHost` を参照できないため WebUiHost 側が所有する同型の解決）。
  出所: agent前提（`Client.Starter.asmdef` の references 実測）
- **言語一覧は既存 `/api/i18n-languages` を再利用し、topic payload は `{ waiting: bool }` だけにした。**
  `LanguageSelect.tsx` が同じ経路を踏んでおり、配信経路を二重化しない。
  出所: agent前提（前例一致）
- **`EventIdleTimer.Reset()` と `EventIdleQuitWatcher.Start()` を削除した。** 武装点が生成へ移り、
  `GameInitializedEvent` 購読と `Reset` の呼び出し元が消えるため。
  出所: agent前提（AGENTS.md「デバッグ/テスト専用publicをプロダクションに残さない」）
- **WebUiHost 起動失敗時は英語のまま即開始して監視を作る。** 無人ブースで画面が出せないまま永久停止するより、
  従来挙動（英語・3分リセット）へ落ちる方が運用上マシと判断した。エラーログを残す。
  出所: agent前提（R12。ADR 0030 の「終了＝リセット」で復旧が保証されるため）
- **言語一覧の取得失敗時は再試行ボタンを出す。** `LanguageSelect.tsx` の既存パターンをそのまま踏襲する。
  出所: agent前提（前例一致）
- **通し確認は Release ビルド＋起動ループの手動確認にした。** 出展モードは Editor で有効化すると既定ワールドを
  不可逆に削除するうえ、`RuntimeInitializeOnLoadMethod` と環境変数と CEF が揃って初めて成立するため、
  自動テストは NUnit/vitest の単位に留め、通しはブースで使う経路そのもので見る。
  出所: agent前提（ADR 0035 の前例）
