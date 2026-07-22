# サーバー発 汎用通知基盤 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** サーバー内の任意のシステムが `NotificationService.Notify(playerId, ...)` を呼ぶだけでWeb UIにゲーム内通知が表示される汎用経路を作り、達成系3種＋無言失敗上位5プロトコルを配線する。

**Architecture:** サーバー側 `NotificationService`（デデュープ込み）→ 既存 `EventProtocolProvider` 経由で `va:event:notification` 1本 → クライアント `NotificationTopic`（SubscribeEventResponse直接購読、既存前例 `TrainRidingTopic` と同型）→ Web `features/notification` トースト表示。Spec: `docs/superpowers/specs/2026-07-22-server-notification-infrastructure-design.md`。

**Tech Stack:** C# (MessagePack / UniRx / Microsoft.Extensions.DependencyInjection) + TypeScript (React / zustand / Mantine / vitest)

## Global Constraints

- 1ファイル200行以下・partial絶対禁止・1ディレクトリ10ファイルまで
- 主要処理セクションに日本語・英語2行セットコメント（各1行厳守）
- イベントはUniRx（`Subject`+`IObservable`）。C# `event Action` 禁止
- try-catch原則禁止・デフォルト引数禁止・単純getter/setterプロパティ禁止（Setは`SetHoge`メソッド）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client` を実行
- .metaファイル手動作成禁止（Unityが生成）
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。ドメインリロードエラー時は45秒待ってリトライ
- git worktree頻用のため作業開始時に `pwd` 確認。タスクごとにコミット
- Webテスト: `cd moorestech_web/webui && npm run test`

## 配置と前例（spec-architecture-review済み）

| 配置決定 | 前例 |
|---|---|
| `NotificationService`/`NotificationMessagePack` を `Server.Event/Notification/` に新設（Server.Protocol→Server.Event参照は既存） | `RegisterPlayedSkitProtocol.cs` がプロトコルから `EventProtocolProvider` を使用 |
| MessagePack DTOはstatic factory・`[Obsolete]`空ctor・enum直接シリアライズ | `RailConnectionEditRequest`（factory）、`UnlockedEventPacket`（Event型DTO） |
| 達成系配線クラスは `IBootInitializable`＋UniRx購読＋DI具象登録 | `ResearchCompleteEventPacket.cs`（同型） |
| DI登録は `MoorestechServerDIContainerGenerator.cs` にAddSingleton | 同ファイルのイベントレシーバー登録ブロック |
| 通知はtransient（初期データ・スナップショット無し）。3点セットの②③は「状態」でないため対象外 | web側 `playtestDomQuery` topic（"transient events without snapshots"コメント） |
| クライアントTopicが `SubscribeEventResponse` を直接購読 | `TrainRidingTopic.cs:34`、`MachineRecipesTopic.cs:33` |
| Web通知UIは `features/toast` と同型の別feature | `toastStore.ts`/`ToastHost.tsx` |
| 時刻は `DateTime.UtcNow` 直書き（時刻抽象は存在しない） | `WorldSettingsDatastore.cs` |

**新規パターン（specで裁定済み）**: 失敗伝達を既存Responseでなく中央イベント1本に集約する点、通知にtransient扱いで初期データを作らない点は、specでユーザー承認済み。

---

### Task 1: サーバー基盤 — NotificationMessagePack + NotificationService

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Event/Notification/NotificationMessagePack.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Event/Notification/NotificationService.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（イベントレシーバー登録ブロック付近）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/NotificationServiceTest.cs`

**Interfaces:**
- Consumes: `EventProtocolProvider.AddEvent(int playerId, string tag, byte[] payload)` / `AddBroadcastEvent(string tag, byte[] payload)`（既存）
- Produces: `NotificationService.EventTag = "va:event:notification"`、`void Notify(int playerId, NotificationMessagePack)`、`void NotifyAll(NotificationMessagePack)`、`void SetCooldownDuration(TimeSpan)`、`NotificationMessagePack.CreateAchievement(string messageId, string[] messageParams)` / `CreateAchievementWithItem(string messageId, string[] messageParams, ItemId itemId)` / `CreateOperationDenied(string messageId, string[] messageParams)`、`NotificationCategory { Achievement, OperationDenied }`

- [ ] **Step 1: NotificationMessagePack.cs を作成**

```csharp
using System;
using Core.Master;
using MessagePack;

namespace Server.Event.Notification
{
    public enum NotificationCategory
    {
        Achievement,
        OperationDenied,
    }

    [MessagePackObject]
    public class NotificationMessagePack
    {
        // EventのMessagePackはProtocolMessagePackBaseを継承しない。Key(0)から開始
        // Event MessagePacks do not inherit ProtocolMessagePackBase; keys start at 0
        [Key(0)] public NotificationCategory Category { get; set; }
        [Key(1)] public string MessageId { get; set; }
        [Key(2)] public string[] MessageParams { get; set; }
        [Key(3)] public ItemId ItemId { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public NotificationMessagePack() { }

        // 生成はstatic factory経由のみ。カテゴリごとの必要フィールドを型で明示する
        // Construction goes through static factories so each category's required fields are explicit
        private NotificationMessagePack(NotificationCategory category, string messageId, string[] messageParams, ItemId itemId)
        {
            Category = category;
            MessageId = messageId;
            MessageParams = messageParams;
            ItemId = itemId;
        }

        public static NotificationMessagePack CreateAchievement(string messageId, string[] messageParams)
            => new(NotificationCategory.Achievement, messageId, messageParams, ItemMaster.EmptyItemId);

        public static NotificationMessagePack CreateAchievementWithItem(string messageId, string[] messageParams, ItemId itemId)
            => new(NotificationCategory.Achievement, messageId, messageParams, itemId);

        public static NotificationMessagePack CreateOperationDenied(string messageId, string[] messageParams)
            => new(NotificationCategory.OperationDenied, messageId, messageParams, ItemMaster.EmptyItemId);
    }
}
```

注意: `ItemMaster.EmptyItemId` の実在を `grep -rn "EmptyItemId" moorestech_server/Assets/Scripts/Core.Master/` で確認。無ければ `new ItemId(0)` 相当の既存センチネル（`ItemMaster` 内定義）に合わせる。

