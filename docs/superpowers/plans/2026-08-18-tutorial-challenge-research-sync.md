# Tutorial Challenge Research Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 初期チュートリアルのチャレンジ構成を研究システムと同期させる（completeResearch新タスク種別・備蓄チャレンジ廃止・研究チャレンジ4件挿入・uiDragGuide矢印チュートリアル・veinPin設置誘導・HUD誘導）。

**Architecture:** サーバーは既存の3タスク種別と同型のイベント購読型タスク`CompleteResearchChallengeTask`を追加（`ResearchEvent.OnResearchCompleted`購読+初回tickの完了済みチェック）。クライアント/WebUIは既存のチュートリアル提示パイプライン（TutorialManager→各Manager→TutorialPresentationStateStore→topic→Webオーバーレイ）に新tutorialType `uiDragGuide` の書き手を1人追加し、Web側は受信したfrom/to anchorを解決して矢印ループアニメーションを描く。マスタデータは`generate_challenges.py`（正本ジェネレータ）のCHALLENGES表を書き換えて再生成する。

**Tech Stack:** Unity C#（サーバー/クライアント）、Mooresmaster SourceGenerator（YAMLスキーマ）、React+TypeScript+zod（WebUI）、Python（マスタ生成）

## Requirements

ADR: `docs/adr/0016-tutorial-challenge-lineup-research-sync.md`（全裁定の出所付き正本）

1. taskCompletionTypeに`completeResearch`を追加。taskParamは単一`researchNodeGuid`。指定研究の完了でチャレンジ達成
2. チャレンジ開始時点で既に完了済みの研究は取りこぼさず即クリア扱いになる（既存セーブ・順序ずれ対策）
3. チュートリアルカテゴリ「生きる基盤」を24件直列に再構成: 備蓄3件（板40/棒35/砕石25）削除、原始研究1〜4の完了チャレンジを素材が揃う各時点へ挿入（並びはADR §2）
4. 既存#11/#16のkeyControlチュートリアル（WebUIで元々非表示）は削除し、研究画面誘導は研究チャレンジ側のsummary+uiHighLight（研究ノードの枠線）へ移す
5. 風力掘削機設置チャレンジにveinPin（粘土鉱脈）で任意位置設置を誘導。座標固定のblockPlacePreviewは使わない
6. 新tutorialType `uiDragGuide`（fromUIObjectId/toUIObjectId）を追加し、WebUIにfrom→toへ矢印が移動をループする新presentation要素を追加。anchor未解決（対象UI非表示）中は非表示
7. 風力掘削機・石窯の設置チャレンジにuiDragGuide（ビルドメニューの該当ブロックエントリ→ホットバー）を付け、「Bでビルドメニューを開く」まではsummary文言で誘導
8. 最初のチャレンジ（小石3個）で、開始スキット100_start_gameへの目標HUD言及追記+uiHighLightによる左上チャレンジHUD枠線ハイライトを行う
9. マスタ検証: completeResearchのresearchNodeGuid実在チェック、uiDragGuideのbuildMenuBlock:{guid}実在チェックをChallengeMasterUtilに追加
10. やらないこと: チャレンジ→研究解放の逆方向gameAction追加／備蓄チャレンジ数量の再調整（廃止のため）／blockPlacePreviewの使用／uiHighLightへの文言表示復活／uGUI側のチュートリアル表示改修（uGUIは廃止方向・WebUIのみ）

## Global Constraints

- AGENTS.md全規約（partial禁止・Func<>禁止・1ファイル200行以下・イベントはUniRx・2行セットコメント・デフォルト引数禁止・try-catch原則禁止）
- スキーマ編集はedit-schemaスキルの手順に従う（`_CompileRequester.cs`のdummyText変更でSourceGeneratorトリガー・`optional: true`原則禁止・foreignKey追加時はバリデーション必須）
- WebUI実装はwebui-designスキルのホワイトリストに従う。**新表現（ドラッグガイド矢印）は様式が先、実装が後** — SKILL.mdへの§8.17追記をWebUI実装より前に行う（Task 5 Step 1）
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client` を実行
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "..."`（EditModeは`--test-mode EditMode`必須）
- 作業はタスク用worktree（`moores-wt new`）で行い、メインワークツリーでUnityを起動しない
- moorestech_masterリポジトリ（`../moorestech_master`）の変更は本repoと同期してコミットする
- GUID定数（本plan内で使用）:
  - 原始研究1: `837e9697-8586-406e-a0f6-16a010050218` / 2: `424be8c1-c40c-4644-8104-06934c59b147` / 3: `07d6226c-ed14-4a6f-aa2a-6fa085fce8ec` / 4: `858bcb10-b8ba-478e-9bc5-473ca61281a2`
  - 風力掘削機block: `934c0ef9-b76e-4058-8fc8-0ad74afbdcd0` / 粘土鉱脈vein: `18d2bd1f-737d-42d6-8c1e-27fa3a9ce1ca` / 青銅の鉱石鉱脈vein: `caabe578-b3ba-4222-8598-3dc5d8ccb660`
  - テストmod研究1（ForUnitTest）: `cd05e30d-d599-46d3-a079-769113cbbf17`

---

## File Structure

| ファイル | 責務 |
|---|---|
| `VanillaSchema/challenges.yml` (Modify) | completeResearch/uiDragGuideのスキーマ定義 |
| `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` (Modify) | SourceGeneratorトリガー |
| `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/CompleteResearchChallengeTask.cs` (Create) | 研究完了タスク本体 |
| `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/VanillaChallengeType.cs` (Modify) | 種別定数追加 |
| `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/ChallengeFactory.cs` (Modify) | 生成器登録 |
| `moorestech_server/Assets/Scripts/Game.Challenge/Game.Challenge.asmdef` (Modify) | Game.Research参照追加 |
| `moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs` (Modify) | 新パラメータのマスタ検証 |
| `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/challenges.json` (Modify) | テスト用completeResearchチャレンジ |
| `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/CompleteResearchChallengeTaskTest.cs` (Create/Test) | タスクの購読型/遡及型完了テスト |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/TutorialAnchorIdMapper.cs` (Modify) | 新ID（hotbar/challengeHud/buildMenuBlock:/researchNode:）変換 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/Presentation/TutorialPresentationData.cs` (Modify) | DragGuidesデータ追加 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/Presentation/TutorialPresentationStateStore.cs` (Modify) | AddDragGuide/RemoveDragGuide |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/UiDragGuideTutorialManager.cs` (Create) | uiDragGuideのViewManager |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/TutorialManager.cs` (Modify) | uiDragGuide登録 |
| `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/tutorial_anchor_ids.json` (Modify) | hotbar.hud追加 |
| `moorestech_client/Assets/Scripts/Client.Tests/WebUi/TutorialAnchorContractTest.cs` (Modify) | uiDragGuideのID突合を追加 |
| `moorestech_client/Assets/AddressableResources/Skit/skits/100_start_game.json` (Modify) | 目標HUD言及の台詞追記（CommandForge形式のプレーンJSON。Unityシリアライズ物ではないためテキスト編集可） |
| `.agents/skills/webui-design/SKILL.md` (Modify) | §8.17 ドラッグガイド様式の追記（実装より先） |
| `moorestech_web/webui/src/bridge/contract/schemas/presentation.ts` (Modify) | dragGuidesスキーマ |
| `moorestech_web/webui/src/shared/tutorialAnchor/anchorIds.ts` (Modify) | hotbarHud静的ID追加 |
| `moorestech_web/webui/src/features/hotbar/HotbarPanel/index.tsx` (Modify) | hotbar.hudアンカー宣言 |
| `moorestech_web/webui/src/features/tutorial/TutorialOverlay.tsx` (Modify) | 矢印ループ描画 |
| `moorestech_web/webui/src/features/tutorial/style.module.css` (Modify) | 矢印アニメーションCSS |
| `moorestech_web/webui/src/app/tokens.css` または `index.css` (Modify) | `--tutorial-drag-guide-*`トークン |
| `../moorestech_master/tools/tutorial_v3_port/generate_challenges.py` (Modify) | CHALLENGES表再構成・researchタスク・新チュートリアルヘルパ |
| `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json` (Regenerate) | 実データ |

---

### Task 1: challenges.ymlスキーマ拡張とSourceGenerator

**Files:**
- Modify: `VanillaSchema/challenges.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`

**Interfaces:**
- Produces: 生成型 `Mooresmaster.Model.ChallengesModule.CompleteResearchTaskParam`（プロパティ `System.Guid ResearchNodeGuid`）、`UiDragGuideTutorialParam`（`string FromUIObjectId` / `string ToUIObjectId`）、定数 `TutorialsElement.TutorialTypeConst.uiDragGuide`

- [x] **Step 1: edit-schemaスキルの`references/yaml_spec.md`を読む**（スキーマ編集の必須前提）

- [x] **Step 2: challenges.yml の taskCompletionType enum に completeResearch を追加**

`- blockPlace`（既存options末尾、73-76行付近）の直後に1行追加:

```yaml
        - key: taskCompletionType
          type: enum
          default: createItem
          options:
          - createItem
          - inInventoryItem
          - blockPlace
          - completeResearch
