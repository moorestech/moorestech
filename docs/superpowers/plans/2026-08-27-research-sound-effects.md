# 序盤原始研究 SE（通知イベント＋Web sound.play） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 研究画面の操作（Web UI）と研究成功・アンロック・チャレンジ完了（サーバー通知）に効果音を付け、共通UIボタンにもクリック音を一括適用する。

**Architecture:** 音声資産と再生は Unity 側 `SoundEffectManager` に一本化する。結果音は `NotificationService.EventTag` を独立購読する `NotificationSoundEffectPlayer`（Client.Game）が messageId→SE 種別の対応表で鳴らす。Web 操作音は `sound.play {type}` アクションを新設し、Web は用途名だけを送る。研究画面の開閉は `ResearchTreeState` が直接鳴らす。

**Tech Stack:** Unity C#（VContainer, MessagePack, NUnit）/ React + TypeScript（vitest, react-test-renderer）/ uloop CLI

## Requirements

設計ADR: `docs/adr/0037-research-sound-effects-via-notification-and-web-sound-play.md`

- R1 `SoundEffectType` に `UiSelect` `UiClose` `UiConfirm` `UiDenied` `UiOpen` `ResearchComplete` `Unlock` `ChallengeComplete` を追加し、各クリップを `[SerializeField]` で割当できる。受入: 8種すべて `PlaySoundEffect` で例外なく再生される
- R2 同一 `SoundEffectType` は直前再生から 0.1 秒はスキップされる（クールダウン）。受入: 単体テストで 0.05 秒後の同種別は拒否・0.2 秒後は許可・別種別は即許可（float 誤差を避けるため境界ちょうどは検証しない）
- R3 通知 messageId `achievement.researchCompleted`→`ResearchComplete`、`achievement.unlocked*`（Item/CraftRecipe/MachineRecipe/Block/TrainCar/ConnectTool/Blueprint）→`Unlock`、`achievement.challengeCompleted`→`ChallengeComplete` で再生する。それ以外（`itemEarned.*`, `denied.*`）は鳴らさない。受入: 対応表の単体テスト
- R4 結果音の再生は Web UI の有無に依らず動く（Web リレー `NotificationTopic` の中では鳴らさない）
- R5 研究画面の開閉: `ResearchTreeState.OnEnter`→`UiOpen`、`OnExit`→`UiClose`
- R6 Web→C# アクション `sound.play { type: "uiSelect" | "uiClose" | "uiConfirm" | "uiDenied" }` を新設。C# は用途名を `SoundEffectType` へ解決して再生。未知の type は `invalid_type` で失敗。受入: `action_names.json` パリティテスト（C#/TS 双方）が通る
- R7 Web の送信は fire-and-forget で、失敗・切断でトーストを出さない
- R8 Web 送信元: 研究ノードカードクリック→`uiSelect`、詳細ペインの×→`uiClose`、研究ボタン（有効）→`uiConfirm`、研究ボタンが disabled かつ未完了のときのポインタ押下→`uiDenied`
- R9 共通部品 `IconButton` / `PanelActionButton` / `ModeSwitch` の onClick で `uiSelect` を送る（全画面へ一括適用）
- R10 クリップは CC0 または CC-BY のフリー素材を `moorestech_client/Assets/Asset/Common/SoundEffect/` に置き、同ディレクトリ `CREDITS.md` に出典URL・作者・ライセンスを記録する
- R11 `GameSystem.prefab` の `SoundEffectManager` への AudioClip 配線は `uloop execute-dynamic-code` で行う（YAML 手編集禁止）
- やらないこと: Webview 内 `<audio>` 再生／クリップ名直指定／`ResearchCompleteActionHandler` での再生／`ItemSlot` 等スロット部品への音付け／音量設定UI

## Global Constraints

- AGENTS.md 全規約（1ファイル200行以下、partial禁止、`Func<>`禁止、UniRx、日英2行コメント、`#region Internal` はメソッド内ローカル関数限定、デフォルト引数禁止、try-catch は外部境界のみ）
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client`
- `.meta` は手で作らない。Prefab/シーンは `uloop execute-dynamic-code` 経由のみ
- Web: `cd moorestech_web/webui && npx vitest run <file>` / `npm run lint`
- C# テスト: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`
- 作業は `moores-wt new feature/research-sound-effects` で切った worktree で行う
- bd: 「序盤原始研究にSEを入れる（C#イベント＋Web操作音→sound.play）」(P2)。着手時 `bd update <id> --claim`

## File Structure

| 種別 | パス | 責務 |
|---|---|---|
| Modify | `moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/SoundEffectManager.cs` | enum 8種追加・クリップ辞書・クールダウン委譲 |
| Create | `moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/SoundEffectCooldown.cs` | 種別ごとの直前再生時刻を持ち再生可否を返す純クラス |
| Create | `moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/NotificationSoundEffectTable.cs` | messageId→SoundEffectType の唯一の対応表（純 static） |
| Create | `moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/NotificationSoundEffectPlayer.cs` | 通知イベント購読→対応表→再生 |
| Create | `moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/UiSoundTypeTable.cs` | Web 用途名文字列→SoundEffectType の唯一の対応表 |
| Modify | `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs` | Player の DI 登録 |
| Modify | `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameContainerActivation.cs` | Player の eager resolve |
| Modify | `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/ResearchTreeState.cs` | 開閉音 |
| Create | `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/SoundEffectActions.cs` | `sound.play` ハンドラ |
| Modify | `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:185` | ハンドラ登録 |
| Modify | `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json` | `sound.play` 追加 |
| Modify | `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/error_codes.json` | `invalid_type` 追加 |
| Create | `moorestech_client/Assets/Scripts/Client.Tests/SoundEffect/SoundEffectCooldownTest.cs` | R2 |
| Create | `moorestech_client/Assets/Scripts/Client.Tests/SoundEffect/NotificationSoundEffectTableTest.cs` | R3 |
| Create | `moorestech_client/Assets/Scripts/Client.Tests/SoundEffect/UiSoundTypeTableTest.cs` | R6 |
| Modify | `moorestech_web/webui/src/bridge/transport/actionContract.ts` | `sound.play` 型と ACTION_TYPES |
| Create | `moorestech_web/webui/src/bridge/transport/uiSound.ts` | `playUiSound(type)` fire-and-forget |
| Create | `moorestech_web/webui/src/bridge/transport/uiSound.test.ts` | R7 |
| Modify | `moorestech_web/webui/src/bridge/index.ts` | `playUiSound` 公開 |
| Modify | `moorestech_web/webui/src/shared/ui/IconButton/index.tsx` `PanelActionButton/index.tsx` `ModeSwitch/index.tsx`（＋各 test） | R9 |
| Modify | `moorestech_web/webui/src/features/research/ResearchNodeCard.tsx` `ResearchDetailPane.tsx`（＋ `ResearchDetailPane.test.ts`） | R8 |
| Create | `moorestech_client/Assets/Asset/Common/SoundEffect/ui-*.ogg`, `CREDITS.md` | R10 |