- [ ] **Step 2: NotificationService.cs を作成**

```csharp
using System;
using System.Collections.Generic;
using MessagePack;

namespace Server.Event.Notification
{
    /// <summary>
    /// サーバー内の任意システムからプレイヤー画面へ通知を送る汎用サービス
    /// Generic service that lets any server system push a notification to the player's screen
    /// </summary>
    public class NotificationService
    {
        public const string EventTag = "va:event:notification";

        // ブロードキャストのクールダウンキー用の擬似プレイヤーID
        // Pseudo player id used as the cooldown key for broadcasts
        private const int BroadcastPlayerId = -1;

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly Dictionary<(int playerId, NotificationCategory category, string messageId), DateTime> _lastSentUtc = new();
        private TimeSpan _cooldownDuration = TimeSpan.FromSeconds(3);

        public NotificationService(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
        }

        public void SetCooldownDuration(TimeSpan cooldownDuration)
        {
            _cooldownDuration = cooldownDuration;
        }

        public void Notify(int playerId, NotificationMessagePack notification)
        {
            // 同一キーの連打はクールダウンで抑制しワイヤにスパムを乗せない
            // Suppress same-key bursts by cooldown so spam never reaches the wire
            if (!TryPassCooldown(playerId, notification)) return;
            _eventProtocolProvider.AddEvent(playerId, EventTag, MessagePackSerializer.Serialize(notification));
        }

        public void NotifyAll(NotificationMessagePack notification)
        {
            if (!TryPassCooldown(BroadcastPlayerId, notification)) return;
            _eventProtocolProvider.AddBroadcastEvent(EventTag, MessagePackSerializer.Serialize(notification));
        }

        private bool TryPassCooldown(int playerId, NotificationMessagePack notification)
        {
            var key = (playerId, notification.Category, notification.MessageId);
            var now = DateTime.UtcNow;
            if (_lastSentUtc.TryGetValue(key, out var last) && now - last < _cooldownDuration) return false;
            _lastSentUtc[key] = now;
            return true;
        }
    }
}
```

- [ ] **Step 3: DI登録**

`MoorestechServerDIContainerGenerator.cs` の `services.AddSingleton<EventProtocolProvider, EventProtocolProvider>();` 行の直後に追加:

```csharp
            services.AddSingleton<Server.Event.Notification.NotificationService>();
```

（既存usingに `Server.Event.Notification` を足して `services.AddSingleton<NotificationService>();` でも可。ファイル内の既存スタイルに合わせる）

- [ ] **Step 4: テストを作成**

`NotificationServiceTest.cs`（既存 `EventTestUtil.RegisterCaptureSink` を利用）:

```csharp
using System;
using System.Linq;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class NotificationServiceTest
    {
        private const int PlayerId = 1;

        [Test]
        public void NotifySendsEventAndCooldownSuppressesDuplicate()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var service = serviceProvider.GetService<NotificationService>();

            // 同一キー2連打は1件だけ通る
            // Two same-key bursts pass only once
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.denied", new[] { "p0" }));
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.denied", new[] { "p0" }));
            var events = sink.TakeAll().Where(e => e.Tag == NotificationService.EventTag).ToList();
            Assert.AreEqual(1, events.Count);

            var data = MessagePackSerializer.Deserialize<NotificationMessagePack>(events[0].Payload);
            Assert.AreEqual(NotificationCategory.OperationDenied, data.Category);
            Assert.AreEqual("test.denied", data.MessageId);
            Assert.AreEqual("p0", data.MessageParams[0]);
        }

        [Test]
        public void DifferentMessageIdPassesAndZeroCooldownAllowsRepeat()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var service = serviceProvider.GetService<NotificationService>();

            // 別MessageIdは抑制されない
            // A different MessageId is not suppressed
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.a", Array.Empty<string>()));
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.b", Array.Empty<string>()));
            Assert.AreEqual(2, sink.TakeAll().Count(e => e.Tag == NotificationService.EventTag));

            // クールダウン0なら同一キーも再送される
            // With zero cooldown the same key is sent again
            service.SetCooldownDuration(TimeSpan.Zero);
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.a", Array.Empty<string>()));
            service.Notify(PlayerId, NotificationMessagePack.CreateOperationDenied("test.a", Array.Empty<string>()));
            Assert.AreEqual(2, sink.TakeAll().Count(e => e.Tag == NotificationService.EventTag));
        }

        [Test]
        public void NotifyAllBroadcastsToRegisteredSink()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var service = serviceProvider.GetService<NotificationService>();

            service.NotifyAll(NotificationMessagePack.CreateAchievement("test.achievement", Array.Empty<string>()));
            var events = sink.TakeAll().Where(e => e.Tag == NotificationService.EventTag).ToList();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(NotificationCategory.Achievement, MessagePackSerializer.Deserialize<NotificationMessagePack>(events[0].Payload).Category);
        }
    }
}
```

- [ ] **Step 5: コンパイル → テスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "NotificationServiceTest"
```
Expected: コンパイルエラー0、テスト3件PASS

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Event/Notification/ moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/NotificationServiceTest.cs
git commit -m "feat(notification): サーバー発汎用通知サービスとイベントパケット基盤を追加"
```
（Unity起動中なら生成された.metaも `git add` に含める）

---

### Task 2: 達成系配線 — AchievementNotificationWiring

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Event/Notification/AchievementNotificationWiring.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/AchievementNotificationWiringTest.cs`

**Interfaces:**
- Consumes: Task 1の `NotificationService` / factory群。`ResearchEvent.OnResearchCompleted`（`IObservable<(int playerId, ResearchNodeMasterElement researchNode)>`）、`ChallengeEvent.OnCompleteChallenge`、`IGameUnlockStateDataController.OnUnlockItem` 等（すべて既存）
- Produces: MessageId規約 `"achievement.researchCompleted"`（params: [研究名]）、`"achievement.challengeCompleted"`（params: [チャレンジ名]）、`"achievement.unlockedItem"`（ItemId付き）、`"achievement.unlockedCraftRecipe"` / `"achievement.unlockedMachineRecipe"` / `"achievement.unlockedBlock"` / `"achievement.unlockedTrainCar"` / `"achievement.unlockedConnectTool"`（params空）

- [ ] **Step 1: マスタ生成物の表示名プロパティを確認**

```bash
grep -n "public string" $(grep -rln "class ResearchNodeMasterElement" moorestech_server/Assets/Scripts) | head -20
grep -n "public string" $(grep -rln "class ChallengeMasterElement" moorestech_server/Assets/Scripts) | head -20
```
研究名・チャレンジ名に相当するプロパティ名（例: `ResearchNodeName` / `Title`）を特定し、Step 2のコードをその名前に合わせる。表示名プロパティが存在しない場合はparamsをGUID文字列にし、Web側テンプレートを名前無し文言（例: "Research completed"）に変更する。

- [ ] **Step 2: AchievementNotificationWiring.cs を作成**

```csharp
using System;
using Game.Challenge;
using Game.Context;
using Game.Research;
using Game.UnlockState;
using UniRx;