```

- [x] **Step 3: taskParam の cases に completeResearch を追加**

`- when: blockPlace` ケース（102-113行付近）の直後、`- key: tutorials` の前に追加:

```yaml
          - when: completeResearch
            type: object
            properties:
            - key: researchNodeGuid
              type: uuid
              foreignKey:
                schemaId: research
                foreignKeyIdPath: /data/[*]/researchNodeGuid
                displayElementPath: /data/[*]/researchNodeName
```

- [x] **Step 4: tutorialType enum に uiDragGuide を追加**

```yaml
            - key: tutorialType
              type: enum
              default: uiHighLight
              options:
              - mapObjectPin
              - veinPin
              - keyControl
              - uiHighLight
              - itemViewHighLight
              - blockPlacePreview
              - uiDragGuide
```

- [x] **Step 5: tutorialParam の cases に uiDragGuide を追加**

`- when: blockPlacePreview` ケースの直後（`- key: startedActions` の前）に追加:

```yaml
              - when: uiDragGuide
                type: object
                properties:
                - key: fromUIObjectId
                  type: string
                  default: from ui object id
                - key: toUIObjectId
                  type: string
                  default: to ui object id
```

- [x] **Step 6: _CompileRequester.cs の dummyText を変更**（SourceGeneratorトリガー）

`private const string dummyText = "...";` の値を任意の新文字列（例: `"complete-research-drag-guide"`）へ変更する。

- [x] **Step 7: コンパイルして生成型を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件。`CompleteResearchTaskParam` / `UiDragGuideTutorialParam` が生成される（既存JSONは新enumを使っていないためロード互換）。

- [x] **Step 8: コミット**

```bash
git add VanillaSchema/challenges.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "feat: チャレンジスキーマにcompleteResearchタスクとuiDragGuideチュートリアルを追加する"
```

---

### Task 2: サーバー CompleteResearchChallengeTask

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/CompleteResearchChallengeTask.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/VanillaChallengeType.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/ChallengeFactory.cs:12-17`
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/Game.Challenge.asmdef`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs:45-74`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/challenges.json`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/CompleteResearchChallengeTaskTest.cs`

**Interfaces:**
- Consumes: `Game.Research.ResearchEvent.OnResearchCompleted`（`IObservable<(int playerId, ResearchNodeMasterElement researchNode)>`）、`Game.Research.IResearchDataStore.IsResearchCompleted(Guid)`、`CompleteResearchTaskParam.ResearchNodeGuid`（Task 1生成物）
- Produces: `CompleteResearchChallengeTask`（`IChallengeTask`実装・`static IChallengeTask Create(ChallengeMasterElement)`）、定数 `VanillaChallengeType.CompleteResearchTask = "completeResearch"`

- [x] **Step 1: テストmodにcompleteResearchチャレンジを追加**

`Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/challenges.json` の Category1（`03ca4ded-3b2b-4e7f-bb6e-430f060c4ed1`）の `challenges` 配列末尾に追加（既存要素の書式に合わせる。`startedActions`/`clearedActions`/`displayListParam`は既存要素からコピーして流用する）:

```json
{
  "challengeGuid": "00000000-0000-0000-4567-000000000101",
  "title": "研究1を完了する",
  "summary": "研究1を完了する",
  "unlockAllPreviousChallengeComplete": true,
  "prevChallengeGuids": [],
  "taskCompletionType": "completeResearch",
  "taskParam": { "researchNodeGuid": "cd05e30d-d599-46d3-a079-769113cbbf17" },
  "tutorials": [],
  "startedActions": [],
  "clearedActions": [],
  "displayListParam": { "UIPosition": [0, 900], "UIScale": [0, 0, 0], "IconItem": "00000000-0000-0000-1234-000000000001" }
}
```

※ `prevChallengeGuids: []` なので初期チャレンジとして起動する。既存テストが初期チャレンジ数を数えている場合は期待値を更新する（Step 6で検出）。

- [x] **Step 2: 失敗するテストを書く**

`Tests/CombinedTest/Game/CompleteResearchChallengeTaskTest.cs` を新規作成。初期化・研究完了の作法は `Tests/CombinedTest/Game/ResearchDataStoreTest.cs`、tick進行は `Tests/CombinedTest/Server/PacketTest/Event/ChallengeCompletedEventTest.cs:89`（`GameUpdater.UpdateOneTick()`）に倣う:

```csharp
using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Challenge;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Research;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class CompleteResearchChallengeTaskTest
    {
        private const int PlayerId = 0;
        private static readonly Guid Research1Guid = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17");
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000101");

        // 研究完了イベントでチャレンジが完了する
        // Completing the research completes the challenge via the event
        [Test]
        public void ResearchCompleteEventCompletesChallenge()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();

            CompleteResearch(serviceProvider);

            Assert.IsTrue(challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid));
        }

        // チャレンジ開始前に完了済みの研究は初回tickで回収される
        // Research completed before the challenge starts is recovered on the first tick
        [Test]
        public void AlreadyCompletedResearchCompletesChallengeOnFirstTick()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();

            CompleteResearch(serviceProvider);
            challengeDatastore.InitializeCurrentChallenges();
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid));
        }

        private static void CompleteResearch(ServiceProvider serviceProvider)
        {
            // 消費アイテムを投入して研究を完了させる
            // Insert the consume items and complete the research
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var researchMaster = MasterHolder.ResearchMaster.GetResearch(Research1Guid);
            foreach (var consumeItem in researchMaster.ConsumeItems)
            {
                var item = ServerContext.ItemStackFactory.Create(consumeItem.ItemGuid, consumeItem.ItemCount);
                playerInventory.MainOpenableInventory.InsertItem(item);
            }
            var result = serviceProvider.GetService<IResearchDataStore>().CompleteResearch(Research1Guid, PlayerId);
            Assert.IsTrue(result);
        }
    }
}
```