## 配置と前例

- 結果音の購読者を `NotificationTopic`（Client.WebUiHost, Web リレー）の中に置かず Client.Game に独立購読で置く: 前例 `ChallengeManager.Construct` の `ClientContext.VanillaApi.Event.SubscribeEventResponse(...)`。理由: R4（Web 非依存）＋リレーに副作用を混ぜない。出所: agent前提（ADR 0037 決定3「通知イベント経由」の配置細目）
- `sound.play` ハンドラ: 前例 `ResearchCompleteActionHandler`（`IActionHandler` + `WebUiGameBinder.RegisterAction`）
- 文字列→enum の対応表を専用 static クラスにする: 前例 `NotificationCategoryTable`
- Web 側の一括適用は共通部品の onClick に仕込む（各呼び出し側に散らさない）: 前例 `IconButton` が既に `onClick` を集約している

---

### Task 1: SoundEffectCooldown（純クラス）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/SoundEffectCooldown.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/SoundEffect/SoundEffectCooldownTest.cs`

**Interfaces:**
- Produces: `public sealed class SoundEffectCooldown { public SoundEffectCooldown(float cooldownSeconds); public bool TryAccept(SoundEffectType type, float nowSeconds); }` — `true` なら再生してよい（内部で直前時刻を更新）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Client.Game.InGame.SoundEffect;
using NUnit.Framework;

namespace Client.Tests.SoundEffect
{
    public class SoundEffectCooldownTest
    {
        [Test]
        public void SameTypeWithinCooldownIsRejected()
        {
            var cooldown = new SoundEffectCooldown(0.1f);
            Assert.IsTrue(cooldown.TryAccept(SoundEffectType.Unlock, 10.0f));
            Assert.IsFalse(cooldown.TryAccept(SoundEffectType.Unlock, 10.05f));
        }

        [Test]
        public void SameTypeAfterCooldownIsAccepted()
        {
            var cooldown = new SoundEffectCooldown(0.1f);
            Assert.IsTrue(cooldown.TryAccept(SoundEffectType.Unlock, 10.0f));
            Assert.IsTrue(cooldown.TryAccept(SoundEffectType.Unlock, 10.2f));
        }

        [Test]
        public void DifferentTypeIsAcceptedImmediately()
        {
            var cooldown = new SoundEffectCooldown(0.1f);
            Assert.IsTrue(cooldown.TryAccept(SoundEffectType.Unlock, 10.0f));
            Assert.IsTrue(cooldown.TryAccept(SoundEffectType.ResearchComplete, 10.0f));
        }