namespace Server.Event.Notification
{
    /// <summary>
    /// ドメインの達成イベントを購読し通知基盤へ配線する
    /// Subscribes to domain achievement events and wires them into the notification service
    /// </summary>
    public class AchievementNotificationWiring : IBootInitializable
    {
        private readonly NotificationService _notificationService;
        private readonly ResearchEvent _researchEvent;
        private readonly ChallengeEvent _challengeEvent;
        private readonly IGameUnlockStateDataController _unlockState;

        public AchievementNotificationWiring(NotificationService notificationService, ResearchEvent researchEvent, ChallengeEvent challengeEvent, IGameUnlockStateDataController unlockState)
        {
            _notificationService = notificationService;
            _researchEvent = researchEvent;
            _challengeEvent = challengeEvent;
            _unlockState = unlockState;
        }

        public void Load()
        {
            // 研究完了は完了プレイヤー宛て、チャレンジ・アンロックは全員宛て
            // Research completion targets the completing player; challenge/unlock broadcast to all
            _researchEvent.OnResearchCompleted.Subscribe(data => _notificationService.Notify(data.playerId,
                NotificationMessagePack.CreateAchievement("achievement.researchCompleted", new[] { data.researchNode.ResearchNodeName })));

            _challengeEvent.OnCompleteChallenge.Subscribe(property => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.challengeCompleted", new[] { property.ChallengeTask.ChallengeMasterElement.Title })));

            _unlockState.OnUnlockItem.Subscribe(itemId => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievementWithItem("achievement.unlockedItem", Array.Empty<string>(), itemId)));
            _unlockState.OnUnlockCraftRecipe.Subscribe(_ => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.unlockedCraftRecipe", Array.Empty<string>())));
            _unlockState.OnUnlockMachineRecipe.Subscribe(_ => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.unlockedMachineRecipe", Array.Empty<string>())));
            _unlockState.OnUnlockBlock.Subscribe(_ => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.unlockedBlock", Array.Empty<string>())));
            _unlockState.OnUnlockTrainCar.Subscribe(_ => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.unlockedTrainCar", Array.Empty<string>())));
            _unlockState.OnUnlockConnectTool.Subscribe(_ => _notificationService.NotifyAll(
                NotificationMessagePack.CreateAchievement("achievement.unlockedConnectTool", Array.Empty<string>())));
        }
    }
}
```

`ResearchNodeName` / `Title` はStep 1で確認した実プロパティ名に置換。`IBootInitializable` のnamespace（`Game.Context`）は `ResearchCompleteEventPacket.cs` のusingに合わせる。ChallengeCategoryアンロックはチャレンジ達成通知と重複するため配線しない（v1判断）。

- [ ] **Step 3: DI登録**

イベントレシーバー登録ブロック（`services.AddSingleton<ResearchCompleteEventPacket>();` の並び）に追加:

```csharp
            services.AddSingleton<AchievementNotificationWiring>();
```
`AddInitializableForwarding()` が `IBootInitializable` を自動転送するため、これだけで `Load()` が呼ばれる。

- [ ] **Step 4: テストを作成**

既存 `ResearchCompleteEventPacketTest.cs` と同じヘルパー（`CompleteResearchForTest`, `Research1Guid` は `Tests.CombinedTest.Game.ResearchDataStoreTest` から static using）を流用:

```csharp
using System.Linq;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class AchievementNotificationWiringTest
    {
        [Test]
        public void ResearchCompleteFiresAchievementNotification()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            CompleteResearchForTest(serviceProvider, Research1Guid);

            var notifications = sink.TakeAll().Where(e => e.Tag == NotificationService.EventTag).ToList();
            Assert.AreEqual(1, notifications.Count(e =>
            {
                var data = MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload);
                return data.Category == NotificationCategory.Achievement && data.MessageId == "achievement.researchCompleted";
            }));
        }
    }
}
```

注意: 研究完了は連鎖アンロック（レシピ等）で `achievement.unlocked*` 通知も同時に飛び得るため、アサートは合計件数でなく該当MessageIdの件数で行う（上記の形を維持）。

- [ ] **Step 5: コンパイル → テスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "AchievementNotificationWiringTest|NotificationServiceTest"
```
Expected: 全PASS

- [ ] **Step 6: コミット**

```bash
git add -A moorestech_server/Assets/Scripts/Server.Event/Notification/ moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/AchievementNotificationWiringTest.cs
git commit -m "feat(notification): 研究完了・チャレンジ達成・アンロックの達成通知を配線"
```

---

### Task 3: 失敗系配線 (1/3) — completeResearch / oneClickCraft

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CompleteResearchProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/OneClickCraft.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/OperationDeniedNotificationTest.cs`

**Interfaces:**
- Consumes: Task 1の `NotificationService`（`serviceProvider.GetService<NotificationService>()` で取得。前例: `RegisterPlayedSkitProtocol.cs`）
- Produces: MessageId `"denied.researchNotCompletable"`、`"denied.craftResultFull"`、`"denied.craftMaterialShortage"`（すべてparams空）

- [ ] **Step 1: CompleteResearchProtocol に失敗通知を追加**

コンストラクタと `GetResponse` を以下に変更（フィールド追加＋失敗分岐に1行）:

```csharp
        private readonly IResearchDataStore _researchDataStore;
        private readonly NotificationService _notificationService;