※ `ServiceProvider` の実型名が違いコンパイルエラーになる場合は `ResearchDataStoreTest.cs` の `CompleteResearchForTest` の引数型に合わせる。

- [x] **Step 3: テスト実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "CompleteResearchChallengeTaskTest"`
Expected: FAIL（`ChallengeFactory` の Dictionary に `completeResearch` が無く `KeyNotFoundException`）

- [x] **Step 4: 実装を書く**

`Game.Challenge.asmdef` の references に `"Game.Research"` を追加（`Game.Research` は `Game.Challenge` を参照していないため循環しない）。

`VanillaChallengeType.cs`:

```csharp
namespace Game.Challenge.Task.Factory
{
    public class VanillaChallengeType
    {
        public const string CreateItemTask = "createItem";
        public const string InInventoryItemTask = "inInventoryItem";
        public const string BlockPlaceTask = "blockPlace";
        public const string CompleteResearchTask = "completeResearch";
    }
}
```

`ChallengeFactory.cs` のコンストラクタに追加:

```csharp
            _taskCreators.Add(VanillaChallengeType.CompleteResearchTask,CompleteResearchChallengeTask.Create);
```

`CompleteResearchChallengeTask.cs` を新規作成:

```csharp
using System;
using Game.Context;
using Game.Research;
using Mooresmaster.Model.ChallengesModule;
using Mooresmaster.Model.ResearchModule;
using UniRx;

namespace Game.Challenge.Task
{
    public class CompleteResearchChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();

        private bool _completed;
        private bool _initialCheckDone;

        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new CompleteResearchChallengeTask(challengeMasterElement);
        }

        public CompleteResearchChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;

            var researchEvent = ServerContext.GetService<ResearchEvent>();
            researchEvent.OnResearchCompleted.Subscribe(OnResearchCompleted);
        }

        private void OnResearchCompleted((int playerId, ResearchNodeMasterElement researchNode) research)
        {
            if (_completed) return;

            var param = (CompleteResearchTaskParam)ChallengeMasterElement.TaskParam;
            if (research.researchNode.ResearchNodeGuid != param.ResearchNodeGuid) return;

            _completed = true;
            _onChallengeComplete.OnNext(this);
        }

        public void ManualUpdate()
        {
            // チャレンジ開始前に完了済みの研究を初回tickだけ照会して取りこぼしを防ぐ
            // Query once on the first tick to recover research completed before this challenge started
            if (_completed || _initialCheckDone) return;
            _initialCheckDone = true;

            var param = (CompleteResearchTaskParam)ChallengeMasterElement.TaskParam;
            var researchDataStore = ServerContext.GetService<IResearchDataStore>();
            if (!researchDataStore.IsResearchCompleted(param.ResearchNodeGuid)) return;

            _completed = true;
            _onChallengeComplete.OnNext(this);
        }
    }
}
```

※ 完了チェックをコンストラクタでなくManualUpdateで行う理由: `ChallengeDatastore.CreateChallenge`は生成後に`OnChallengeComplete`を購読するため、コンストラクタ内でOnNextすると購読前で取りこぼす。

`ChallengeMasterUtil.cs` の `TaskParamValidation` switch に case を追加（`BlockPlaceTaskParam` caseの直後）:

```csharp
                            case CompleteResearchTaskParam completeResearch:
                            {
                                // 参照先研究ノードの実在を検証
                                // Validate that the referenced research node exists
                                if (!MasterHolder.ResearchMaster.ResearchElements.ContainsKey(completeResearch.ResearchNodeGuid))
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.ResearchNodeGuid:{completeResearch.ResearchNodeGuid}\n";
                                }
                                break;
                            }
```

`ChallengeMasterUtil.cs` の `TutorialValidation` switch にも case を追加（`BlockPlacePreviewTutorialParam` caseの直後）。uiDragGuideの`buildMenuBlock:`書式が指すブロックの実在をマスタ側でも検証する:

```csharp
                                case UiDragGuideTutorialParam uiDragGuide:
                                {
                                    // buildMenuBlock:書式が指すブロックの実在を検証
                                    // Validate blocks referenced by the buildMenuBlock: form
                                    logs += ValidateDragGuideObjectId(uiDragGuide.FromUIObjectId, challenge.Title);
                                    logs += ValidateDragGuideObjectId(uiDragGuide.ToUIObjectId, challenge.Title);
                                    break;
                                }
```

`TutorialValidation` のローカル関数として追加（`#region Internal` 内・`ValidateGameActions` と同じ並び）:

```csharp
            string ValidateDragGuideObjectId(string uiObjectId, string challengeTitle)
            {
                const string blockPrefix = "buildMenuBlock:";
                if (!uiObjectId.StartsWith(blockPrefix)) return "";

                if (!Guid.TryParse(uiObjectId.Substring(blockPrefix.Length), out var blockGuid) ||
                    MasterHolder.BlockMaster.GetBlockIdOrNull(blockGuid) == null)
                {
                    return $"[ChallengeMaster] Challenge:{challengeTitle} has invalid uiDragGuide target:{uiObjectId}\n";
                }
                return "";
            }
```

- [x] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [x] **Step 6: テスト実行して通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "CompleteResearchChallengeTaskTest"`
Expected: PASS 2件

続けて既存チャレンジ系の回帰:
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Challenge"`
Expected: 全件PASS。テストmodへの初期チャレンジ追加で件数期待値が壊れたテストがあれば期待値を+1更新する。

- [x] **Step 7: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Challenge moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs moorestech_server/Assets/Scripts/Tests.Module moorestech_server/Assets/Scripts/Tests
git commit -m "feat: 研究完了を達成条件とするcompleteResearchチャレンジタスクを追加する"
```

---

### Task 3: Unityクライアント uiDragGuide経路とアンカー拡張

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/TutorialAnchorIdMapper.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/Presentation/TutorialPresentationData.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/Presentation/TutorialPresentationStateStore.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/UiDragGuideTutorialManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/TutorialManager.cs:30-34`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/tutorial_anchor_ids.json`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/TutorialAnchorContractTest.cs`

**Interfaces:**
- Consumes: `UiDragGuideTutorialParam.FromUIObjectId` / `.ToUIObjectId`（Task 1生成物）、`TutorialsElement.TutorialTypeConst.uiDragGuide`
- Produces: `TutorialPresentationStateStore.AddDragGuide(string fromAnchorId, string toAnchorId)` → `ITutorialView`、`TutorialPresentationData.DragGuides`（`TutorialDragGuideData[]`: `GuideId`/`FromAnchorId`/`ToAnchorId`）、`TutorialAnchorIdMapper.FromUiObjectId` が `"hotbar"`→`"hotbar.hud"`・`"challengeHud"`→`"challenge.current-hud"`・`"buildMenuBlock:<guid>"`→`"build-menu.entry-block-<guid小文字>"`・`"researchNode:<guid>"`→`"research.node-<guid小文字>"` を解決

- [x] **Step 1: 失敗するテストを書く（アンカー契約テスト拡張）**

`TutorialAnchorContractTest.cs` に追記（既存3テストの後）:

```csharp
        // 動的uiObjectId書式（buildMenuBlock:/researchNode:）がWeb側動的prefixへ変換されること
        // Dynamic uiObjectId forms must map onto the Web-side dynamic prefixes
        [Test]
        public void DynamicUiObjectIdsMapToWebDynamicPrefixes()
        {
            var fixture = LoadFixture();
            var buildMenuPrefix = fixture["dynamicPrefixes"]["buildMenuEntry"].Value<string>();
            var researchPrefix = fixture["dynamicPrefixes"]["researchNode"].Value<string>();

            var blockAnchor = TutorialAnchorIdMapper.FromUiObjectId("buildMenuBlock:934C0EF9-B76E-4058-8FC8-0AD74AFBDCD0");
            Assert.AreEqual($"{buildMenuPrefix}block-934c0ef9-b76e-4058-8fc8-0ad74afbdcd0", blockAnchor);

            var researchAnchor = TutorialAnchorIdMapper.FromUiObjectId("researchNode:837E9697-8586-406E-A0F6-16A010050218");
            Assert.AreEqual($"{researchPrefix}837e9697-8586-406e-a0f6-16a010050218", researchAnchor);
        }

        // 全modのchallenges.jsonが宣言するuiDragGuideのfrom/toが既知のuiObjectIdであること
        // Every uiDragGuide from/to declared across all mods must be a known uiObjectId
        [Test]
        public void AllModDragGuideUiObjectIdsAreKnownToMapper()
        {
            var masterRoot = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../moorestech_master"));
            if (!Directory.Exists(masterRoot))
            {
                Assert.Ignore($"moorestech_master repository not found at {masterRoot}");
                return;
            }

            foreach (var uiObjectId in CollectDragGuideUiObjectIds(masterRoot))
            {
                Assert.IsTrue(TutorialAnchorIdMapper.IsKnownUiObjectId(uiObjectId), $"'{uiObjectId}' is not a known key in TutorialAnchorIdMapper");
            }
        }
```

`#region Internal` に収集ヘルパを追加（`CollectHighLightUIObjectIds` と同型で `$..fromUIObjectId` と `$..toUIObjectId` の両方をSelectTokensする）:

```csharp
        private static List<string> CollectDragGuideUiObjectIds(string masterRoot)
        {
            var result = new List<string>();
            foreach (var serverDir in Directory.GetDirectories(masterRoot, "server*"))
            {
                var modsDir = Path.Combine(serverDir, "mods");
                if (!Directory.Exists(modsDir)) continue;

                foreach (var modDir in Directory.GetDirectories(modsDir))
                {
                    var challengesPath = Path.Combine(modDir, "master", "challenges.json");
                    if (!File.Exists(challengesPath)) continue;

                    var json = JToken.Parse(File.ReadAllText(challengesPath));
                    result.AddRange(json.SelectTokens("$..fromUIObjectId").Select(t => t.Value<string>()));
                    result.AddRange(json.SelectTokens("$..toUIObjectId").Select(t => t.Value<string>()));
                }
            }

            return result;
        }
```

- [x] **Step 2: テスト実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "TutorialAnchorContractTest"`
Expected: `DynamicUiObjectIdsMapToWebDynamicPrefixes` がFAIL（`KeyNotFoundException`）

- [x] **Step 3: TutorialAnchorIdMapper を拡張**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public static class TutorialAnchorIdMapper
    {
        // 動的アンカーIDのprefix。Web側TutorialAnchorDynamicPrefixesと対応する
        // Dynamic anchor ID prefix; must mirror Web's TutorialAnchorDynamicPrefixes
        public const string ItemAnchorPrefix = "recipe.item-";

        // マスタ側uiObjectIdの動的書式「種別:GUID」のprefix
        // Prefixes of the master-side dynamic uiObjectId form "kind:GUID"
        public const string BuildMenuBlockObjectIdPrefix = "buildMenuBlock:";
        public const string ResearchNodeObjectIdPrefix = "researchNode:";

        private static readonly IReadOnlyDictionary<string, string> UiAnchors =
            new Dictionary<string, string>
            {
                { "craftButton", "recipe.craft-button" },
                { "challengeHud", "challenge.current-hud" },
                { "hotbar", "hotbar.hud" },
            };

        public static string FromUiObjectId(string uiObjectId)
        {
            // 動的対象はGUIDを小文字化してWeb側の動的anchor生成規則へ揃える
            // Dynamic targets lower-case the GUID to match the web-side dynamic anchor rules
            if (uiObjectId.StartsWith(BuildMenuBlockObjectIdPrefix))
                return $"build-menu.entry-block-{uiObjectId.Substring(BuildMenuBlockObjectIdPrefix.Length).ToLowerInvariant()}";
            if (uiObjectId.StartsWith(ResearchNodeObjectIdPrefix))
                return $"research.node-{uiObjectId.Substring(ResearchNodeObjectIdPrefix.Length).ToLowerInvariant()}";
            return UiAnchors[uiObjectId];
        }

        public static string FromItemId(int itemId)
        {
            return $"{ItemAnchorPrefix}{itemId}";
        }

        // マスタ照合テスト用にマスタ側uiObjectIdの既知判定を公開する
        // Exposes known-key lookup for the master-data cross-check test
        public static bool IsKnownUiObjectId(string uiObjectId)
        {
            if (uiObjectId.StartsWith(BuildMenuBlockObjectIdPrefix))
                return Guid.TryParse(uiObjectId.Substring(BuildMenuBlockObjectIdPrefix.Length), out _);
            if (uiObjectId.StartsWith(ResearchNodeObjectIdPrefix))
                return Guid.TryParse(uiObjectId.Substring(ResearchNodeObjectIdPrefix.Length), out _);
            return UiAnchors.ContainsKey(uiObjectId);
        }

        // Web側フィクスチャとの突合テスト用に、静的マッピングの出力アンカーID全件を公開する
        // Exposes every statically mapped anchor ID for the parity test against the Web-side fixture
        public static IReadOnlyCollection<string> AllMappedAnchorIds => UiAnchors.Values.ToArray();
    }
}
```

- [x] **Step 4: フィクスチャに hotbar.hud を追加**

`tutorial_anchor_ids.json` の `staticIds` 配列へ `"hotbar.hud"` を追加（Web側 anchorIds.ts の変更はTask 5で同期する）。

- [x] **Step 5: TutorialPresentationData に DragGuides を追加**

```csharp
namespace Client.Game.InGame.Tutorial
{
    public class TutorialPresentationData
    {
        public string TutorialSessionId;
        public int Revision;
        public string ChallengeId;
        public TutorialHighlightData[] Highlights;
        public TutorialDragGuideData[] DragGuides;
    }

    public class TutorialHighlightData
    {
        public string HighlightId;
        public string AnchorId;
        public string Kind;
        public int PaddingPx;
        public bool BlocksPointerInput;
    }

    public class TutorialDragGuideData
    {
        public string GuideId;
        public string FromAnchorId;
        public string ToAnchorId;
    }
}
```

- [x] **Step 6: TutorialPresentationStateStore に AddDragGuide / RemoveDragGuide を追加**

変更点（既存構造は維持。`SetHighlights` は highlights と dragGuides の両方を受ける `SetState` へ改名し、既存呼び出しは現在値を渡す）:

```csharp
        public void BeginSession(Guid challengeId)
        {
            _current = new TutorialPresentationData
            {
                TutorialSessionId = Guid.NewGuid().ToString(),
                Revision = 0,
                ChallengeId = challengeId.ToString(),
                Highlights = Array.Empty<TutorialHighlightData>(),
                DragGuides = Array.Empty<TutorialDragGuideData>(),
            };
            Publish();
        }

        // D&D操作の説明矢印。from→toのanchor間ループはWeb側が描く
        // D&D guide arrow; the web side draws the looping motion between the anchors
        public ITutorialView AddDragGuide(string fromAnchorId, string toAnchorId)
        {
            var guide = new TutorialDragGuideData
            {
                GuideId = Guid.NewGuid().ToString(),
                FromAnchorId = fromAnchorId,
                ToAnchorId = toAnchorId,
            };
            var guides = new List<TutorialDragGuideData>(_current.DragGuides) { guide };
            SetState(_current.Highlights, guides.ToArray());
            return new TutorialDragGuideView(this, _current.TutorialSessionId, guide.GuideId);
        }

        public void RemoveDragGuide(string sessionId, string guideId)
        {
            if (sessionId != _current.TutorialSessionId) return;
            var guides = _current.DragGuides.Where(value => value.GuideId != guideId).ToArray();
            if (guides.Length == _current.DragGuides.Length) return;
            SetState(_current.Highlights, guides);
        }

        private void SetState(TutorialHighlightData[] highlights, TutorialDragGuideData[] dragGuides)
        {
            _current = new TutorialPresentationData
            {
                TutorialSessionId = _current.TutorialSessionId,
                Revision = _current.Revision + 1,
                ChallengeId = _current.ChallengeId,
                Highlights = highlights,
                DragGuides = dragGuides,
            };
            Publish();
        }
```

- `AddOutlineHighlight` / `RemoveHighlight` / `EndSession` 内の `SetHighlights(x)` 呼び出しは `SetState(x, _current.DragGuides)` へ置換。`EndSession` の空判定は `if (_current.Highlights.Length == 0 && _current.DragGuides.Length == 0) return;` とし、クリアは `SetState(Array.Empty<TutorialHighlightData>(), Array.Empty<TutorialDragGuideData>())`。
- `CreateIdle()` にも `DragGuides = Array.Empty<TutorialDragGuideData>()` を追加。
- `TutorialDragGuideView` は既存 `TutorialPresentationView` と同型で `CompleteTutorial()` が `RemoveDragGuide` を呼ぶクラスを同ファイルに追加。ファイルが200行を超える場合は `TutorialPresentationView` / `TutorialDragGuideView` を `Presentation/TutorialPresentationViews.cs` へ分離する。

- [x] **Step 7: UiDragGuideTutorialManager を新規作成**

```csharp
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.Tutorial.UIHighlight
{
    public class UiDragGuideTutorialManager : MonoBehaviour, ITutorialViewManager
    {
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            var param = (UiDragGuideTutorialParam)tutorial.TutorialParam;

            // D&DガイドはWebオーバーレイの矢印ループのみで表示する
            // The drag guide is rendered exclusively via the web overlay's looping arrow
            var fromAnchorId = TutorialAnchorIdMapper.FromUiObjectId(param.FromUIObjectId);
            var toAnchorId = TutorialAnchorIdMapper.FromUiObjectId(param.ToUIObjectId);
            return TutorialPresentationStateStore.Instance.AddDragGuide(fromAnchorId, toAnchorId);
        }
    }
}
```

※ MonoBehaviourの配置: `UIHighlightTutorialManager` がシーン/Prefabへどう配置されDIされているかを確認し（`TutorialManager` のコンストラクタ引数の注入元を遡る）、同じ場所へ `uloop execute-dynamic-code` でAddComponent+参照結線する。Prefab/シーンのテキスト直編集は禁止。

- [x] **Step 8: TutorialManager に登録**

コンストラクタ引数に `UiDragGuideTutorialManager uiDragGuideTutorialManager` を追加し（デフォルト引数禁止・呼び出し側も更新）、登録行を追加:

```csharp
            _tutorialViewManagers.Add(TutorialsElement.TutorialTypeConst.uiDragGuide, uiDragGuideTutorialManager);
```

- [x] **Step 9: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "TutorialAnchorContractTest|TutorialPresentation"`
Expected: 全件PASS（既存のpresentation系テストがDragGuides未初期化で落ちる場合は `Array.Empty` 初期化を期待値に追加）

- [x] **Step 10: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat: uiDragGuideチュートリアルのクライアント経路とアンカー変換を追加する"
```

---

### Task 4: スキット台本に目標HUD言及を追記

**Files:**
- Modify: `moorestech_client/Assets/AddressableResources/Skit/skits/100_start_game.json`

- [x] **Step 1: 台詞を挿入**

`commands` 配列の最後の `text` コマンド（`"id": 40`・「大丈夫です。ぼくがしっかりサポートするので…さあ、行きますよ」）の**直前**に挿入:

```json
    {
      "type": "text",
      "backgroundColor": "#ffffff",
      "characterId": "chr_002",
      "body": "画面の左上に、いま目指す目標が表示されています。\n迷ったらそこを確認してください。",
      "id": 139,
      "overrideCharacterName": "？？？"
    },
```

※ CommandForge形式のプレーンJSON（Unityシリアライズ物ではない）のためテキスト編集可。idは既存最大138の次の139。

- [x] **Step 2: JSONの整合を確認**

Run: `python3 -c "import json; json.load(open('moorestech_client/Assets/AddressableResources/Skit/skits/100_start_game.json')); print('ok')"`
Expected: `ok`

- [x] **Step 3: コミット**

```bash
git add moorestech_client/Assets/AddressableResources/Skit/skits/100_start_game.json
git commit -m "feat: 開始スキットに左上目標HUDの案内台詞を追加する"
```

---

### Task 5: WebUI ドラッグガイド矢印とアンカー

**Files:**
- Modify: `.agents/skills/webui-design/SKILL.md`（**最初に**・様式が先）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/presentation.ts:6-13`
- Modify: `moorestech_web/webui/src/shared/tutorialAnchor/anchorIds.ts`
- Modify: `moorestech_web/webui/src/features/hotbar/HotbarPanel/index.tsx:30`
- Modify: `moorestech_web/webui/src/features/tutorial/TutorialOverlay.tsx`
- Modify: `moorestech_web/webui/src/features/tutorial/style.module.css`
- Modify: `moorestech_web/webui/src/app/index.css`（トークン追加。`--z-*`・色トークンの定義場所）
- Test: 既存の `moorestech_web/webui/src/shared/tutorialAnchor/anchorIds.test.ts`（フィクスチャ突合）と `presentation` スキーマのテスト（存在すれば拡張）

**Interfaces:**
- Consumes: `tutorial.presentation` topicの `dragGuides`（Task 3のUnity側が配信: `{guideId, fromAnchorId, toAnchorId}`）
- Produces: `TutorialAnchorIds.hotbarHud = "hotbar.hud"`、矢印ループ描画

- [x] **Step 1: webui-design SKILL.md に §8.17 を追記**（ユーザー裁定2026-08-18に基づく様式化。§8.16の後に挿入）

```markdown
## 8.17 チュートリアルのドラッグガイド矢印