        [Test]
        public void RejectedCallDoesNotExtendCooldown()
        {
            var cooldown = new SoundEffectCooldown(0.1f);
            cooldown.TryAccept(SoundEffectType.Unlock, 10.0f);
            cooldown.TryAccept(SoundEffectType.Unlock, 10.05f);
            Assert.IsTrue(cooldown.TryAccept(SoundEffectType.Unlock, 10.2f));
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `SoundEffectCooldown` 未定義、`SoundEffectType.Unlock` / `ResearchComplete` 未定義のエラー

- [ ] **Step 3: 実装（enum 追加と Cooldown）**

`SoundEffectManager.cs` の enum を置き換える:

```csharp
    public enum SoundEffectType
    {
        DestroyBlock,
        DestroyStone,
        DestroyTree,
        DestroyBush,
        PlaceBlock,
        UiSelect,
        UiClose,
        UiConfirm,
        UiDenied,
        UiOpen,
        ResearchComplete,
        Unlock,
        ChallengeComplete,
    }
```

`SoundEffectCooldown.cs`:

```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.SoundEffect
{
    /// <summary>
    /// 同一SE種別の連続再生を一定秒抑える。通知バースト（1研究で解放通知が複数件）で音が重ならないようにする
    /// Suppresses repeated playback of the same SE type for a short window so notification bursts do not stack
    /// </summary>
    public sealed class SoundEffectCooldown
    {
        private readonly float _cooldownSeconds;
        private readonly Dictionary<SoundEffectType, float> _lastAcceptedSeconds = new();

        public SoundEffectCooldown(float cooldownSeconds)
        {
            _cooldownSeconds = cooldownSeconds;
        }

        public bool TryAccept(SoundEffectType type, float nowSeconds)
        {
            // 直前受理からクールダウン未満なら拒否。拒否は時刻を更新しない
            // Reject within the window since the last accepted play; a rejection does not move the timestamp
            if (_lastAcceptedSeconds.TryGetValue(type, out var last) && nowSeconds - last < _cooldownSeconds) return false;
            _lastAcceptedSeconds[type] = nowSeconds;
            return true;
        }
    }
}
```

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "SoundEffectCooldownTest"`
Expected: 4 passed

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/ moorestech_client/Assets/Scripts/Client.Tests/SoundEffect/
git commit -m "feat(se): SoundEffectType に UI/結果音8種を追加しクールダウンを導入"
```

---

### Task 2: SoundEffectManager のクリップ割当とクールダウン適用

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/SoundEffectManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Mining/AttackTrackingMiningTarget.cs:56-57`

**Interfaces:**
- Consumes: `SoundEffectCooldown.TryAccept`
- Produces: `SoundEffectManager.PlaySoundEffect(SoundEffectType)`（既存シグネチャ不変。内部でクールダウン適用）

- [ ] **Step 1: SoundEffectManager を書き換える**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.SoundEffect
{
    /// <summary>
    ///     TODO 仮のSE専用マネージャ 将来的な作り変えを意識しつつ、とりあえずこれで実装する
    /// </summary>
    public class SoundEffectManager : MonoBehaviour
    {
        private const float CooldownSeconds = 0.1f;

        [SerializeField] private AudioClip destroyBlockSound;
        [SerializeField] private AudioClip destroyStoneSound;
        [SerializeField] private AudioClip destroyTreeSound;
        [SerializeField] private AudioClip destroyBushSound;
        [SerializeField] private AudioClip placeBlockSound;
        [SerializeField] private AudioClip uiSelectSound;
        [SerializeField] private AudioClip uiCloseSound;
        [SerializeField] private AudioClip uiConfirmSound;
        [SerializeField] private AudioClip uiDeniedSound;
        [SerializeField] private AudioClip uiOpenSound;
        [SerializeField] private AudioClip researchCompleteSound;
        [SerializeField] private AudioClip unlockSound;
        [SerializeField] private AudioClip challengeCompleteSound;

        [SerializeField] private AudioSource audioSource;

        private readonly Dictionary<SoundEffectType, AudioClip> _soundEffectTypeToAudioClip = new();
        private readonly SoundEffectCooldown _cooldown = new(CooldownSeconds);

        public static SoundEffectManager Instance { get; private set; }

        private void Awake()
        {
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyBlock, destroyBlockSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyStone, destroyStoneSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyTree, destroyTreeSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyBush, destroyBushSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.PlaceBlock, placeBlockSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.UiSelect, uiSelectSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.UiClose, uiCloseSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.UiConfirm, uiConfirmSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.UiDenied, uiDeniedSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.UiOpen, uiOpenSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.ResearchComplete, researchCompleteSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.Unlock, unlockSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.ChallengeComplete, challengeCompleteSound);

            Instance = this;
        }

        public void PlaySoundEffect(SoundEffectType soundEffectType)
        {
            // クライアント演出の実時間判定。サーバーロジックではないので Time.unscaledTime で可
            // Client-side presentation timing; not server logic, so Time.unscaledTime is acceptable
            if (!_cooldown.TryAccept(soundEffectType, Time.unscaledTime)) return;
            audioSource.PlayOneShot(_soundEffectTypeToAudioClip[soundEffectType]);
        }
    }

    public enum SoundEffectType
    {
        DestroyBlock,
        DestroyStone,
        DestroyTree,
        DestroyBush,
        PlaceBlock,
        UiSelect,
        UiClose,
        UiConfirm,
        UiDenied,
        UiOpen,
        ResearchComplete,
        Unlock,
        ChallengeComplete,
    }
}
```

- [ ] **Step 2: 既存テストフィクスチャの割当を全フィールドへ広げる**

`AttackTrackingMiningTarget.cs` の

```csharp
            foreach (var fieldName in new[] { "destroyBlockSound", "destroyStoneSound", "destroyTreeSound", "destroyBushSound", "placeBlockSound" })
```

を

```csharp
            foreach (var fieldName in new[]
                     {
                         "destroyBlockSound", "destroyStoneSound", "destroyTreeSound", "destroyBushSound", "placeBlockSound",
                         "uiSelectSound", "uiCloseSound", "uiConfirmSound", "uiDeniedSound", "uiOpenSound",
                         "researchCompleteSound", "unlockSound", "challengeCompleteSound",
                     })
```

に変える。

- [ ] **Step 3: コンパイルと既存採掘テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MiningFocusStateTest|MiningTargetFocusContextTest|OutcropMiningTargetTest"`
Expected: すべて pass（クールダウンで採掘連打テストが落ちる場合は、そのテストが同一フレーム内に同種別を2回期待していないか確認し、期待側を `Time.unscaledTime` 進行込みに直す。落ちなければ何もしない）

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/SoundEffectManager.cs moorestech_client/Assets/Scripts/Client.Tests/Mining/AttackTrackingMiningTarget.cs
git commit -m "feat(se): SoundEffectManager に8クリップ枠とクールダウンを適用"
```

---

### Task 3: 通知 messageId→SE 対応表と購読プレイヤー

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/NotificationSoundEffectTable.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/NotificationSoundEffectPlayer.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs:64` 付近（`RegisterRuntimeServices` 内）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameContainerActivation.cs:24-34`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/SoundEffect/NotificationSoundEffectTableTest.cs`

**Interfaces:**
- Produces: `public static class NotificationSoundEffectTable { public static bool TryGet(string messageId, out SoundEffectType type); }`
- Produces: `public sealed class NotificationSoundEffectPlayer`（ctor で購読。DI Singleton）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Client.Game.InGame.SoundEffect;
using NUnit.Framework;

namespace Client.Tests.SoundEffect
{
    public class NotificationSoundEffectTableTest
    {
        [TestCase("achievement.researchCompleted", SoundEffectType.ResearchComplete)]
        [TestCase("achievement.challengeCompleted", SoundEffectType.ChallengeComplete)]
        [TestCase("achievement.unlockedItem", SoundEffectType.Unlock)]
        [TestCase("achievement.unlockedCraftRecipe", SoundEffectType.Unlock)]
        [TestCase("achievement.unlockedMachineRecipe", SoundEffectType.Unlock)]
        [TestCase("achievement.unlockedBlock", SoundEffectType.Unlock)]
        [TestCase("achievement.unlockedTrainCar", SoundEffectType.Unlock)]
        [TestCase("achievement.unlockedConnectTool", SoundEffectType.Unlock)]
        [TestCase("achievement.unlockedBlueprint", SoundEffectType.Unlock)]
        public void AchievementMessageIdMapsToSoundEffect(string messageId, SoundEffectType expected)
        {
            Assert.IsTrue(NotificationSoundEffectTable.TryGet(messageId, out var type));
            Assert.AreEqual(expected, type);
        }

        [TestCase("itemEarned.mined")]
        [TestCase("denied.miningInventoryFull")]
        [TestCase("achievement.unknownFuture")]
        public void NonAchievementOrUnknownMessageIdHasNoSound(string messageId)
        {
            Assert.IsFalse(NotificationSoundEffectTable.TryGet(messageId, out _));
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `NotificationSoundEffectTable` 未定義

- [ ] **Step 3: 対応表を実装**

```csharp
namespace Client.Game.InGame.SoundEffect
{
    /// <summary>
    /// 通知 messageId と SE 種別の唯一の対応表。サーバー AchievementNotificationWiring の文字列と対にする
    /// The only mapping from notification messageId to SE type; paired with the strings in the server's AchievementNotificationWiring
    /// </summary>
    public static class NotificationSoundEffectTable
    {
        private const string ResearchCompletedMessageId = "achievement.researchCompleted";
        private const string ChallengeCompletedMessageId = "achievement.challengeCompleted";
        private const string UnlockedMessageIdPrefix = "achievement.unlocked";

        public static bool TryGet(string messageId, out SoundEffectType type)
        {
            if (messageId == ResearchCompletedMessageId)
            {
                type = SoundEffectType.ResearchComplete;
                return true;
            }
            if (messageId == ChallengeCompletedMessageId)
            {
                type = SoundEffectType.ChallengeComplete;
                return true;
            }
            // 解放系は種類を問わず同じ音。新種の unlocked* が増えても追従する
            // All unlock kinds share one sound, so future unlocked* ids are covered automatically
            if (messageId.StartsWith(UnlockedMessageIdPrefix))
            {
                type = SoundEffectType.Unlock;
                return true;
            }
            type = default;
            return false;
        }
    }
}
```

テスト `achievement.unknownFuture` は prefix に該当しないので false になる（意図どおり）。

- [ ] **Step 4: 購読プレイヤーを実装**

```csharp
using Client.Game.InGame.Context;
using MessagePack;
using Server.Event.Notification;

namespace Client.Game.InGame.SoundEffect
{
    /// <summary>
    /// サーバー通知イベントを購読し、messageId に対応する結果音を鳴らす。Web UI の有無に依存しない
    /// Subscribes to server notification events and plays the result sound mapped from messageId; independent of the web UI
    /// </summary>
    public sealed class NotificationSoundEffectPlayer
    {
        public NotificationSoundEffectPlayer()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(NotificationService.EventTag, OnNotification);
        }

        private void OnNotification(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<NotificationMessagePack>(payload);
            if (!NotificationSoundEffectTable.TryGet(message.MessageId, out var type)) return;
            SoundEffectManager.Instance.PlaySoundEffect(type);
        }
    }
}
```

- [ ] **Step 5: DI 登録と eager resolve**

`MainGameInteractionRegistration.cs` の `RegisterRuntimeServices(ContainerBuilder builder)` 本体の末尾に追加（`using Client.Game.InGame.SoundEffect;` を先頭に追加）:

```csharp
            builder.Register<NotificationSoundEffectPlayer>(Lifetime.Singleton);
```

`MainGameContainerActivation.ResolveRequiredServices` の `resolver.Resolve<ChallengeManager>();` の次行に追加（`using Client.Game.InGame.SoundEffect;` 追加）:

```csharp
            resolver.Resolve<NotificationSoundEffectPlayer>();
```

- [ ] **Step 6: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "NotificationSoundEffectTableTest"`
Expected: 12 passed

- [ ] **Step 7: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/ moorestech_client/Assets/Scripts/Client.Starter/Registration/ moorestech_client/Assets/Scripts/Client.Tests/SoundEffect/
git commit -m "feat(se): 通知イベント購読で研究成功・解放・チャレンジ完了音を鳴らす"
```

---

### Task 4: 研究画面の開閉音

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/ResearchTreeState.cs`

**Interfaces:**
- Consumes: `SoundEffectManager.Instance.PlaySoundEffect`

- [ ] **Step 1: OnEnter / OnExit に追加**

`using Client.Game.InGame.SoundEffect;` を追加し、

```csharp
        public void OnEnter(UITransitContext context)
        {
            // リサーチUIの表示とカーソル制御
            // Show research UI and update cursor
            _researchTreeViewManager.SetActive(true);
            InputManager.MouseCursorVisible(true);
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.UiOpen);
        }
```

```csharp
        public void OnExit()
        {
            // リサーチUIを閉じてカーソルを隠す
            // Hide research UI and the cursor
            _researchTreeViewManager.SetActive(false);
            InputManager.MouseCursorVisible(false);
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.UiClose);
        }
```

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 0 errors

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/ResearchTreeState.cs
git commit -m "feat(se): 研究画面の開閉に UiOpen/UiClose を付ける"
```

---

### Task 5: `sound.play` アクション（C#）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/UiSoundTypeTable.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/SoundEffectActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:185`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/error_codes.json`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/SoundEffect/UiSoundTypeTableTest.cs`

**Interfaces:**
- Produces: `public static class UiSoundTypeTable { public static bool TryGet(string webName, out SoundEffectType type); }`（受理: `uiSelect` `uiClose` `uiConfirm` `uiDenied`）
- Produces: `SoundEffectPlayActionHandler : IActionHandler`（`ActionType = "sound.play"`、payload `{ type: string }`、失敗コード `invalid_payload` / `invalid_type`）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Client.Game.InGame.SoundEffect;
using NUnit.Framework;

namespace Client.Tests.SoundEffect
{
    public class UiSoundTypeTableTest
    {
        [TestCase("uiSelect", SoundEffectType.UiSelect)]
        [TestCase("uiClose", SoundEffectType.UiClose)]
        [TestCase("uiConfirm", SoundEffectType.UiConfirm)]
        [TestCase("uiDenied", SoundEffectType.UiDenied)]
        public void WebNameMapsToUiSoundEffect(string webName, SoundEffectType expected)
        {
            Assert.IsTrue(UiSoundTypeTable.TryGet(webName, out var type));
            Assert.AreEqual(expected, type);
        }

        [TestCase("UiSelect")]
        [TestCase("destroyBlock")]
        [TestCase("researchComplete")]
        [TestCase("")]
        public void NonUiOrUnknownNameIsRejected(string webName)
        {
            // Web からは UI 操作音だけを鳴らせる。結果音・ワールド音は Web 契約に載せない
            // The web may only trigger UI sounds; result and world sounds stay out of the web contract
            Assert.IsFalse(UiSoundTypeTable.TryGet(webName, out _));
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `UiSoundTypeTable` 未定義

- [ ] **Step 3: 対応表を実装**

```csharp
namespace Client.Game.InGame.SoundEffect
{
    /// <summary>
    /// Web の sound.play 用途名と SE 種別の唯一の対応表。TS 側 UiSoundType と対にする
    /// The only mapping from the web's sound.play names to SE types; paired with the TS UiSoundType union
    /// </summary>
    public static class UiSoundTypeTable
    {
        public static bool TryGet(string webName, out SoundEffectType type)
        {
            switch (webName)
            {
                case "uiSelect": type = SoundEffectType.UiSelect; return true;
                case "uiClose": type = SoundEffectType.UiClose; return true;
                case "uiConfirm": type = SoundEffectType.UiConfirm; return true;
                case "uiDenied": type = SoundEffectType.UiDenied; return true;
            }
            type = default;
            return false;
        }
    }
}
```

- [ ] **Step 4: ハンドラを実装**

`SoundEffectActions.cs`:

```csharp
using Client.Game.InGame.SoundEffect;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// sound.play: Web の操作音要求を用途名で受け、SoundEffectManager で再生する
    /// sound.play: receives a UI sound request by semantic name from the web and plays it via SoundEffectManager
    /// </summary>
    public class SoundEffectPlayActionHandler : IActionHandler
    {
        public string ActionType => "sound.play";

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload == null) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (payload["type"] is not JValue { Type: JTokenType.String } typeValue) return UniTask.FromResult(ActionResult.Fail("invalid_payload"));
            if (!UiSoundTypeTable.TryGet((string)typeValue, out var type)) return UniTask.FromResult(ActionResult.Fail("invalid_type"));

            SoundEffectManager.Instance.PlaySoundEffect(type);
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
```

- [ ] **Step 5: 登録とフィクスチャ更新**

`WebUiGameBinder.cs` の `hub.RegisterAction(new ResearchCompleteActionHandler(researchTopic));` の次行に:

```csharp
            hub.RegisterAction(new SoundEffectPlayActionHandler());
```

`action_names.json` の `"skit.set_ui_hidden"` の後に `"sound.play"` を追加（末尾カンマに注意）。
`error_codes.json` の `"codes"` 配列末尾に `"invalid_type"` を追加。

- [ ] **Step 6: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "UiSoundTypeTableTest|WireContractActionNamesTest|WireContractErrorCodes"`
Expected: すべて pass（`WireContractErrorCodes` 系のテスト名は `ls moorestech_client/Assets/Scripts/Client.Tests/WebUi/` で実名を確認してから regex に入れる）

- [ ] **Step 7: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/SoundEffect/UiSoundTypeTable.cs moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/SoundEffectActions.cs moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs moorestech_client/Assets/Scripts/Client.Tests/
git commit -m "feat(webui-host): sound.play アクションを追加"
```

---

### Task 6: Web 側 `playUiSound`（bridge）

**Files:**
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`
- Create: `moorestech_web/webui/src/bridge/transport/uiSound.ts`
- Create: `moorestech_web/webui/src/bridge/transport/uiSound.test.ts`
- Modify: `moorestech_web/webui/src/bridge/index.ts`

**Interfaces:**
- Produces: `export type UiSoundType = "uiSelect" | "uiClose" | "uiConfirm" | "uiDenied"`（actionContract.ts）
- Produces: `export function playUiSound(type: UiSoundType): void`（bridge から公開）

- [ ] **Step 1: 失敗するテストを書く**

```ts
import { describe, expect, it, vi } from "vitest";

const sendActionMock = vi.hoisted(() => vi.fn());
const notifyMock = vi.hoisted(() => vi.fn());
vi.mock("./webSocketClient", () => ({ sendAction: sendActionMock }));
vi.mock("./notify", () => ({ notify: notifyMock }));

import { playUiSound } from "./uiSound";

// 操作音は演出であり、失敗・切断でプレイヤーの操作を邪魔しない
// UI sounds are presentation; a failure or disconnect must never interrupt the player with a toast
describe("playUiSound", () => {
  it("sound.play を用途名付きで送る", () => {
    sendActionMock.mockResolvedValueOnce({ ok: true });
    playUiSound("uiSelect");
    expect(sendActionMock).toHaveBeenCalledWith("sound.play", { type: "uiSelect" });
  });

  it("失敗応答でもトーストしない", async () => {
    sendActionMock.mockResolvedValueOnce({ ok: false, error: "invalid_type" });
    playUiSound("uiConfirm");
    await Promise.resolve();
    expect(notifyMock).not.toHaveBeenCalled();
  });

  it("切断中の reject でもトーストせず例外を漏らさない", async () => {
    sendActionMock.mockRejectedValueOnce(new Error("disconnected"));
    expect(() => playUiSound("uiClose")).not.toThrow();
    await Promise.resolve();
    await Promise.resolve();
    expect(notifyMock).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `cd moorestech_web/webui && npx vitest run src/bridge/transport/uiSound.test.ts`
Expected: FAIL（`./uiSound` が無い）

- [ ] **Step 3: 契約と実装**

`actionContract.ts` の `"research.complete": { researchGuid: string };` の直後に:

```ts
  // Web は用途名だけを送る。クリップ選択・クールダウンは C# SoundEffectManager が持つ（ADR 0037）
  // The web sends only a semantic name; clip choice and cooldown live in the C# SoundEffectManager (ADR 0037)
  "sound.play": { type: UiSoundType };
```

同ファイルの型定義群（`ActionPayloads` より上）に:

```ts
export type UiSoundType = "uiSelect" | "uiClose" | "uiConfirm" | "uiDenied";
```

`ACTION_TYPES` 配列の `"research.complete",` の次に `"sound.play",` を追加。

`uiSound.ts`:

```ts
import { sendAction } from "./webSocketClient";
import type { UiSoundType } from "./actionContract";

// 操作音は fire-and-forget。失敗も切断も握りつぶし、dispatchAction のトースト経路を通さない
// UI sounds are fire-and-forget: swallow failures and disconnects instead of routing through dispatchAction's toast path
export function playUiSound(type: UiSoundType): void {
  void sendAction("sound.play", { type }).catch(() => undefined);
}
```

`bridge/index.ts` に追加:

```ts
export { playUiSound } from "./transport/uiSound";
export type { UiSoundType } from "./transport/actionContract";
```

（`actionContract.ts` が `protocol.ts` から re-export されている場合は既存の `ActionPayloads` の export 経路に合わせる。`grep -n "actionContract" src/bridge/transport/protocol.ts` で確認）

- [ ] **Step 4: テストとパリティ**

Run: `cd moorestech_web/webui && npx vitest run src/bridge/transport/uiSound.test.ts src/bridge/contract/actionNames.test.ts`
Expected: すべて pass（`actionNames.test.ts` は Task 5 で更新した `action_names.json` と一致）

- [ ] **Step 5: コミット**

```bash
git add moorestech_web/webui/src/bridge/
git commit -m "feat(webui): sound.play 契約と playUiSound を追加"
```

---

### Task 7: 共通部品への一括適用（R9）

**Files:**
- Modify: `moorestech_web/webui/src/shared/ui/IconButton/index.tsx`, `index.test.ts`
- Modify: `moorestech_web/webui/src/shared/ui/PanelActionButton/index.tsx`
- Create: `moorestech_web/webui/src/shared/ui/PanelActionButton/index.test.ts`
- Modify: `moorestech_web/webui/src/shared/ui/ModeSwitch/index.tsx`, `index.test.ts`

**Interfaces:**
- Consumes: `playUiSound` from `@/bridge`

- [ ] **Step 1: IconButton のテストに追加**

`IconButton/index.test.ts` 先頭の import 群の後に mock を足し、既存 describe 内へ 1 ケース追加:

```ts
const playUiSoundMock = vi.hoisted(() => vi.fn());
vi.mock("@/bridge", () => ({ playUiSound: playUiSoundMock }));
```

```ts
  it("クリックで uiSelect を鳴らしてから onClick を呼ぶ", () => {
    const onClick = vi.fn();
    const renderer = create(createElement(IconButton, { onClick, ariaLabel: "Close" }));
    act(() => renderer.root.findByType("button").props.onClick());
    expect(playUiSoundMock).toHaveBeenCalledWith("uiSelect");
    expect(onClick).toHaveBeenCalledOnce();
  });
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `cd moorestech_web/webui && npx vitest run src/shared/ui/IconButton`
Expected: 新ケースが FAIL

- [ ] **Step 3: 3部品を実装**

`IconButton/index.tsx`: `import { playUiSound } from "@/bridge";` を追加し、`onClick={onClick}` を

```tsx
      onClick={() => { playUiSound("uiSelect"); onClick(); }}
```

`PanelActionButton/index.tsx`: 同様に import を追加し `onClick={onClick}` を

```tsx
    <button className={styles.button} type="button" data-testid={testId} onClick={() => { playUiSound("uiSelect"); onClick(); }}>
```

`ModeSwitch/index.tsx`: import を追加し `onClick={() => onChange(option.value)}` を

```tsx
            onClick={() => { playUiSound("uiSelect"); onChange(option.value); }}
```

- [ ] **Step 4: PanelActionButton / ModeSwitch のテスト**

`PanelActionButton/index.test.ts`（新規）:

```ts
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";

const playUiSoundMock = vi.hoisted(() => vi.fn());
vi.mock("@/bridge", () => ({ playUiSound: playUiSoundMock }));

import PanelActionButton from "./index";

describe("PanelActionButton", () => {
  it("クリックで uiSelect を鳴らしてから onClick を呼ぶ", () => {
    const onClick = vi.fn();
    const renderer = create(createElement(PanelActionButton, { onClick, testId: "act" }, "Sort"));
    act(() => renderer.root.findByType("button").props.onClick());
    expect(playUiSoundMock).toHaveBeenCalledWith("uiSelect");
    expect(onClick).toHaveBeenCalledOnce();
  });
});
```

`ModeSwitch/index.test.ts`: 既存 mock 群の後ろに同じ `playUiSoundMock` の hoisted mock を追加し、既存の「クリックで onChange」相当のケースに `expect(playUiSoundMock).toHaveBeenCalledWith("uiSelect");` を1行足す（既存テストの構造は実ファイルを読んで合わせる）。

- [ ] **Step 5: 全 UI テストと lint**

Run: `cd moorestech_web/webui && npx vitest run src/shared/ui && npm run lint`
Expected: pass。他の feature テストが `vi.mock("@/bridge", ...)` で `playUiSound` を含まない部分モックを使い `playUiSound is not a function` で落ちる場合は、そのテストの mock に `playUiSound: vi.fn()` を追加する（`npx vitest run` 全体で洗い出す）

- [ ] **Step 6: コミット**

```bash
git add moorestech_web/webui/src/shared/ui/
git commit -m "feat(webui): 共通ボタン部品のクリックで uiSelect を鳴らす"
```

---

### Task 8: 研究画面の操作音（R8）

**Files:**
- Modify: `moorestech_web/webui/src/features/research/ResearchNodeCard.tsx`
- Modify: `moorestech_web/webui/src/features/research/ResearchDetailPane.tsx`
- Modify: `moorestech_web/webui/src/features/research/ResearchDetailPane.test.ts`

**Interfaces:**
- Consumes: `playUiSound` from `@/bridge`、`deriveResearchButton(node).interactable / .completed`

- [ ] **Step 1: ResearchDetailPane のテストを追加**

既存の `vi.mock("@/bridge", ...)` の返却に `playUiSound: playUiSoundMock,` を追加し、`const playUiSoundMock = vi.hoisted(() => vi.fn());` を `dispatchMock` の隣に置く。既存 describe に追加（fixture の作り方は同ファイルの既存ケースに合わせる。`researchable` な node と `unresearchableNotEnoughItem` な node の2種を使う）:

```ts
  it("有効な研究ボタンは uiConfirm を鳴らして research.complete を送る", () => {
    const renderer = create(createElement(ResearchDetailPane, { node: researchableNode, owned: null, onClose: vi.fn() }));
    const button = renderer.root.findByProps({ "data-testid": `research-button-${researchableNode.guid}` });
    act(() => button.props.onClick());
    expect(playUiSoundMock).toHaveBeenCalledWith("uiConfirm");
    expect(dispatchMock).toHaveBeenCalledWith("research.complete", { researchGuid: researchableNode.guid });
  });

  it("素材不足で disabled のボタン領域を押すと uiDenied を鳴らす", () => {
    const renderer = create(createElement(ResearchDetailPane, { node: lackingNode, owned: null, onClose: vi.fn() }));
    const wrapper = renderer.root.findByProps({ "data-testid": "research-button-area" });
    act(() => wrapper.props.onPointerDown());
    expect(playUiSoundMock).toHaveBeenCalledWith("uiDenied");
    expect(dispatchMock).not.toHaveBeenCalled();
  });

  it("×で uiClose を鳴らして onClose を呼ぶ", () => {
    const onClose = vi.fn();
    const renderer = create(createElement(ResearchDetailPane, { node: researchableNode, owned: null, onClose }));
    act(() => renderer.root.findByProps({ "data-testid": "research-detail-close" }).props.onClick());
    expect(playUiSoundMock).toHaveBeenCalledWith("uiClose");
    expect(onClose).toHaveBeenCalledOnce();
  });
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `cd moorestech_web/webui && npx vitest run src/features/research/ResearchDetailPane.test.ts`
Expected: 新3ケースが FAIL

- [ ] **Step 3: ResearchDetailPane を実装**

import に `playUiSound` を追加（`import { dispatchAction, playUiSound } from "@/bridge";`）。

×ボタン:

```tsx
            <button type="button" className={styles.detailClose} data-testid="research-detail-close"
              onClick={() => { playUiSound("uiClose"); onClose(); }}>
```

研究ボタンを包み要素で囲む（disabled の `<button>` は click が来ないため包みで押下を拾う）:

```tsx
          {/* disabled ボタンは click を発火しないので、包みの pointerdown で否定音を出す */}
          {/* A disabled button emits no click, so the wrapper's pointerdown carries the denied sound */}
          <div
            data-testid="research-button-area"
            onPointerDown={() => { if (!button.completed && !button.interactable) playUiSound("uiDenied"); }}
          >
            <button
              type="button"
              className={styles.researchButton}
              disabled={!button.interactable}
              data-testid={`research-button-${node.guid}`}
              onClick={() => { playUiSound("uiConfirm"); void dispatchAction("research.complete", { researchGuid: node.guid }); }}
            >
              {button.completed ? t(L.ui.research.completed) : t(L.ui.research.action)}
            </button>
          </div>
```

- [ ] **Step 4: ResearchNodeCard を実装**

`import { playUiSound } from "@/bridge";` を追加し

```tsx
      onClick={() => { playUiSound("uiSelect"); onSelect(node.guid); }}
```

`ResearchTreePanel.test.ts` 等が `@/bridge` を部分モックしているなら `playUiSound: vi.fn()` を足す。

- [ ] **Step 5: テスト・lint・e2e 型チェック**

Run: `cd moorestech_web/webui && npx vitest run && npm run lint`
Expected: すべて pass。研究ボタンの CSS（`styles.researchButton` の幅/margin）が包み `div` で崩れていないか `style.module.css` を確認し、必要なら包みに `className={styles.researchButtonArea}` を与えて既存 margin を移す

- [ ] **Step 6: コミット**

```bash
git add moorestech_web/webui/src/features/research/
git commit -m "feat(webui): 研究画面の選択・閉じ・確定・否定に操作音を付ける"
```

---

### Task 9: クリップ入手・配置・CREDITS・Prefab 配線（R10, R11）

**Files:**
- Create: `moorestech_client/Assets/Asset/Common/SoundEffect/ui-select.ogg` `ui-close.ogg` `ui-confirm.ogg` `ui-denied.ogg` `ui-open.ogg` `research-complete.ogg` `unlock.ogg` `challenge-complete.ogg`
- Create: `moorestech_client/Assets/Asset/Common/SoundEffect/CREDITS.md`
- Modify（Unity 経由）: `moorestech_client/Assets/Asset/Common/Prefab/GameSystem.prefab`

- [ ] **Step 1: 素材を入手する**

Kenney「UI Audio」「Interface Sounds」パック（CC0）を第一候補にする。`WebFetch` で https://kenney.nl/assets/ui-audio と https://kenney.nl/assets/interface-sounds を開き、ページ上のライセンス表記が CC0 であることと zip のダウンロードURLを確認してから `curl -L -o <scratchpad>/kenney-ui-audio.zip <URL>` で取得・`unzip` する（URL を推測で書かない）。取得できない場合は OpenGameArt で CC0/CC-BY の UI SFX を探し、同じ手順で確認する。

割当（agent前提。差し替えは Inspector のみで済む）:

| SoundEffectType | ファイル | 候補（Kenney UI Audio / Interface Sounds） |
|---|---|---|
| UiSelect | ui-select.ogg | click1 / click_001 |
| UiClose | ui-close.ogg | switch2 / close_001 |
| UiConfirm | ui-confirm.ogg | confirmation_001 |
| UiDenied | ui-denied.ogg | error_001 |
| UiOpen | ui-open.ogg | open_001 / switch1 |
| ResearchComplete | research-complete.ogg | confirmation_004（長め） |
| Unlock | unlock.ogg | select_001 |
| ChallengeComplete | challenge-complete.ogg | confirmation_002 |

選んだ元ファイルを上表の名前にコピーして配置する。Unity が `.meta` を生成するので手で作らない。

- [ ] **Step 2: CREDITS.md を書く**

```markdown
# SoundEffect credits

| file | source | author | license |
|---|---|---|---|
| ui-select.ogg | https://kenney.nl/assets/ui-audio (click1) | Kenney | CC0 1.0 |
| ui-close.ogg | ... | Kenney | CC0 1.0 |
| ui-confirm.ogg | ... | ... | ... |
| ui-denied.ogg | ... | ... | ... |
| ui-open.ogg | ... | ... | ... |
| research-complete.ogg | ... | ... | ... |
| unlock.ogg | ... | ... | ... |
| challenge-complete.ogg | ... | ... | ... |

CC-BY 素材を使った場合は作者名・元URL・ライセンスURLを必ず埋める（表記義務）。
```

`...` は実際に使った素材で全行埋める（プレースホルダのままコミットしない）。

- [ ] **Step 3: Unity にインポートさせる**

Run: `uloop compile --project-path ./moorestech_client`（アセットリフレッシュを兼ねる）
Run: `ls moorestech_client/Assets/Asset/Common/SoundEffect/*.ogg.meta`
Expected: 8 ファイルの .meta が生成されている

- [ ] **Step 4: Prefab に配線する（uloop execute-dynamic-code）**

```csharp
using UnityEditor;
using UnityEngine;
using Client.Game.InGame.SoundEffect;

var prefabPath = "Assets/Asset/Common/Prefab/GameSystem.prefab";
var root = PrefabUtility.LoadPrefabContents(prefabPath);
var manager = root.GetComponentInChildren<SoundEffectManager>(true);
var so = new SerializedObject(manager);
var pairs = new (string field, string file)[]
{
    ("uiSelectSound", "ui-select.ogg"), ("uiCloseSound", "ui-close.ogg"), ("uiConfirmSound", "ui-confirm.ogg"),
    ("uiDeniedSound", "ui-denied.ogg"), ("uiOpenSound", "ui-open.ogg"), ("researchCompleteSound", "research-complete.ogg"),
    ("unlockSound", "unlock.ogg"), ("challengeCompleteSound", "challenge-complete.ogg"),
};
foreach (var (field, file) in pairs)
{
    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Asset/Common/SoundEffect/" + file);
    if (clip == null) throw new System.Exception("clip not found: " + file);
    so.FindProperty(field).objectReferenceValue = clip;
}
so.ApplyModifiedPropertiesWithoutUndo();
PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
PrefabUtility.UnloadPrefabContents(root);
Debug.Log("wired 8 clips");
```

Run: `uloop execute-dynamic-code --project-path ./moorestech_client --code '<上記>'`（引用の扱いは uloop-execute-dynamic-code スキル参照）
Expected: `wired 8 clips`

- [ ] **Step 5: 検証**

Run: `grep -n "uiSelectSound\|challengeCompleteSound" moorestech_client/Assets/Asset/Common/Prefab/GameSystem.prefab`
Expected: 両方とも `{fileID: 8300000, guid: ..., type: 3}` で null（`{fileID: 0}`）でない

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Asset/Common/SoundEffect/ moorestech_client/Assets/Asset/Common/Prefab/GameSystem.prefab
git commit -m "feat(se): UI/結果音クリップ8種を追加し GameSystem.prefab に配線"
```

---

### Task 10: 通し確認（unityプレイ録画テスト）

**Files:** なし（検証のみ）

- [ ] **Step 1: プレイテスト DSL で研究画面を通す**

unity-playmode-recorded-playtest スキルの DSL で「Rキーで研究画面を開く→原始研究1ノードをクリック→研究ボタン→閉じる」のシナリオを流し、`uloop get-logs --project-path ./moorestech_client --log-type Error` に `KeyNotFoundException`（辞書未登録）や `NullReferenceException`（クリップ未配線）が無いことを確認する。

- [ ] **Step 2: 素材不足クリックの確認**

同シナリオで消費素材を持たない状態の研究ボタン領域を押し、Error ログが無いことと `sound.play` が `invalid_type` を返していないこと（`uloop get-logs --log-type Warning` に `sound.play failed` が無い）を確認する。

- [ ] **Step 3: bd 更新**

```bash
bd note <id> "実装完了: 通しシナリオで研究画面の操作音・結果音・開閉音を確認"
```

---

### Task 11: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1: moores-code-review を実行する**

必ず最後にコードレビュースキル（moores-code-review）で全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘の機械的修正を適用し、設計判断は AskUserQuestion でまとめる。

- [ ] **Step 2: PR 作成後に worktree を畳む**

pr-create スキルで PR を作り、`moores-wt rm <name>` で撤収する。

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0037-research-sound-effects-via-notification-and-web-sound-play.md`（裁定6件は同ADRと `.decisions/2026-08-26-*` / `2026-08-27-*` に出所付きで記録済み）
- **結果音の購読者を `NotificationTopic` の外（Client.Game `NotificationSoundEffectPlayer`）に置く**: 出所 agent前提。ADR 決定3「通知イベント経由」の配置細目。理由: `NotificationTopic` は Web リレー（Client.WebUiHost）であり Web 未接続時に生成されない可能性がある／リレーへ副作用を混ぜない。前例 `ChallengeManager` の `SubscribeEventResponse`
- **対応キーは category ではなく messageId**: 出所 agent前提（事実訂正）。研究完了・解放・チャレンジ完了は全て `Category=Achievement` で、区別は `MessageId`（`achievement.researchCompleted` 等）にしかない。ADR 本文の「category 別」は messageId の意で読む
- **Web から鳴らせるのは Ui* 4種のみ**（`UiSoundTypeTable`）: 出所 agent前提。結果音・ワールド音を Web 契約に載せない（呼び出し側の線引き）
- **`playUiSound` は `dispatchAction` を通さず `sendAction` 直呼び＋catch**: 出所 agent前提（R7 の実現手段）。`BENIGN_ERRORS` へ載せる案は「切断時 reject でトーストしない」保証が別経路になるため不採用
- **クールダウン秒数 0.1 秒／`Time.unscaledTime`**: 出所 agent前提。クライアント演出であり GameUpdater ティック規約（サーバーロジック）の対象外
- **素材の第一候補 Kenney（CC0）と割当表**: 出所 agent前提。ユーザー裁定は「エージェントがフリー素材」「CC-BY 可」まで