        public CompleteResearchProtocol(ServiceProvider serviceProvider)
        {
            _researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            _notificationService = serviceProvider.GetService<NotificationService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<RequestCompleteResearchMessagePack>(payload);

            // 研究完了を試みる
            var isSuccess = _researchDataStore.CompleteResearch(request.ResearchGuid, request.PlayerId);
            var nodeStates = _researchDataStore.GetResearchNodeStates(request.PlayerId);

            // 失敗（前提未達・素材不足等）は通知基盤経由でプレイヤーに知らせる
            // Report failure (prerequisites unmet, materials short, etc.) via the notification service
            if (!isSuccess) _notificationService.Notify(request.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.researchNotCompletable", Array.Empty<string>()));

            return new ResponseCompleteResearchMessagePack(isSuccess, request.ResearchGuid.ToString(), nodeStates);
        }
```
using追加: `using Server.Event.Notification;`（`System` は既存）。

- [ ] **Step 2: OneClickCraft に失敗理由の判別と通知を追加**

`GetResponse` のクラフト可否チェック部分を、理由が分かる2段階チェックに置換:

```csharp
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<RequestOneClickCraftProtocolMessagePack>(payload);
            
            var craftConfig = MasterHolder.CraftRecipeMaster.GetCraftRecipe(data.CraftRecipeGuid);
            //プレイヤーインベントリを取得
            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            var mainInventory = playerInventory.MainOpenableInventory;
            
            // クラフト不可の理由を判別して通知し中断する
            // Identify why crafting is impossible, notify the player, and abort
            if (!CanInsertResult(mainInventory, craftConfig))
            {
                _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.craftResultFull", Array.Empty<string>()));
                return null;
            }
            if (!HasRequiredItems(mainInventory, craftConfig))
            {
                _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.craftMaterialShortage", Array.Empty<string>()));
                return null;
            }
            
            //クラフト可能な場合はクラフトを実行
            ...（以降は既存のまま: ConsumptionItem → InsertItem → InvokeCraftItem → return null）
        }
```

既存 `IsCraftable` を2つのprivate staticメソッドに分割する（ロジックは既存の移植であり新規作成禁止）:

```csharp
        // 既存IsCraftable前半（InsertionCheck部分）をそのまま移す
        // Move the existing IsCraftable's InsertionCheck part verbatim
        private static bool CanInsertResult(IOpenableInventory mainInventory, CraftRecipeMasterElement recipe)
        {
            var resultItem = ServerContext.ItemStackFactory.Create(recipe.CraftResultItemGuid, recipe.CraftResultCount);
            var resultItemList = new List<IItemStack> { resultItem };
            return mainInventory.InsertionCheck(resultItemList);
        }

        // 既存IsCraftable後半（必要アイテム集計〜所持チェック）をそのまま移す
        // Move the existing IsCraftable's required-item check part verbatim
        private static bool HasRequiredItems(IOpenableInventory mainInventory, CraftRecipeMasterElement recipe)
        {
            ...（既存IsCraftableのrequiredItems集計〜checkResult比較のコードをそのまま）
            return true;
        }
```
`IsCraftable` 本体は削除。コンストラクタに `_notificationService = serviceProvider.GetService<NotificationService>();` とフィールド、using `Server.Event.Notification` を追加。分割後にファイルが200行を超えないことを確認（現状約190行なので、超える場合はチェックロジックを `PacketResponse/Util/` 配下の既存構成に合わせた static クラスへ抽出）。

- [ ] **Step 3: テストを作成**

`OperationDeniedNotificationTest.cs`。プロトコル呼び出しは同ディレクトリの既存PacketTest（例: `Tests/CombinedTest/Server/PacketTest/` 配下で `PacketResponseCreator` を使っているテスト）の呼び出しパターンを確認してそれに合わせる（`Create()` が返す第1要素が `PacketResponseCreator`）:

```csharp
using System;
using System.Linq;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class OperationDeniedNotificationTest
    {
        private const int PlayerId = 1;

        [Test]
        public void CompleteResearchFailureFiresDeniedNotification()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // 素材ゼロの初期状態で研究完了を要求 → 失敗して拒否通知が飛ぶ
            // Request research completion with an empty inventory → fails and fires a denied notification
            // Research1Guid等のGUIDは ResearchDataStoreTest の定義を流用する
            var request = new CompleteResearchProtocol.RequestCompleteResearchMessagePack(PlayerId, Tests.CombinedTest.Game.ResearchDataStoreTest.Research1Guid);
            packetResponse.GetPacketResponse(MessagePackSerializer.Serialize(request).ToList());

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.researchNotCompletable"));
        }

        [Test]
        public void OneClickCraftMaterialShortageFiresDeniedNotification()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // 素材ゼロでクラフト要求 → 素材不足通知
            // Request crafting with no materials → material shortage notification
            // レシピGUIDはForUnitTestModのcraftRecipes.jsonから既存テストが使っているものを流用する
            var recipeGuid = MasterHolder.CraftRecipeMaster.CraftRecipes.Data[0].CraftRecipeGuid;
            var request = new OneClickCraft.RequestOneClickCraftProtocolMessagePack(PlayerId, recipeGuid);
            packetResponse.GetPacketResponse(MessagePackSerializer.Serialize(request).ToList());

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.craftMaterialShortage"));
        }

        private static System.Collections.Generic.List<NotificationMessagePack> TakeDenied(CapturedEventSink sink)
        {
            return sink.TakeAll()
                .Where(e => e.Tag == NotificationService.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload))
                .Where(n => n.Category == NotificationCategory.OperationDenied)
                .ToList();
        }
    }
}
```
`GetPacketResponse` の正確なシグネチャ（`List<byte>` か `byte[]` か）と `MasterHolder.CraftRecipeMaster` のレシピ列挙プロパティ名は既存PacketTest・既存OneClickCraftテスト（あれば）に合わせて修正する。usingに `Core.Master` を追加。

- [ ] **Step 4: コンパイル → テスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "OperationDeniedNotificationTest"
```
Expected: 2件PASS。既存の `CompleteResearch` / `OneClickCraft` 系テストも回してリグレッション無しを確認:
```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Research|Craft"
```

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CompleteResearchProtocol.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/OneClickCraft.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/OperationDeniedNotificationTest.cs
git commit -m "feat(notification): 研究完了失敗・クラフト失敗の拒否通知を配線"
```

---

### Task 4: 失敗系配線 (2/3) — removeTrainCar / placeBlock

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveTrainCarProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/OperationDeniedNotificationTest.cs`（テスト追加）