- **D&D操作の説明専用。** `tutorial.presentation` の `dragGuides`（from/to anchor）を受け、
  fromアンカー中心→toアンカー中心へカーソル型インラインSVGが移動をループするアニメーションを
  `TutorialOverlay` に描く。装飾ではなく操作説明であり、他用途への流用は禁止（ユーザー裁定 2026-08-18）。
- from/toの**両方**のアンカーが解決している間だけ表示する。片方でも未解決（対象UIが閉じている等）なら
  何も描かない。「対象UIを開くまでの誘導」はチャレンジsummary文言の責務。
- 図像は `--text-high-contrast` の塗り+世界分離用の最小限の固定長ドロップシャドウ（§8.12ツールボタンと同族）。
  新しい色相・光彩は使わない。寸法 `--tutorial-drag-guide-size`、周期 `--tutorial-drag-guide-duration` の
  固定長トークンで管理する。移動はCSS keyframesのtranslateで、ease-in-out・無限ループ・終端で不透明度を
  落として先頭へ戻る。
- `pointer-events: none` を維持し、z層は既存の tutorial overlay 内（新しい `--z-*` を増やさない）。
- e2e/スクリーンショット検証はアニメーション非同期のため座標一致を要求しない（表示有無のみ検証する）。
```

- [x] **Step 2: 失敗するテストを書く（アンカー単一ソースとスキーマ）**

`anchorIds.test.ts` はフィクスチャ（`tutorial_anchor_ids.json`）と `TutorialAnchorIds` の突合をしている。Task 3 Step 4でフィクスチャに `hotbar.hud` を足したため、この時点でテストは**既に失敗している**はず。確認:

Run: `cd moorestech_web/webui && npx vitest run src/shared/tutorialAnchor`
Expected: FAIL（`hotbar.hud` がWeb側に無い）

- [x] **Step 3: anchorIds.ts に hotbarHud を追加**

`TutorialAnchorIds` オブジェクトへ1行追加:

```ts
  hotbarHud: "hotbar.hud",
```

- [x] **Step 4: HotbarPanel にアンカーを宣言**

`HotbarPanel/index.tsx` の `data-hotbar-row` を持つdiv（30行付近）へ追加:

```tsx
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
...
      <div className={styles.hotbarFrame} data-testid="hotbar-grid" data-hotbar-row data-wheel-passthrough
        data-select-disabled={selectAccepted ? undefined : "true"}
        {...tutorialAnchor(TutorialAnchorIds.hotbarHud)}>
```

- [x] **Step 5: presentation.ts スキーマに dragGuides を追加**

```ts
// D&D説明の矢印ガイド。from/to両anchorが解決している間だけ描く
// Drag guide arrows for D&D instruction; drawn only while both anchors resolve
export const TutorialDragGuideSchema = z.object({
  guideId: z.string(), fromAnchorId: z.string(), toAnchorId: z.string(),
}).strict();
export const TutorialPresentationDataSchema = z.object({
  tutorialSessionId: z.string(), revision: z.number().int().nonnegative(),
  challengeId: z.string(), highlights: z.array(TutorialHighlightSchema),
  dragGuides: z.array(TutorialDragGuideSchema),
});
```

※ スキーマのテストファイル（`presentation.test.ts` 等）が存在すれば `dragGuides: []` を既存フィクスチャへ追加し、guide入りのケースを1件足す。Unity側スナップショット（`WebUiJson.Serialize`）が`dragGuides`を必ず含むため optional にしない。

- [x] **Step 6: TutorialOverlay に矢印ループを実装**

`TutorialOverlay.tsx` を拡張。dragGuideごとにfrom/to両anchorを `TutorialAnchorRegistry.subscribe` で解決し（ハイライトと同じ購読機構・ackはハイライトのみで矢印は送らない）、両方 `ready` のときだけ矢印を描く:

```tsx
    {presentation.dragGuides.map((guide) => {
      const from = resolvedGuides[`${guide.guideId}:from`];
      const to = resolvedGuides[`${guide.guideId}:to`];
      if (!from || from.status !== "ready" || !to || to.status !== "ready") return null;
      const fromX = from.rect.left + from.rect.width / 2;
      const fromY = from.rect.top + from.rect.height / 2;
      const toX = to.rect.left + to.rect.width / 2;
      const toY = to.rect.top + to.rect.height / 2;
      return <div key={guide.guideId} className={styles.dragGuide}
        style={{ left: fromX, top: fromY,
          "--drag-guide-dx": `${toX - fromX}px`, "--drag-guide-dy": `${toY - fromY}px` } as React.CSSProperties}>
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M6 3 L18 12 L11 13.5 L13.5 20 L10.5 21 L8 14.5 L3 18 Z" />
        </svg>
      </div>;
    })}
```

guide用の解決購読は専用の `useEffect` で束ねる（ackは送らない・§8.17）:

```tsx
  const [resolvedGuides, setResolvedGuides] = useState<Record<string, ResolvedAnchor>>({});

  useEffect(() => {
    if (!presentation || !registry.current) return;
    return combine(presentation.dragGuides.flatMap((guide) => [
      registry.current!.subscribe(guide.fromAnchorId, (value) =>
        setResolvedGuides((current) => ({ ...current, [`${guide.guideId}:from`]: value }))),
      registry.current!.subscribe(guide.toAnchorId, (value) =>
        setResolvedGuides((current) => ({ ...current, [`${guide.guideId}:to`]: value }))),
    ]));
  }, [presentation]);
```

`style.module.css` に追加:

```css
/* D&D説明のカーソル矢印。fromからtoへの移動をループする（webui-design §8.17） */
.dragGuide {
  position: absolute;
  width: var(--tutorial-drag-guide-size);
  height: var(--tutorial-drag-guide-size);
  margin: calc(var(--tutorial-drag-guide-size) / -2) 0 0 calc(var(--tutorial-drag-guide-size) / -2);
  pointer-events: none;
  animation: drag-guide-loop var(--tutorial-drag-guide-duration) ease-in-out infinite;
}
.dragGuide svg {
  width: 100%;
  height: 100%;
  fill: var(--text-high-contrast);
  filter: drop-shadow(0 1px 2px rgb(0 0 0 / 0.6));
}
@keyframes drag-guide-loop {
  0% { transform: translate(0, 0); opacity: 0; }
  15% { opacity: 1; }
  75% { transform: translate(var(--drag-guide-dx), var(--drag-guide-dy)); opacity: 1; }
  100% { transform: translate(var(--drag-guide-dx), var(--drag-guide-dy)); opacity: 0; }
}
```

`index.css` のトークン定義部へ追加:

```css
  --tutorial-drag-guide-size: 28px;
  --tutorial-drag-guide-duration: 1600ms;
```

- [x] **Step 7: テスト実行**

Run: `cd moorestech_web/webui && npx vitest run src/shared/tutorialAnchor src/bridge src/features/tutorial`
Expected: 全件PASS（TutorialOverlayのテストが `dragGuides` 欠落フィクスチャで落ちる場合は `dragGuides: []` を追加）

- [x] **Step 8: lint/型チェック**

Run: `cd moorestech_web/webui && npx tsc --noEmit && npx eslint src/features/tutorial src/shared/tutorialAnchor src/features/hotbar`
Expected: エラー0件

- [x] **Step 9: コミット**

```bash
git add .agents/skills/webui-design/SKILL.md moorestech_web/webui
git commit -m "feat: WebUIにD&D説明のドラッグガイド矢印とホットバーアンカーを追加する"
```

---

### Task 6: マスタデータ再構成（moorestech_masterリポジトリ）

**Files:**
- Modify: `../moorestech_master/tools/tutorial_v3_port/generate_challenges.py`
- Regenerate: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json`

**Interfaces:**
- Consumes: Task 1のスキーマ（completeResearch / uiDragGuide）、Task 3のuiObjectId書式（`buildMenuBlock:<guid>` / `researchNode:<guid>` / `hotbar` / `challengeHud`）
- Produces: 24件直列の新チャレンジ実データ

- [x] **Step 1: generate_challenges.py のヘルパを追加・変更**

既存ヘルパ群（`pin`/`ui`/`iv`/`key`）を以下へ変更。`key` ヘルパは削除する（keyControl全廃・Requirement 4）:

```python
research_by_name = {r['researchNodeName']: r['researchNodeGuid'] for r in research}
veins = {v['veinName']: v['veinGuid'] for v in load_map()['mapVeins']}

def pin(name, text): return ('mapObjectPin', {'mapObjectGuid': map_objects[name], 'pinText': text})
def vein(name, text): return ('veinPin', {'veinGuid': veins[name], 'pinText': text})
def ui(object_id, text): return ('uiHighLight', {'highLightUIObjectId': object_id, 'highLightText': text})
def iv(name, text): return ('itemViewHighLight', {'highLightItemGuid': items[name], 'highLightText': text})
def drag(block_name, text): return ('uiDragGuide', {
    'fromUIObjectId': f'buildMenuBlock:{blocks[block_name]}', 'toUIObjectId': 'hotbar'})
def research_node_ui(name, text): return ('uiHighLight', {
    'highLightUIObjectId': f'researchNode:{research_by_name[name]}', 'highLightText': text})
```

※ 既存の `ui('クラフトボタンで作成')` 呼び出しは `ui('craftButton', 'クラフトボタンで作成')` へ書き換える。`drag` の `text` 引数はスキーマに文言フィールドが無いため未使用（呼び出し側の可読性用）。

- [x] **Step 2: CHALLENGES 表を24件へ全面書き換え**

タスク種別 `'research'` を導入し、表全体を以下へ置換（ADR 0016 §2の並び。既存文言は維持し、変更点のみ差し替え）:

```python
CHALLENGES = [
    # (title, summary, task, target_name, count, tutorials, icon_name)
    ('小石を3個拾う', '地面の小石を左クリックで3個拾おう', 'item', '小石', 3,
     [pin('小石', '左クリックで拾う'), ui('challengeHud', '左上にいまの目標が表示される')], '小石'),
    ('石器を作る', '小石3個からインベントリで石器をクラフトしよう', 'craft', '石器', None,
     [ui('craftButton', 'クラフトボタンで作成'), iv('石器', '石器のレシピを確認')], '石器'),
    ('木を伐採して原木を入手する', '石器で木を伐採して原木を3個集めよう', 'item', '原木', 3,
     [pin('木', '石器で木を伐採')], '原木'),
    ('木の板を5枚作る', '原木から木の板を5枚クラフトしよう', 'item', '木の板', 5,
     [iv('木の板', '原木から木の板を作る')], '木の板'),
    ('木の棒を5本作る', '木の板から木の棒を5本クラフトしよう', 'item', '木の棒', 5, [], '木の棒'),
    ('原始研究1を完了する', 'Rキーで研究画面を開き、木の板5枚と木の棒5本で原始研究1を完了しよう', 'research', '原始研究1', None,
     [research_node_ui('原始研究1', '原始研究1を完了する')], '木の板'),
    ('石を採掘する', '石鉱脈から石を5個採掘しよう', 'item', '石', 5,
     [pin('石鉱脈', '石鉱脈から石を採掘')], '石'),
    ('砕いた石材を5個作る', '石から砕いた石材を5個クラフトしよう', 'item', '砕いた石材', 5, [], '砕いた石材'),
    ('原始研究2を完了する', '研究画面で木の板5枚・木の棒5本・砕いた石材5個を使い原始研究2を完了して、石の斧を解放しよう', 'research', '原始研究2', None,
     [research_node_ui('原始研究2', '原始研究2を完了する')], '砕いた石材'),
    ('石の斧を作る', '木の棒と砕いた石材で石の斧を作ろう', 'craft', '石の斧', None, [], '石の斧'),
    ('原始研究3を完了する', '研究画面で木の板10枚・木の棒5本・砕いた石材10個を使い原始研究3を完了して、風力掘削機を解放しよう', 'research', '原始研究3', None,
     [research_node_ui('原始研究3', '原始研究3を完了する')], '砕いた石材'),
    ('風力掘削機を設置する', 'Bでビルドメニューを開き、風力掘削機をホットバーへドラッグして粘土鉱脈の上に設置しよう', 'block', '風力掘削機', 1,
     [vein('粘土鉱脈', '粘土鉱脈の上に設置'), drag('風力掘削機', 'ホットバーへドラッグ')], '砕いた石材'),
    ('粘土を入手する', '風力掘削機で粘土を採掘して1個入手しよう', 'item', '粘土', 1, [], '粘土'),
    ('レンガを作る', '粘土からレンガをクラフトしよう', 'craft', 'レンガ', None,
     [iv('レンガ', '粘土からレンガを作る')], 'レンガ'),
    ('青銅の鉱石を5個採掘する', '青銅の鉱脈の上に風力掘削機を設置して青銅の鉱石を5個採掘しよう', 'item', '青銅の鉱石', 5,
     [vein('青銅の鉱石鉱脈', '青銅の鉱脈の上に掘削機を設置')], '青銅の鉱石'),
    ('青銅鉱石の粉を3個作る', '青銅の鉱石から青銅鉱石の粉を3個クラフトしよう', 'item', '青銅鉱石の粉', 3, [], '青銅鉱石の粉'),
    ('原始研究4を完了する', '研究画面で木の板20枚・木の棒20本・砕いた石材10個を使い原始研究4を完了して、石窯を解放しよう', 'research', '原始研究4', None,
     [research_node_ui('原始研究4', '原始研究4を完了する')], 'レンガ'),
    ('石窯を設置する', 'Bでビルドメニューを開き、石窯をホットバーへドラッグして設置しよう', 'block', '石窯', 1,
     [drag('石窯', 'ホットバーへドラッグ')], 'レンガ'),
    ('青銅インゴットを作る', '石窯に青銅鉱石の粉と原木を入れて青銅インゴットを精錬しよう', 'item', '青銅インゴット', 1, [], '青銅インゴット'),
    ('青銅シートを作る', '青銅インゴット3個から青銅シートをクラフトしよう', 'craft', '青銅シート', None, [], '青銅シート'),
    ('木釘を9本作る', '木の棒から木釘を9本クラフトしよう', 'item', '木釘', 9, [], '木釘'),
    ('合板を作る', '木釘と木の板で合板をクラフトしよう', 'craft', '合板', None, [], '合板'),
    ('補強棒材を作る', '木の棒と青銅シートで補強棒材をクラフトしよう', 'craft', '補強棒材', None, [], '補強棒材'),
    ('木のフレームを作る', '補強棒材と合板で木のフレームをクラフトしよう', 'craft', '木のフレーム', None, [], '木のフレーム'),
]
```

- [x] **Step 3: 'research' タスク種別の出力と検証を追加**