**Interfaces:**
- Consumes: Task 1の `NotificationService` / `CreateOperationDenied`
- Produces: MessageId `"denied.removeTrainCarInventoryFull"`、`"denied.placeBlockNotUnlocked"`、`"denied.placeBlockCostShortage"`、`"denied.placeBlockWireShortage"`（すべてparams空）

- [ ] **Step 1: RemoveTrainCarProtocol に通知を追加**

コンストラクタに `_notificationService = serviceProvider.GetService<NotificationService>();`（フィールド・using追加）。インベントリ満杯分岐のみ通知を追加（列車不在・車両不在はクライアント側の不整合であり通知対象外）:

```csharp
            if (!playerMainInventory.InsertionCheck(refundItems))
            {
                Debug.LogWarning($"Remove train car aborted. Player inventory is full. \ncarId: {trainCarInstanceId}");
                // 高価な車両が無言で消えない事故を防ぐため満杯を通知する
                // Notify inventory-full so an expensive car never silently refuses to be removed
                _notificationService.Notify(request.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.removeTrainCarInventoryFull", Array.Empty<string>()));
                return null;
            }
```

- [ ] **Step 2: PlaceBlockProtocol にセル単位スキップの集約通知を追加**

コンストラクタに `NotificationService` を追加取得。`GetResponse` のループを理由カウント付きに変更し、ループ後に理由ごとに1回だけ通知（セル数分のスパム防止。サーバー側クールダウンとは独立にリクエスト内で集約する）:

```csharp
            // セル単位のスキップ理由を集約しリクエスト末尾で1回ずつ通知する
            // Aggregate per-cell skip reasons and notify once per reason at the end of the request
            var notUnlockedCount = 0;
            var costShortageCount = 0;
            var wireShortageCount = 0;

            foreach (var placeInfo in data.PlacePositions)
            {
                PlaceBlock(placeInfo);
            }

            if (notUnlockedCount > 0) _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockNotUnlocked", Array.Empty<string>()));
            if (costShortageCount > 0) _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockCostShortage", Array.Empty<string>()));
            if (wireShortageCount > 0) _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockWireShortage", Array.Empty<string>()));

            return null;
```

`PlaceBlock` ローカル関数内の該当スキップにカウントを追加:

```csharp
                if (!IsUnlocked(placeBlockId, blockMaster.BlockGuid)) { notUnlockedCount++; return; }
                ...
                if (!ConstructionCostService.HasRequiredItems(costItemCounts, inventory.InventoryItems)) { costShortageCount++; return; }
                ...
                    if (!plan.IsPlaceable) { wireShortageCount++; return; }
```

既存ブロック重複（`Exists`）と `TryAddBlock` 失敗は通知しない（ドラッグ設置で恒常的に起きる正常系スキップのため。v1判断、specに準拠）。

- [ ] **Step 3: テスト追加**

`OperationDeniedNotificationTest.cs` に追加。placeBlockは未解放ブロックの設置で検証（`DebugParameters` のFreeBlockPlacementがcacheに残っていると素通りするので注意 — テスト前にcache確認）:

```csharp
        [Test]
        public void PlaceBlockNotUnlockedFiresDeniedNotification()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // 未解放ブロックを設置要求 → スキップされ未解放通知が1件飛ぶ
            // Request placing a locked block → the cell is skipped and one not-unlocked notification fires
            // 未解放のBlockIdは既存のPlaceBlock系テスト/GameUnlockStateのテストが使う「初期ロック」ブロックを流用する
            ...（既存PlaceBlockProtocol系テストのリクエスト構築コードを流用し、ロック状態のBlockIdで送信）

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.placeBlockNotUnlocked"));
        }
```
リクエスト構築（`SendPlaceBlockProtocolMessagePack` / `PlaceInfoMessagePack`）は既存の `PlaceBlockProtocol` 系テスト（`grep -rln "SendPlaceBlockProtocolMessagePack" moorestech_server/Assets/Scripts/Tests/` で特定）から流用する。removeTrainCarの満杯シナリオはセットアップが重い（列車生成＋満杯化）ため、既存のRemoveTrainCar系テストに満杯ケースがあればそこへアサート追加、無ければplaceBlockテストのみでこのTaskの検証とする（RemoveTrainCar側は同一コードパス`InsertionCheck`→Notifyであり、NotificationService自体はTask 1でテスト済み。未検証である旨をコミットメッセージに記す）。

- [ ] **Step 4: コンパイル → テスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "OperationDeniedNotificationTest|PlaceBlock|RemoveTrainCar"
```
Expected: 新テストPASS・既存リグレッション無し

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveTrainCarProtocol.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/OperationDeniedNotificationTest.cs
git commit -m "feat(notification): ブロック設置スキップ・車両撤去失敗の拒否通知を配線"
```

---

### Task 5: 失敗系配線 (3/3) — railConnectionEdit

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RailConnectionEditProtocol.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/OperationDeniedNotificationTest.cs`（テスト追加）

**Interfaces:**
- Consumes: Task 1の `NotificationService`。既存 `ResponseRailConnectionEditMessagePack`（`Success`/`FailureReason` 相当のプロパティ、`RailConnectionEditFailureReason` enum）
- Produces: MessageId `"denied.railEdit.<FailureReason名>"`（例: `"denied.railEdit.NotEnoughRailItem"`。enumの `ToString()` で構成、params空）

- [ ] **Step 1: 応答の失敗を1箇所で捕捉して通知**

失敗returnは8箇所以上に散っているため、各分岐でなく `GetResponse` の応答確定点で1回だけ捕捉する（応答オブジェクトに理由が既に載っているため。分岐ごとの1行追加より変更が小さく漏れも出ない）:

```csharp
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<RailConnectionEditRequest>(payload);
            var response = ExecuteEdit(data);

            // 失敗応答はSendOnlyで破棄されるため、通知基盤経由でプレイヤーに理由を届ける
            // Failure responses are discarded by SendOnly, so deliver the reason via the notification service
            if (response is ResponseRailConnectionEditMessagePack railResponse && !railResponse.IsSuccess)
            {
                _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied($"denied.railEdit.{railResponse.FailureReason}", Array.Empty<string>()));
            }

            return response;
        }