構築ループのtaskParam分岐へ追加:

```python
    elif task == 'research':
        c['taskCompletionType'] = 'completeResearch'
        c['taskParam'] = {'researchNodeGuid': research_by_name[target]}
```

到達可能性検証ループにも分岐を追加（researchはresearch_by_nameのKeyErrorで自然に検出されるため、blockの既存検証と同様にスキップで良い）:

```python
for title, _, task, target, _, _, _ in CHALLENGES:
    if task == 'research':
        if target not in research_by_name:
            errors.append(f'{title}: 研究 {target} が見つからない')
        continue
    ...
```

- [x] **Step 4: 再生成して差分確認**

```bash
cd ../moorestech_master && python3 tools/tutorial_v3_port/generate_challenges.py
git diff --stat server_v8/mods/moorestechAlphaMod_8/master/challenges.json
python3 -c "
import json
d=json.load(open('server_v8/mods/moorestechAlphaMod_8/master/challenges.json'))
chs=d['data'][0]['challenges']
print(len(chs))
print([c['title'] for c in chs])
print([c['taskCompletionType'] for c in chs if c['taskCompletionType']=='completeResearch'])
"
```
Expected: 24件。研究チャレンジ4件が`completeResearch`。keyControlが0件（`grep -c keyControl` で確認）。直列（各prevChallengeGuidsが直前1件）はジェネレータ構造上保証される。

- [x] **Step 5: サーバー側マスタ検証テストで実マスタを照合**

moorestechリポジトリ側で:
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "TutorialAnchorContractTest|ChallengeMasterValidation"`
Expected: 全件PASS（`AllModHighLightUIObjectIdsAreKnownToMapper` が新ID `challengeHud` / `researchNode:...` を、`AllModDragGuideUiObjectIdsAreKnownToMapper` が `buildMenuBlock:...` / `hotbar` を検証する）

※ 旧バージョンmod（server_v4〜v7等）のchallenges.jsonに未知のuiObjectIdが残っていて落ちる場合は、そのIDをマッパーへ追加するのではなく、テストの収集対象が旧modを含む仕様のままで良いか実データを確認し、実際に使われていない残骸ならユーザーへ報告して裁定を仰ぐ。

- [x] **Step 6: moorestech_master をコミット**

```bash
cd ../moorestech_master
git add tools/tutorial_v3_port/generate_challenges.py server_v8/mods/moorestechAlphaMod_8/master/challenges.json
git commit -m "feat: チュートリアルを研究同期構成へ再編する（備蓄廃止・研究4件・D&D/veinPin/HUD誘導）"
```

---

### Task 7: 統合検証（コンパイル・全テスト・目視QA）

- [x] **Step 1: フルコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [x] **Step 2: サーバー/クライアントEditModeテスト**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Challenge|Research|Tutorial"`
Expected: 全件PASS

- [x] **Step 3: WebUIテストスイート**

Run: `cd moorestech_web/webui && npx vitest run`
Expected: 既存赤（既知のロケール起因10件・moorestech-2lh.1）以外は全件PASS

- [x] **Step 4: 目視QA（webui-design §10）**

mock-host（`e2e/capture-eval.ts` の様式）でビルドメニュー+ホットバーを表示し、dragGuide入りの `tutorial.presentation` を再現してスクリーンショット撮影。確認項目:
1. 矢印がビルドメニューの対象エントリ中心→ホットバー中心へループ移動する
2. ビルドメニューを閉じると矢印が消える（anchor未解決）
3. チャレンジHUDのuiHighLight枠線が左上HUDに重なる
4. `pointer-events` が素通し（矢印上でもクリック可能）

- [x] **Step 5: unityプレイ録画テストで通し確認（推奨）**

unity-playmode-recorded-playtest スキルのプレイテストDSLで、新規ワールド開始→スキット送り→小石3個→（チートまたは実操作で）原始研究1完了→チャレンジHUDの進行を録画で確認する。masterデータはブランチ互換コミットへピン留めしたworktreeを使う（スキーマ不整合はMooresmasterLoaderExceptionで無言死するため必須）。

- [x] **Step 6: コミット（残作業があれば）**

```bash
git status --short && git add -A && git commit -m "test: 研究同期チュートリアルの統合検証を通す"
```

---

### Task 8: 全ブランチレビュー（省略不可）

- [x] **必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

moores-code-review スキルを起動し、本ブランチの全変更（moorestech_master側の変更も対象に含める）をレビューする。指摘の機械的修正を適用し、設計判断はAskUserQuestionで裁定を仰ぐ。

---

## 判断記録（ADR）

- 設計セッションのADR: `docs/adr/0016-tutorial-challenge-lineup-research-sync.md`（全裁定・出所付き）
- 裁定蒸留: `.decisions/2026-08-18-研究完了チャレンジを新設し備蓄チャレンジを廃止する.md` / `.decisions/2026-08-18-チュートリアル提示はWebUI経路に統一しD&Dは矢印ループで示す.md`
- planning中の追加判断:
  - **完了済み研究の遡及回収はManualUpdateの初回1回チェックで行う**。コンストラクタ内OnNextは`ChallengeDatastore.CreateChallenge`の購読前で取りこぼすため不可。毎tickポーリングは「状態変化の検知は購読で」の規約に反するため初回のみ。出所: agent前提（ChallengeDatastore.cs:111-116の購読順序という機構的制約）
  - **uiObjectIdの動的書式は「種別:GUID」（buildMenuBlock:/researchNode:）とし、TutorialAnchorIdMapperで変換する**。マスタ文字列→Webアンカーの変換責務は既存のマッパーに集約されており、その辞書+動的prefix機構（FromItemIdと同型）の前例に従う。出所: agent前提（TutorialAnchorIdMapper.cs既存構造）
  - **ドラッグガイドの矢印はackを送らない**。anchor_ackはハイライトの診断用で、ガイドは「未解決なら非表示」の挙動だけで十分。出所: agent前提（TutorialOverlay.tsx:29-34の既存ack責務）
  - **webui-design SKILL.mdへの§8.17追記を実装より先に行う**。ホワイトリスト「様式が先、実装が後」の規約。出所: agent前提（webui-designスキル大原則）
  - **マスタ実データはgenerate_challenges.pyの再生成で作る**（JSON手編集しない）。GUIDがuuid5決定的生成でスクリプトが正本のため。出所: agent前提（moorestech_master/tools/tutorial_v3_port既存構造）
  - **石窯設置チャレンジにもuiDragGuideを付け、風力掘削機と同じD&D導線を再提示する**。青銅採掘チャレンジには青銅鉱脈のveinPinを追加。出所: agent前提（裁定「#11（と#16）にチュートリアルを付与」の範囲内の具体化）
  - **research challengeのアイコンは素材アイテムを流用**（IconItemはアイテム参照のため研究固有アイコンは無い）。出所: agent前提（challenges.ymlのIconItem foreignKey制約）
  - **旧セーブ（削除された備蓄チャレンジがcurrentのセーブ）はチャレンジ進行が空になり止まる**。`ChallengeDatastore.LoadChallenge`は消えたguidをスキップし（:196,229）、CompletedGuids非空のため初期チャレンジも再追加されない（:212）。AGENTS.mdの「後方互換考慮不要」方針に従い救済コードは書かない。出所: agent前提（AGENTS.md互換方針の適用。救済が必要になったらユーザーへ裁定を仰ぐ）