```

実際の `GetResponse` の現在形（`ExecuteEdit` を直接returnしているか等）と `ResponseRailConnectionEditMessagePack` の成功/理由プロパティの正確な名前（`IsSuccess`/`Success`、`FailureReason`）をファイルを読んで確認し、上記をその形に合わせて挿入する。コンストラクタに `NotificationService` 取得とusingを追加。`FailureReason == None` で `IsSuccess == false` になるケースが無いことも確認（あればNoneを通知対象から除外）。

- [ ] **Step 2: テスト追加**

不正ノードID（存在しないノード）でConnect要求 → `denied.railEdit.InvalidNode` を検証:

```csharp
        [Test]
        public void RailConnectInvalidNodeFiresDeniedNotification()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // 存在しないノード同士の接続要求 → InvalidNodeの拒否通知
            // Request connecting nonexistent nodes → InvalidNode denied notification
            // RailConnectionEditRequestはstatic factory（CreateConnectRequest等）から構築する。既存のRail系PacketTestの構築コードを流用
            ...（存在しないNodeId/Guidを渡してConnectモードのリクエストを構築・送信）

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.railEdit.InvalidNode"));
        }
```
リクエスト構築は `grep -rln "RailConnectionEditRequest" moorestech_server/Assets/Scripts/Tests/` で既存テストを特定して流用する。

- [ ] **Step 3: コンパイル → テスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "OperationDeniedNotificationTest|RailConnection"
```
Expected: 新テストPASS・既存Rail系リグレッション無し

- [ ] **Step 4: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RailConnectionEditProtocol.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/OperationDeniedNotificationTest.cs
git commit -m "feat(notification): レール接続/切断失敗の拒否通知を配線（8種の失敗理由を送出）"
```

---

### Task 6: クライアント中継 — NotificationTopic

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/NotificationTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`（`Bind()` 内のTopic登録並び）

**Interfaces:**
- Consumes: `NotificationService.EventTag`・`NotificationMessagePack`（Server.Event参照は `Client.WebUiHost.asmdef` に既にあり）、`ClientContext.VanillaApi.Event.SubscribeEventResponse`、`WebSocketHub.Publish` / `RegisterTopic`、`WebUiJson.Serialize`
- Produces: topic `"notification.events"`、JSON `{ seq: number, category: "achievement"|"operationDenied", messageId: string, messageParams: string[], itemId: number|null }`（Task 7のWebが消費）

- [ ] **Step 1: NotificationTopic.cs を作成**

```csharp
using System;
using Client.Game.InGame.Context;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.Notification;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// サーバー通知イベントをWebへ中継するtransientトピック（スナップショット再生なし）
    /// Transient topic relaying server notifications to the web (no snapshot replay)
    /// </summary>
    public class NotificationTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "notification.events";

        private readonly WebSocketHub _hub;
        private readonly IDisposable _subscription;
        private long _seq;

        public NotificationTopic(WebSocketHub hub)
        {
            _hub = hub;
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(NotificationService.EventTag, OnNotification);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            // 通知は揮発。接続時に過去分を再生しない
            // Notifications are transient; do not replay history on connect
            return UniTask.FromResult("{}");
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }

        private void OnNotification(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<NotificationMessagePack>(payload);
            _seq++;
            var dto = new NotificationDto
            {
                Seq = _seq,
                Category = message.Category == NotificationCategory.Achievement ? "achievement" : "operationDenied",
                MessageId = message.MessageId,
                MessageParams = message.MessageParams,
                ItemId = message.ItemId == ItemMaster.EmptyItemId ? null : (int?)(int)message.ItemId,
            };
            _hub.Publish(TopicName, WebUiJson.Serialize(dto));
        }
    }

    public class NotificationDto
    {
        public long Seq;
        public string Category;
        public string MessageId;
        public string[] MessageParams;
        public int? ItemId;
    }
}
```
`WebUiJson.Serialize` のプロパティ命名（camelCase変換の有無）を `ProgressDto`（PascalCaseフィールド）とWeb側 `ProgressData` の対応で確認し、Web側スキーマ（Task 7）とキー名を一致させる。`ClientContext` のnamespaceは `TrainRidingTopic.cs` のusingに合わせる。

- [ ] **Step 2: WebUiGameBinder に登録**

`Bind()` 内のTopic登録並び（`hub.RegisterTopic(CrosshairTopic.TopicName, ...)` 等の近く）に追加:

```csharp
            hub.RegisterTopic(NotificationTopic.TopicName, new NotificationTopic(hub));
```

- [ ] **Step 3: コンパイル → WebUi系テスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.WebUi"
```
Expected: コンパイルエラー0。`WebUiGateClassification` / `WireContract` 系テストが新topicの分類・契約登録漏れで落ちた場合は、失敗メッセージの指示に従い分類リスト（例: `Client.Tests/WebUi/Gate/WebUiGateClassification.cs`）へ `"notification.events"` を追加して再実行

- [ ] **Step 4: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.WebUiHost/ moorestech_client/Assets/Scripts/Client.Tests/
git commit -m "feat(notification): サーバー通知をWebへ中継するNotificationTopicを追加"
```

---

### Task 7: Web表示 — features/notification

**Files:**
- Modify: `moorestech_web/webui/src/bridge/transport/protocol.ts`（Topics / TopicPayloads）
- Modify: `moorestech_web/webui/src/bridge/contract/` 配下のスキーマ定義（`LocalizationData` 等が定義されている場所に `NotificationData` を追加。`grep -rn "LocalizationData" moorestech_web/webui/src/bridge/contract/` で特定）
- Create: `moorestech_web/webui/src/features/notification/notificationMessages.ts`
- Create: `moorestech_web/webui/src/features/notification/notificationStore.ts`
- Create: `moorestech_web/webui/src/features/notification/NotificationHost.tsx`
- Create: `moorestech_web/webui/src/features/notification/index.ts`
- Modify: `moorestech_web/webui/src/app/App.tsx`（Portal内にマウント）
- Test: `moorestech_web/webui/src/features/notification/notificationStore.test.ts`、`moorestech_web/webui/src/features/notification/notificationMessages.test.ts`

**Interfaces:**
- Consumes: Task 6のtopic `"notification.events"` とDTO、`useTopic`、`useI18n().t`、`shared/ui/ItemIcon`
- Produces: `<NotificationHost />`（App.tsxがマウント）、`data-testid="notification-host"`

- [ ] **Step 0: webui-design スキルを読む**（Web側の見た目・構造ホワイトリスト遵守のため必須）

- [ ] **Step 1: スキーマとトピック登録**

zodスキーマ（既存スキーマ定義ファイルの流儀に合わせ、`LocalizationData` と同じファイル群に追加）:

```typescript
export const NotificationDataSchema = z.object({
  seq: z.number(),
  category: z.enum(["achievement", "operationDenied"]),
  messageId: z.string(),
  messageParams: z.array(z.string()),
  itemId: z.number().nullable(),
});
```
スナップショットが `{}` で届くため、topicStoreの検証を通すには `.partial()` 相当の緩和が必要になり得る。既存のtransient topic `playtestDomQuery` のスキーマ定義がどう処理しているかを確認し、同じ形にする（例: 全フィールドoptionalにしてHost側で `seq` 有無をガード）。

`protocol.ts`:
```typescript
  // 通知はsnapshotを持たない一時イベントとして扱う
  // Notifications are transient events without snapshots
  notification: "notification.events",
```
```typescript
  [Topics.notification]: NotificationData;
```
キー名（`messageParams` 等）はTask 6のStep 1で確認した実際のシリアライズ結果に一致させる。

- [ ] **Step 2: notificationMessages.ts を作成**

```typescript
// messageId→表示テンプレートの対応表。文言はWeb側が所有しサーバーは構造化IDのみ送る
// Maps messageId to display templates; the web owns wording, the server sends structured ids only
// キーはi18n辞書キーとしても機能する（key=原文運用。{p0}等はparamsで補間）
const templates: Record<string, string> = {
  "achievement.researchCompleted": "Research completed: {p0}",
  "achievement.challengeCompleted": "Challenge completed: {p0}",
  "achievement.unlockedItem": "New item unlocked",
  "achievement.unlockedCraftRecipe": "New crafting recipe unlocked",
  "achievement.unlockedMachineRecipe": "New machine recipe unlocked",
  "achievement.unlockedBlock": "New block unlocked",
  "achievement.unlockedTrainCar": "New train car unlocked",
  "achievement.unlockedConnectTool": "New connect tool unlocked",
  "denied.researchNotCompletable": "Cannot complete research (prerequisites or materials missing)",
  "denied.craftResultFull": "Cannot craft: inventory is full",
  "denied.craftMaterialShortage": "Cannot craft: not enough materials",
  "denied.removeTrainCarInventoryFull": "Cannot remove train car: inventory is full",
  "denied.placeBlockNotUnlocked": "Some blocks were not placed: not unlocked yet",
  "denied.placeBlockCostShortage": "Some blocks were not placed: not enough materials",
  "denied.placeBlockWireShortage": "Some blocks were not placed: not enough wires",
  "denied.railEdit.InvalidNode": "Rail edit failed: invalid rail node",
  "denied.railEdit.NodeInUseByTrain": "Rail edit failed: a train is using this rail",
  "denied.railEdit.StationInternalEdge": "Rail edit failed: cannot edit station internal rail",
  "denied.railEdit.InvalidMode": "Rail edit failed",
  "denied.railEdit.NotEnoughRailItem": "Rail edit failed: not enough rail materials",
  "denied.railEdit.NotEnoughInventorySpace": "Rail edit failed: inventory is full",
  "denied.railEdit.RailLengthExceeded": "Rail edit failed: rail is too long",
  "denied.railEdit.NotUnlocked": "Rail edit failed: connect tool not unlocked",
  "denied.railEdit.UnknownError": "Rail edit failed",
};

// 未知のmessageIdはID文字列をそのまま表示して欠落を可視化する
// Unknown messageIds fall back to the raw id string to surface gaps
export function resolveNotificationTemplate(messageId: string): string {
  return templates[messageId] ?? messageId;
}

export function buildInterpolationValues(messageParams: string[]): Record<string, string> {
  return Object.fromEntries(messageParams.map((value, index) => [`p${index}`, value]));
}
```

- [ ] **Step 3: notificationStore.ts を作成**（`toastStore.ts` と同型・表示5秒）

```typescript
import { create } from "zustand";

export type GameNotification = {
  id: number;
  category: "achievement" | "operationDenied";
  messageId: string;
  messageParams: string[];
  itemId: number | null;
};

type NotificationState = {
  notifications: GameNotification[];
  addNotification: (n: Omit<GameNotification, "id">) => void;
  removeNotification: (id: number) => void;
};

let nextId = 1;
const DISPLAY_MS = 5000;

export const useNotificationStore = create<NotificationState>((set) => ({
  notifications: [],
  addNotification: (n) => {
    const id = nextId++;
    set((s) => ({ notifications: [...s.notifications, { ...n, id }] }));
    setTimeout(() => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })), DISPLAY_MS);
  },
  removeNotification: (id) => set((s) => ({ notifications: s.notifications.filter((x) => x.id !== id) })),
}));
```

- [ ] **Step 4: NotificationHost.tsx を作成**

```tsx
import { useEffect, useRef } from "react";
import { Notification, Stack } from "@mantine/core";
import { useTopic } from "@/bridge";
import { Topics } from "@/bridge/transport/protocol";
import { useI18n } from "@/shared/i18n";
import ItemIcon from "@/shared/ui/ItemIcon";
import { useNotificationStore } from "./notificationStore";
import { resolveNotificationTemplate, buildInterpolationValues } from "./notificationMessages";
import styles from "./style.module.css";

export default function NotificationHost() {
  const payload = useTopic(Topics.notification);
  const { t } = useI18n();
  const notifications = useNotificationStore((s) => s.notifications);
  const lastSeq = useRef(0);

  useEffect(() => {
    // snapshotの空オブジェクトや重複配信はseqで弾く
    // Guard against the empty snapshot object and duplicate deliveries via seq
    if (!payload?.seq || payload.seq <= lastSeq.current) return;
    lastSeq.current = payload.seq;
    useNotificationStore.getState().addNotification({
      category: payload.category,
      messageId: payload.messageId,
      messageParams: payload.messageParams,
      itemId: payload.itemId,
    });
  }, [payload]);

  return (
    <Stack gap="xs" className={styles.host} data-testid="notification-host">
      {notifications.map((n) => (
        <Notification
          key={n.id}
          color={n.category === "operationDenied" ? "yellow" : "teal"}
          icon={n.itemId != null ? <ItemIcon itemId={n.itemId} /> : undefined}
          withCloseButton={false}
          withBorder
        >
          {t(resolveNotificationTemplate(n.messageId), buildInterpolationValues(n.messageParams))}
        </Notification>
      ))}
    </Stack>
  );
}
```
`style.module.css` は `features/toast/style.module.css` を参考に、toastと重ならない位置（toastが右下なら本通知は右上）で作成。import経路（`@/bridge` からの `useTopic` export有無）は既存featureのimportに合わせる。`index.ts` で `NotificationHost` をexport。

- [ ] **Step 5: App.tsx にマウント**

```tsx
import { NotificationHost } from "@/features/notification";
```
`<Portal>` 内の `<ToastHost />` の隣に:
```tsx
        <ToastHost />
        <NotificationHost />
```

- [ ] **Step 6: テストを作成**

`notificationStore.test.ts`（`toastStore.test.ts` と同型）:

```typescript
import { describe, it, expect, vi, beforeEach } from "vitest";
import { useNotificationStore } from "./notificationStore";

describe("notificationStore", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    useNotificationStore.setState({ notifications: [] });
  });

  it("追加され5秒後に消える", () => {
    useNotificationStore.getState().addNotification({ category: "achievement", messageId: "achievement.researchCompleted", messageParams: ["Iron"], itemId: null });
    expect(useNotificationStore.getState().notifications).toHaveLength(1);
    vi.advanceTimersByTime(5000);
    expect(useNotificationStore.getState().notifications).toHaveLength(0);
  });
});
```

`notificationMessages.test.ts`:

```typescript
import { describe, it, expect } from "vitest";
import { resolveNotificationTemplate, buildInterpolationValues } from "./notificationMessages";

describe("notificationMessages", () => {
  it("既知のmessageIdはテンプレートを返す", () => {
    expect(resolveNotificationTemplate("denied.craftMaterialShortage")).toContain("materials");
  });
  it("未知のmessageIdはID文字列をそのまま返す", () => {
    expect(resolveNotificationTemplate("unknown.id")).toBe("unknown.id");
  });
  it("paramsをp0,p1に変換する", () => {
    expect(buildInterpolationValues(["a", "b"])).toEqual({ p0: "a", p1: "b" });
  });
});
```

- [ ] **Step 7: テスト・lint実行**

```bash
cd moorestech_web/webui && npm run test && npm run lint
```
Expected: 全PASS・lintエラー0

- [ ] **Step 8: コミット**

```bash
git add moorestech_web/webui/src/
git commit -m "feat(notification): Web側ゲーム内通知表示（features/notification）を追加"
```

---

### Task 8: 統合確認と仕上げ

**Files:**
- 変更なし（検証のみ。必要に応じて前タスクの修正）

- [ ] **Step 1: 全体コンパイルとサーバー/クライアントテスト一括**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Notification|OperationDenied"
cd moorestech_web/webui && npm run test
```
Expected: 全PASS

- [ ] **Step 2: 実機スモーク（PlayMode）**

unity-playmode-recorded-playtest スキルのDSLで起動し、以下を目視/録画確認:
1. 素材ゼロで研究完了を試みる → 右上に "Cannot complete research..." が出る
2. 研究を1つ完了する → "Research completed: ..." が出る
3. 同一失敗を連打 → 3秒間は通知が増えない（クールダウン）

WebUIモードのビルドメニューはuGUI自動操作不能のため、設置系の確認は `SendOnly.PlaceBlock` 直送で行う（メモリ: webui-playtest-ugui-buildmenu-broken）。スモークが環境都合で実行不能な場合はその旨を最終報告に明記する（「動くはず」とは書かない）。

- [ ] **Step 3: 最終コミット・レビュー**

```bash
git status  # 未コミットの取りこぼし確認（.metaを含む）
git add -A && git commit -m "chore(notification): 通知基盤の統合確認"
```
moores-code-review スキルで1パスかけてからPR作成（pr-createスキル）。

---

## スコープ外（v2以降・specに準拠）

- 残り失敗系配線: 機械レシピ選択・電線接続・歯車チェーン接続・電柱/歯車ポール/橋脚延長・列車車両設置/連結・ベースキャンプ昇格
- MapObject採取の満杯時アイテム消失（挙動修正が必要な可能性・別課題）
- 通知センター（履歴・未読）・クライアント発通知
- 日本語ローカライズ辞書への通知キー追加（辞書は `Client.Localization` の `Localize` が配信元。key=原文フォールバックによりv1は英語表示で動作する。ja辞書追加はフォローアップ）
- `BlockGameObjectChild.cs:75` のTODO（removeBlock理由表示の通知基盤への移行）— 既に唯一動いている失敗表示のため、安定後に移行

## 既知の限界（実装者への注意）

- クールダウンのキーはparamsを含まない。同時多発アンロック（研究1件→レシピ5件解放等）は同種通知が1件に畳まれる（仕様。スパム防止優先）
- クールダウン期間経過後の再送は `SetCooldownDuration(TimeSpan.Zero)` 経由でテストする（時刻抽象が無いため実時間の経過はテストしない）
- Task 3〜5のテスト内でリクエスト構築・`GetPacketResponse` 呼び出しの正確なシグネチャは既存PacketTestから流用すること（本計画のテストコードは骨子であり、API名は実ファイルに合わせて修正してよい。アサート内容は変更しない）
