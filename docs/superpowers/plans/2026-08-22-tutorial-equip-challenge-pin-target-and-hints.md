# 初期チュートリアル調整（装備チャレンジ・木ピンのドロップ品指定・キーヒント赤字・ドラッグ矢印・研究説明文） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ADR 0029 の5裁定（装備チャレンジ新設＋`equipItem` 完了種別、`mapObjectPin` のドロップ品指定、キー操作ヒントの赤字、ドラッグ矢印の速度半分・大きさ2倍、研究説明文の全差し替え）を本体repo（schema/サーバー/クライアント/webui）とマスタrepo（generator・challenges.json・research.json・localization.csv・mod_3）へ同一マージ単位で実装する。

**Architecture:** スキーマ変更（`challenges.yml` に `equipItem` と `mapObjectPin.pinTargetType/pinTargetParam` のネストswitch、`research.yml` の default 削除）→ SourceGenerator 再生成 → コンパイルエラー駆動で参照を置換。サーバーは `EquipItemChallengeTask`（`IEquipmentInventoryUpdateEvent` 購読＋初回tick回収。前例 `CompleteResearchChallengeTask`）を Factory に登録。クライアントは `MapObjectPin` が `pinTargetParam` を `MapObjectPinTargetResolver` で mapObjectGuid 集合に解決し、`MapObjectGameObjectDatastore.SearchNearestMapObject(集合, 位置)` で最寄りを選ぶ。webui は `app/tokens.css` の3トークン変更のみ。マスタは `generate_challenges.py` の表に装備チャレンジ2件を追加して再生成し、research.json/localization.csv/mod_3 を手で更新する。

**Tech Stack:** Unity 6000.3 / C# (UniRx, VContainer), mooresmaster SourceGenerator (YAML schema), React+CSS (moorestech_web/webui, vitest), Python3 (マスタ生成・CSV更新), uloop CLI。

## Requirements

設計ADR: `docs/adr/0029-tutorial-equip-challenge-pin-target-and-hints.md`。裁定: `.decisions/2026-08-22-*.md` 4件。

- R1 `taskCompletionType` に `equipItem { itemGuid }` を追加する。達成条件は「選択中の装備スロット（`IEquipmentInventory.GetSelectedItem()`）に対象アイテムが入った時」。装備スロット更新・選択index更新の両イベントで判定し、チャレンジ開始時に既に装備済みなら初回 `ManualUpdate` で達成する。受け入れ: サーバーCombinedTest 3本（選択中スロットへ装備→達成／非選択スロットに入れただけでは未達成→選択で達成／開始前に装備済み→初回tickで達成）が緑。
- R2 v8マスタに「石器を装備する」（石器を作る→**石器を装備する**→木を伐採）と「石の斧を装備する」（石の斧を作る→**石の斧を装備する**→原始研究3）を独立チャレンジとして追加し、tutorials に `keyControl{GameScreen, Tab, "インベントリを開いて<道具>を装備"}` と `uiDragGuide{inventory.item-<guid> → equipment.selected-slot}` を付ける。「木を伐採して原木を入手する」からは keyControl/uiDragGuide を外し mapObjectPin だけ残す。受け入れ: `challenges.json` の順序・prevChallengeGuids が直列で、既存 challengeGuid/tutorialGuid は変わらない（key不変）。
- R3 `mapObjectPin` の tutorialParam を `pinTargetType: enum{mapObject, earnItem}` ＋ `pinTargetParam` switch（`mapObject{mapObjectGuid}` / `earnItem{itemGuid}`）にする。`pinText` は据え置き。木を伐採は `earnItem{原木}`、小石拾いと mod_3 の4件は `mapObject{...}`。受け入れ: クライアント EditMode テストで earnItem 指定が「earnItems に当該 itemGuid を含む全 mapObjectGuid」に解決される。サーバーの `ChallengeMasterUtil` が両ケースの GUID 実在を検証する。
- R4 クライアント `MapObjectPin` は解決した mapObjectGuid 集合のうち未破壊の最寄りへピンする。受け入れ: `MapObjectGameObjectDatastore.SearchNearestMapObject` が GUID 集合を受ける形になり、単一GUIDの旧シグネチャは残さない。
- R5 キー操作ヒント（`keyHintText` 共有様式）の文字色を `--text-insufficient`（#ff7878）にする。チュートリアル keyControl HUD・インベントリ画面左下・研究画面左下の3箇所すべてが赤になる。受け入れ: `tokens.css` に `--key-hint-color` が追加され `:where(.keyHintText)` と配下 `kbd` がそれを使う。vitest 緑。
- R6 `--tutorial-drag-guide-size: 56px`、`--tutorial-drag-guide-duration: 3200ms`。keyframes比率は据え置き。
- R7 `research.yml` の `researchNodeDescription` から `default: New Research Description` を削除（必須化）し、v8 `research.json` 全ノードの `researchNodeDescription` を「何を解放するか」を軸にした日本語1行に差し替える。`localization.csv` の `research.<guid>.description` 行（Source/english/japanese）も同時更新する。受け入れ: research.json と localization.csv のどこにも "New Research Description" が残らない。
- R8 スキーマ変更に伴い、`mapObjectPin` を含む全マスタJSON（v8 challenges.json・mod_3 challenges.json）と `research.json` を同一マージ単位で更新し、本体 `.moorestech-external-revisions.json` の `moorestech_master.commitHash` をマスタPRのマージコミットへ更新する。
- R9 新チャレンジ・新チュートリアルの `challenge.<guid>.title/summary`・`challengeTutorial.<guid>.text` 行を `localization.csv` に追加し、孤児行を作らない。
- R10 webui-design SKILL.md（§7 / §8.17 / §8.19）を新トークン・新値に追従させる。
- やらないこと: 未配置GUID『木』のマスタ行削除、草花が原木を落とすマスタ修正、`SearchNearestMapObject` が0件のときの LogError 抑止方針の変更、`inInventoryItem` が装備スロットを数えない既存仕様の変更、他チャレンジの文言改訂。

## Global Constraints

- AGENTS.md 全規約（200行/ファイル・10ファイル/ディレクトリ・partial禁止・`Func<>`禁止・try-catch禁止・UniRx・日英2行コメント・`#region Internal` はローカル関数用途のみ・デフォルト引数禁止・`{ get; private set; }` 以外の単純プロパティ禁止）。
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client`（Domain Reload 中は45秒待って再試行）。テストは `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`。
- スキーマ編集は edit-schema スキルの手順（YAML編集 → `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` 変更 → コンパイル）。`optional: true`・`?? Default`・ローダー補完で吸収しない（全JSON一括更新）。Mooresmaster.Model.* は手書き禁止。
- 生成クラス名の規則: switch の case は `PascalCase(when)+PascalCase(key)`（前例: `veinParam`/`minable` → `MinableHandMiningParam`、`tutorialParam`/`mapObjectPin` → `MapObjectPinTutorialParam`）。よって `pinTargetParam` の case は `MapObjectPinTargetParam` / `EarnItemPinTargetParam`、`taskParam`/`equipItem` は `EquipItemTaskParam`。コンパイル後に `Mooresmaster.Model.ChallengesModule` で実名を確認し、違えば本planの名前を実名へ読み替える（規則外の命名にはしない）。
- 作業場所: 本体 worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/tutorial-equip-challenge`（branch `feature/tutorial-equip-challenge-and-pin-fixes`）。マスタは `/Users/sakastudio/hermes-agent/data/repos/moorestech-master-worktrees/tutorial-equip-challenge`（branch `feature/tutorial-equip-challenge-pin-research-desc`、Task M1 で作成）。メインワークツリーでは作業しない。
- zsh では `echo ===` や `--include=*.cs` のような `=` 始まり／グロブ引数が展開エラーになる。区切り文字列はクォートし、grep は `grep -rn ... | grep "\.cs:"` の形にする。
- `../moorestech_master`（本体 worktree から見た相対）は `moorestech-worktrees/moorestech_master` → `moorestech-master-worktrees/pin-*` の共有symlink。Editor 起動時に `ExternalRepositorySyncService` が `.moorestech-external-revisions.json` の commitHash を `checkout --detach` し、逆に `RecordCurrentCommitsIfChanged` が pin 側 HEAD をファイルへ書き戻す（未コミット差分として現れる）。**pin 側に未コミット変更があると checkout はスキップされる**。この書き戻し差分は `git checkout -- .moorestech-external-revisions.json` で捨て、意図した commitHash だけをコミットする。
- 日本語文言の english 訳は localization.csv の既存行（例: `左クリックで拾う` → `Pick Up with Left Click`）の調子に合わせた自然な英語にする。
- コミットメッセージ末尾: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` と `Claude-Session: https://claude.ai/code/session_01H9smkc2WK32HcxxjYtUFgA`。

---

## File Structure

本体repo（`moorestech-worktrees/tutorial-equip-challenge`）:
- Modify: `VanillaSchema/challenges.yml` — `equipItem` task、`mapObjectPin` の `pinTargetType`/`pinTargetParam`
- Modify: `VanillaSchema/research.yml` — `researchNodeDescription` の default 削除
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs` — `EquipItemTaskParam` と `pinTargetParam` 両caseの検証
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/VanillaChallengeType.cs` — `EquipItemTask = "equipItem"`
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/ChallengeFactory.cs` — 登録
- Create: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/EquipItemChallengeTask.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/EquipItemChallengeTaskTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/challenges.json` — equipItem テストチャレンジ追加
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPinTargetResolver.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/MapObjectPinTargetResolverTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPin.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs:177-196`
- Modify: `moorestech_web/webui/src/app/tokens.css`（`--key-hint-color`、`--tutorial-drag-guide-size/duration`、`:where(.keyHintText)`）
- Modify: `.agents/skills/webui-design/SKILL.md`（§7 / §8.17 / §8.19）
- Modify: `.moorestech-external-revisions.json`（最終タスク）

マスタrepo（`moorestech-master-worktrees/tutorial-equip-challenge`）:
- Modify: `tools/tutorial_v3_port/generate_challenges.py`
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/challenges.json`（再生成）
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/research.json`
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`
- Modify: `server/mods/moorestechAlphaMod_3/master/challenges.json`（mapObjectPin 4件）

---

### Task 1: スキーマ移行（equipItem・pinTarget・research default削除）とコンパイル復旧

**Files:**
- Modify: `VanillaSchema/challenges.yml:70-78`（enum）、`:80-126`（taskParam cases）、`:147-158`（mapObjectPin case）
- Modify: `VanillaSchema/research.yml:21-23`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs:44-90, 101-113`
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/VanillaChallengeType.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPinTargetResolver.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/MapObjectPinTargetResolverTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPin.cs:26,62-75,80-84`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs:177-196`

**Interfaces:**
- Consumes: `MasterHolder.MapObjectMaster.Map.MapObjects` (`MapObjectMasterElement[]`, 各要素に `MapObjectGuid: Guid`, `EarnItems: EarnItemsElement[]` で `ItemGuid: Guid`)、`MapObjectGameObject.MapObjectGuid: Guid` / `IsDestroyed` / `GetPosition()`
- Produces:
  - 生成型 `Mooresmaster.Model.ChallengesModule.EquipItemTaskParam { Guid ItemGuid }`
  - 生成型 `MapObjectPinTutorialParam { string PinTargetType; object PinTargetParam; string PinText }`、`MapObjectPinTargetParam { Guid MapObjectGuid }`、`EarnItemPinTargetParam { Guid ItemGuid }`
  - `public static class Client.Game.InGame.Tutorial.MapObjectPinTargetResolver { public static IReadOnlyList<Guid> ResolveMapObjectGuids(MapObjectPinTutorialParam param) }`
  - `public MapObjectGameObject MapObjectGameObjectDatastore.SearchNearestMapObject(IReadOnlyList<Guid> mapObjectGuids, Vector3 position)`
  - `VanillaChallengeType.EquipItemTask == "equipItem"`

- [ ] **Step 1: クライアントの解決テストを書く（失敗確認用）**

`moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/MapObjectPinTargetResolverTest.cs`:

```csharp
using System;
using System.Linq;
using Client.Game.InGame.Tutorial;
using Mooresmaster.Model.ChallengesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.UnitTest.Tutorial
{
    public class MapObjectPinTargetResolverTest
    {
        // forUnitTest map.json: TreeTest/TestMiningRock/TestRubbleRock が item ...0002 を落とし、vanilla:Tree だけが ...0001 を落とす
        // forUnitTest map.json: TreeTest/TestMiningRock/TestRubbleRock drop item ...0002, only vanilla:Tree drops ...0001
        private static readonly Guid TreeTestGuid = Guid.Parse("00000000-0000-1111-0000-000000000001");
        private static readonly Guid MiningRockGuid = Guid.Parse("00000000-0000-2222-0000-000000000001");
        private static readonly Guid RubbleRockGuid = Guid.Parse("00000000-0000-3333-0000-000000000001");
        private static readonly Guid VanillaTreeGuid = Guid.Parse("8c0e1339-be75-4690-99cd-58b5385a17cd");
        private static readonly Guid Item2Guid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void MapObjectTargetResolvesToSingleGuid()
        {
            var param = new MapObjectPinTutorialParam(
                MapObjectPinTutorialParam.PinTargetTypeConst.mapObject,
                new MapObjectPinTargetParam(TreeTestGuid),
                "pin");

            var result = MapObjectPinTargetResolver.ResolveMapObjectGuids(param);

            CollectionAssert.AreEqual(new[] { TreeTestGuid }, result);
        }

        [Test]
        public void EarnItemTargetResolvesToEveryMapObjectDroppingTheItem()
        {
            var param = new MapObjectPinTutorialParam(
                MapObjectPinTutorialParam.PinTargetTypeConst.earnItem,
                new EarnItemPinTargetParam(Item2Guid),
                "pin");

            var result = MapObjectPinTargetResolver.ResolveMapObjectGuids(param);

            CollectionAssert.AreEquivalent(new[] { TreeTestGuid, MiningRockGuid, RubbleRockGuid }, result);
            Assert.IsFalse(result.Contains(VanillaTreeGuid));
        }
    }
}
```

生成型のコンストラクタ引数順（`pinTargetType, pinTargetParam, pinText`）と `PinTargetTypeConst` の有無はコンパイル後に `Library/Bee/.../Mooresmaster` 生成物または IDE 定義ジャンプで確認し、違えば本テストをその実引数に合わせる（前例: `TutorialsElement.TutorialTypeConst.mapObjectPin` があるので、enum key `pinTargetType` にも `PinTargetTypeConst` が生える）。

- [ ] **Step 2: challenges.yml を編集する**

`VanillaSchema/challenges.yml` の `taskCompletionType` enum に `equipItem` を追加し、`taskParam` の cases 末尾（`completeResearch` の後）に追加:

```yaml
          - when: equipItem
            type: object
            properties:
            - key: itemGuid
              type: uuid
              foreignKey:
                schemaId: items
                foreignKeyIdPath: /data/[*]/itemGuid
                displayElementPath: /data/[*]/name
```

`mapObjectPin` の case を次に置換（`mapObjectGuid` 直下プロパティは削除）:

```yaml
              - when: mapObjectPin
                type: object
                properties:
                # ピン先の指定方法。mapObject=GUID直指定、earnItem=そのアイテムを落とす全mapObjectのうち最寄り
                # How the pin target is chosen: mapObject=direct GUID, earnItem=nearest of every mapObject dropping that item
                - key: pinTargetType
                  type: enum
                  options:
                  - mapObject
                  - earnItem
                  default: mapObject
                - key: pinTargetParam
                  switch: ./pinTargetType
                  cases:
                  - when: mapObject
                    type: object
                    properties:
                    - key: mapObjectGuid
                      type: uuid
                      foreignKey:
                        schemaId: map
                        foreignKeyIdPath: /mapObjects/[*]/mapObjectGuid
                        displayElementPath: /mapObjects/[*]/mapObjectName
                  - when: earnItem
                    type: object
                    properties:
                    - key: itemGuid
                      type: uuid
                      foreignKey:
                        schemaId: items
                        foreignKeyIdPath: /data/[*]/itemGuid
                        displayElementPath: /data/[*]/name
                - key: pinText
                  type: string
                  default: pin text
```

- [ ] **Step 3: research.yml の default を削除する**

`VanillaSchema/research.yml:21-23` を:

```yaml
    - key: researchNodeDescription
      type: string
```

- [ ] **Step 4: SourceGenerator をトリガする**

`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` の値を `"2026-08-22-tutorial-equip-pin-target"` に変更。

- [ ] **Step 5: VanillaChallengeType に定数を追加する**

```csharp
        public const string EquipItemTask = "equipItem";
```

- [ ] **Step 6: ChallengeMasterUtil の検証を更新する**

`TaskParamValidation()` の `CompleteResearchTaskParam` case の直後に追加:

```csharp
                            case EquipItemTaskParam equipItem:
                            {
                                var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(equipItem.ItemGuid);
                                if (itemId == null)
                                {
                                    logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid TaskParam.ItemGuid:{equipItem.ItemGuid}\n";
                                }
                                break;
                            }
```

`TutorialValidation()` の `MapObjectPinTutorialParam` case を置換:

```csharp
                                case MapObjectPinTutorialParam mapObjectPin:
                                {
                                    // ピン先はGUID直指定とドロップ品指定の2系統。どちらも参照先の実在だけを検証する
                                    // Two target kinds: direct GUID and drop item; both only verify the referenced master exists
                                    switch (mapObjectPin.PinTargetParam)
                                    {
                                        case MapObjectPinTargetParam byMapObject:
                                            if (MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(byMapObject.MapObjectGuid) == null)
                                            {
                                                logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.MapObjectGuid:{byMapObject.MapObjectGuid}\n";
                                            }
                                            break;
                                        case EarnItemPinTargetParam byEarnItem:
                                            if (MasterHolder.ItemMaster.GetItemIdOrNull(byEarnItem.ItemGuid) == null)
                                            {
                                                logs += $"[ChallengeMaster] Challenge:{challenge.Title} has invalid Tutorial.PinTarget.ItemGuid:{byEarnItem.ItemGuid}\n";
                                            }
                                            break;
                                        default:
                                            logs += $"[ChallengeMaster] Challenge:{challenge.Title} has unvalidated PinTargetParam type:{mapObjectPin.PinTargetParam?.GetType().Name}\n";
                                            break;
                                    }
                                    break;
                                }
```

- [ ] **Step 7: クライアント解決クラスを作る**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPinTargetResolver.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;

namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    ///     mapObjectPin の pinTargetParam をピン候補の mapObjectGuid 集合へ解決する
    ///     Resolves a mapObjectPin's pinTargetParam into the set of candidate mapObjectGuids
    /// </summary>
    public static class MapObjectPinTargetResolver
    {
        public static IReadOnlyList<Guid> ResolveMapObjectGuids(MapObjectPinTutorialParam param)
        {
            return param.PinTargetParam switch
            {
                MapObjectPinTargetParam byMapObject => new[] { byMapObject.MapObjectGuid },
                EarnItemPinTargetParam byEarnItem => ResolveByEarnItem(byEarnItem.ItemGuid),
                _ => throw new InvalidOperationException($"Unknown pinTargetType: {param.PinTargetType}"),
            };
        }

        // そのアイテムを落とす全mapObjectが候補。木の種類が増えてもマスタ側の列挙は不要
        // Every mapObject dropping the item is a candidate, so new tree species need no master enumeration
        private static IReadOnlyList<Guid> ResolveByEarnItem(Guid itemGuid)
        {
            return MasterHolder.MapObjectMaster.Map.MapObjects
                .Where(mapObject => mapObject.EarnItems.Any(earnItem => earnItem.ItemGuid == itemGuid))
                .Select(mapObject => mapObject.MapObjectGuid)
                .ToList();
        }
    }
}
```

- [ ] **Step 8: Datastore の最寄り探索をGUID集合受けにする**

`MapObjectGameObjectDatastore.cs:177-196` を置換:

```csharp
        public MapObjectGameObject SearchNearestMapObject(IReadOnlyList<Guid> mapObjectGuids, Vector3 position)
        {
            MapObjectGameObject nearestMapObject = null;
            var maxMagnitude = float.MaxValue;

            foreach (var mapObject in _allMapObjects.Values)
            {
                // 候補GUIDに含まれ、かつ未破壊のものだけを距離比較する
                // Compare distance only for undestroyed objects whose GUID is in the candidate set
                if (mapObject.IsDestroyed || !mapObjectGuids.Contains(mapObject.MapObjectGuid)) continue;

                var magnitude = (position - mapObject.GetPosition()).magnitude;
                if (maxMagnitude < magnitude) continue;

                nearestMapObject = mapObject;
                maxMagnitude = magnitude;
            }

            return nearestMapObject;
        }
```

- [ ] **Step 9: MapObjectPin を新paramに追従させる**

`MapObjectPin.cs` のフィールド `_currentTutorialParam` の直下に追加し、`NearestPinMapObject` と `ApplyTutorial` を置換:

```csharp
        private MapObjectPinTutorialParam _currentTutorialParam;
        private IReadOnlyList<Guid> _targetMapObjectGuids = Array.Empty<Guid>();
```

```csharp
            void NearestPinMapObject()
            {
                // 候補GUID集合のうち最寄りの未破壊MapObjectへピンする
                // Pin the nearest undestroyed MapObject among the candidate GUIDs
                var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;
                var mapObject = _mapObjectGameObjectDatastore.SearchNearestMapObject(_targetMapObjectGuids, playerPos);

                if (mapObject == null)
                {
                    Debug.LogError($"未破壊のMapObject（pinTargetType={_currentTutorialParam.PinTargetType}）が存在しません");
                    return;
                }

                transform.position = mapObject.GetPosition();
            }
```

```csharp
        public ITutorialView ApplyTutorial(TutorialsElement tutorial)
        {
            _currentTutorialParam = (MapObjectPinTutorialParam)tutorial.TutorialParam;
            _targetMapObjectGuids = MapObjectPinTargetResolver.ResolveMapObjectGuids(_currentTutorialParam);
            _pinTutorialGuid = tutorial.TutorialGuid.ToString("D");
```

`using System;` と `using System.Collections.Generic;` を先頭に追加。

- [ ] **Step 10: コンパイルしてエラーゼロを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0。残エラーがあれば生成型名（`EquipItemTaskParam` / `MapObjectPinTargetParam` / `EarnItemPinTargetParam` / `PinTargetTypeConst`）の実名ずれなので、生成物を見て Step 1/6/7/9 の名前を実名に合わせる。

- [ ] **Step 11: テストを実行する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectPinTargetResolverTest|VeinPinTutorialTest|MasterSourceTextCollectorTest|ChallengeMaster"`
Expected: 全PASS（`MasterSourceTextCollectorTest` は `PinText` 参照のみで影響なし。forUnitTest の challenges.json に mapObjectPin は無いためローダー側も影響なし）。

- [ ] **Step 12: コミットする**

```bash
git add VanillaSchema/challenges.yml VanillaSchema/research.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs moorestech_server/Assets/Scripts/Core.Master/Validator/ChallengeMasterUtil.cs moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/VanillaChallengeType.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPinTargetResolver.cs* moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/MapObjectPinTargetResolverTest.cs* moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/MapObjectPin.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs
git commit -m "feat(schema): challenges に equipItem 完了種別と mapObjectPin の pinTarget 切替を追加し research 説明文の default を削除 (ADR 0029)"
```

（`.meta` は Unity 生成物をそのまま add する。`.moorestech-external-revisions.json` の書き戻し差分は add しない。）

---

### Task 2: サーバー EquipItemChallengeTask（選択中装備スロット判定）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/challenges.json`（Category1 に1件追加）
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/EquipItemChallengeTaskTest.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/EquipItemChallengeTask.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/ChallengeFactory.cs:17`

**Interfaces:**
- Consumes: `VanillaChallengeType.EquipItemTask`、`EquipItemTaskParam.ItemGuid`（Task 1）、`IPlayerInventoryDataStore.GetAllPlayerId()/GetInventoryData(int).EquipmentInventory`、`IEquipmentInventory.GetSelectedItem()/SetItem(int, ItemId, int)/SetSelectedEquipmentIndex(int)`、`IEquipmentInventoryUpdateEvent.Subscribe(UpdateInventoryEvent)/SubscribeSelectedEquipmentIndex(UpdateSelectedEquipmentIndexEvent)`（`PlayerInventoryUpdateEventProperties.PlayerId` / `EquipmentSelectedIndexUpdateEventProperties.PlayerId`）
- Produces: `Game.Challenge.Task.EquipItemChallengeTask : IChallengeTask`（`public static IChallengeTask Create(ChallengeMasterElement)`）

- [ ] **Step 1: テストモッドに equipItem チャレンジを足す**

`forUnitTest/master/challenges.json` の Category1（`03ca4ded-...`）の `challenges` 配列末尾（`研究1を完了する` の次）に追加:

```json
        {
          "challengeGuid": "00000000-0000-0000-4567-000000000102",
          "title": "Test1を装備する",
          "summary": "Test1を装備する",
          "unlockAllPreviousChallengeComplete": true,
          "prevChallengeGuids": [],
          "taskCompletionType": "equipItem",
          "taskParam": {
            "itemGuid": "00000000-0000-0000-1234-000000000001"
          },
          "tutorials": [],
          "startedActions": [],
          "clearedActions": [],
          "displayListParam": {
            "UIPosition": [
              0,
              1200
            ],
            "UIScale": [
              0,
              0,
              0
            ],
            "IconItem": "00000000-0000-0000-1234-000000000001"
          }
        }
```

（直前の `研究1を完了する` エントリと同じキー順・インデント。`研究1を完了する` の閉じ `}` の後にカンマを足してから挿入する。）

- [ ] **Step 2: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/EquipItemChallengeTaskTest.cs`:

```csharp
using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Challenge;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class EquipItemChallengeTaskTest
    {
        private const int PlayerId = 0;
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000102");
        private static readonly Guid Test1ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // 選択中の装備スロットへ入れた瞬間に達成する
        // Putting the item into the selected equipment slot completes the challenge immediately
        [Test]
        public void EquippingIntoSelectedSlotCompletesChallenge()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            var equipment = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;

            equipment.SetSelectedEquipmentIndex(0);
            equipment.SetItem(0, MasterHolder.ItemMaster.GetItemId(Test1ItemGuid), 1);

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        // 非選択スロットに入れただけでは未達成、そのスロットを選択した時点で達成する
        // A non-selected slot does not count; selecting that slot completes it
        [Test]
        public void NonSelectedSlotDoesNotCountUntilSelected()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            var equipment = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;

            equipment.SetSelectedEquipmentIndex(0);
            equipment.SetItem(1, MasterHolder.ItemMaster.GetItemId(Test1ItemGuid), 1);
            GameUpdater.UpdateOneTick();
            Assert.IsFalse(IsCompleted(challengeDatastore));

            equipment.SetSelectedEquipmentIndex(1);
            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        // チャレンジ開始前に装備済みなら初回tickで回収される
        // An item already equipped before the challenge starts is recovered on the first tick
        [Test]
        public void AlreadyEquippedCompletesOnFirstTick()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            var equipment = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).EquipmentInventory;
            equipment.SetSelectedEquipmentIndex(0);
            equipment.SetItem(0, MasterHolder.ItemMaster.GetItemId(Test1ItemGuid), 1);

            challengeDatastore.InitializeCurrentChallenges();
            Assert.IsFalse(IsCompleted(challengeDatastore));
            GameUpdater.UpdateOneTick();

            Assert.IsTrue(IsCompleted(challengeDatastore));
        }

        private static bool IsCompleted(ChallengeDatastore challengeDatastore)
        {
            return challengeDatastore.CurrentChallengeInfo.CompletedChallenges.Any(c => c.ChallengeGuid == ChallengeGuid);
        }
    }
}
```

- [ ] **Step 3: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client` → errors 0（テストはコンパイルは通る）。
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EquipItemChallengeTaskTest"`
Expected: 3本FAIL（`ChallengeFactory` に `equipItem` が未登録のため `KeyNotFoundException`）。

- [ ] **Step 4: EquipItemChallengeTask を実装する**

`moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/EquipItemChallengeTask.cs`:

```csharp
using System;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge.Task
{
    /// <summary>
    ///     選択中の装備スロットに対象アイテムが入った時に達成する
    ///     Completes when the target item sits in the selected equipment slot
    /// </summary>
    public class EquipItemChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();

        private bool _completed;
        private bool _initialCheckDone;

        private readonly ItemId _targetItemId;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new EquipItemChallengeTask(challengeMasterElement);
        }

        private EquipItemChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;

            // マスタのtaskParam型不整合を生成時に検出する（前例: CompleteResearchChallengeTask）
            // Detect a taskParam type mismatch at construction time (precedent: CompleteResearchChallengeTask)
            var equipItemTaskParam = (EquipItemTaskParam)challengeMasterElement.TaskParam;
            _targetItemId = MasterHolder.ItemMaster.GetItemId(equipItemTaskParam.ItemGuid);
            _playerInventoryDataStore = ServerContext.GetService<IPlayerInventoryDataStore>();

            // スロット中身と選択indexは別々に変わるため両方を購読する
            // Slot contents and the selected index change independently, so subscribe to both
            var equipmentUpdateEvent = ServerContext.GetService<IEquipmentInventoryUpdateEvent>();
            equipmentUpdateEvent.Subscribe(OnEquipmentSlotUpdated);
            equipmentUpdateEvent.SubscribeSelectedEquipmentIndex(OnSelectedEquipmentIndexUpdated);
        }

        private void OnEquipmentSlotUpdated(PlayerInventoryUpdateEventProperties properties)
        {
            CheckEquipped(properties.PlayerId);
        }

        private void OnSelectedEquipmentIndexUpdated(EquipmentSelectedIndexUpdateEventProperties properties)
        {
            CheckEquipped(properties.PlayerId);
        }

        public void ManualUpdate()
        {
            // チャレンジ開始前から装備済みの取りこぼしを初回tickだけ照会する
            // Query once on the first tick to recover an item equipped before this challenge started
            if (_completed || _initialCheckDone) return;
            _initialCheckDone = true;

            foreach (var playerId in _playerInventoryDataStore.GetAllPlayerId())
            {
                CheckEquipped(playerId);
            }
        }

        private void CheckEquipped(int playerId)
        {
            if (_completed) return;

            var selectedItem = _playerInventoryDataStore.GetInventoryData(playerId).EquipmentInventory.GetSelectedItem();
            if (selectedItem.Id != _targetItemId) return;

            _completed = true;
            _onChallengeComplete.OnNext(this);
        }
    }
}
```

`Game.Challenge.asmdef` が `Game.PlayerInventory.Interface` を参照していることを確認する（`InInventoryItemChallengeTask` が `IPlayerInventoryDataStore` を使っているので既に参照済み）。`IEquipmentInventoryUpdateEvent` は `Game.PlayerInventory.Interface.Event` 名前空間で同asmdef内。

- [ ] **Step 5: Factory に登録する**

`ChallengeFactory.cs` のコンストラクタ末尾に追加:

```csharp
            _taskCreators.Add(VanillaChallengeType.EquipItemTask,EquipItemChallengeTask.Create);
```

- [ ] **Step 6: コンパイルしてテストを通す**

Run: `uloop compile --project-path ./moorestech_client` → errors 0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EquipItemChallengeTaskTest|ChallengeDatastore|Challenge.*Test"`
Expected: 全PASS（既存チャレンジ系テストが forUnitTest の新チャレンジで崩れていないこと。`Category1` の件数を数えるテストがあれば 6→7 へ期待値を更新する）。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/EquipItemChallengeTask.cs* moorestech_server/Assets/Scripts/Game.Challenge/ChallengeTask/Factory/ChallengeFactory.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/EquipItemChallengeTaskTest.cs* moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/challenges.json
git commit -m "feat(challenge): 選択中装備スロットで達成する equipItem チャレンジタスクを追加 (ADR 0029)"
```

---

### Task 3: webui トークン（キーヒント赤字・ドラッグ矢印 56px/3200ms）と webui-design 追従

**Files:**
- Modify: `moorestech_web/webui/src/app/tokens.css:57-63`（`--key-hint-*` 群）、`:228-231`（drag guide）、`:347-361`（`:where(.keyHintText)`）
- Modify: `.agents/skills/webui-design/SKILL.md`（§7 キー操作ヒント行、§8.17 ドラッグガイド、§8.19 keyControl HUD）

**Interfaces:**
- Consumes: 既存トークン `--text-insufficient`（`tokens.css:93`）
- Produces: `--key-hint-color`

- [ ] **Step 1: tokens.css を編集する**

`--key-hint-*` 群の `--key-hint-text-shadow` 行の直後に追加:

```css
  /* キー操作ヒントの文字色。白文字では世界背景に埋もれるため共有の赤で描く（ユーザー裁定 2026-08-22） */
  /* Key-hint text color; white sinks into the world, so use the shared red (user ruling 2026-08-22) */
  --key-hint-color: var(--text-insufficient);
```

drag guide の2値を置換:

```css
  --tutorial-drag-guide-size: 56px;
  --tutorial-drag-guide-duration: 3200ms;
```

同コメントを「寸法は旧28pxの2倍・周期は旧1600msの2倍（ユーザー裁定 2026-08-22）」の1行ずつ（日英）に改める。

`:where(.keyHintText)` と `:where(.keyHintText) kbd` の `color: var(--text-high-contrast);` を両方 `color: var(--key-hint-color);` にする。

- [ ] **Step 2: vitest と lint を実行する**

Run: `cd moorestech_web/webui && pnpm test && pnpm lint`（`pnpm` が無ければ `../node/<platform>/pnpm`。`WebUiPaths.PnpmBinary` 参照）
Expected: PASS（色・寸法を断言するテストは無い。`TutorialOverlay.test.ts` は表示有無のみ）。

- [ ] **Step 3: webui-design SKILL.md を更新する**

- §7 のキー操作ヒント行末尾に「文字色は `--key-hint-color`（= `--text-insufficient` の赤。ユーザー裁定 2026-08-22『キーヒント全部を赤文字に』）。白には戻さない。」を追記。
- §8.17 ドラッグガイドの寸法・周期の記述に「現在値 56px / 3200ms（ユーザー裁定 2026-08-22『速度半分・大きさ2倍』）」を追記。
- §8.19 の「文字様式は…`keyHintText` クラス（§7）」の直後に「色も §7 の `--key-hint-color` に従い、HUD専用の色例外は作らない」を追記。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_web/webui/src/app/tokens.css .agents/skills/webui-design/SKILL.md
git commit -m "feat(webui): キー操作ヒントを赤字にしチュートリアルのドラッグ矢印を56px/3200msへ (ADR 0029)"
```

---

### Task M1: マスタ worktree と generate_challenges.py（装備チャレンジ2件・木ピンの earnItem 化）

**Files（マスタrepo）:**
- Modify: `tools/tutorial_v3_port/generate_challenges.py:44`（`pin` ヘルパ）、`:62-66`（CHALLENGES 表）、`:186-200`（task 分岐）
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/challenges.json`（再生成）

**Interfaces:**
- Produces: 新チャレンジ key `石器を装備する` / `石の斧を装備する`（GUIDは `guid_for(key)` で安定導出）、tutorials は `tutorial_guid_for(key, slot)`

- [ ] **Step 1: マスタ worktree を作る**

```bash
git -C /Users/sakastudio/hermes-agent/data/repos/moorestech_master fetch -q origin
git -C /Users/sakastudio/hermes-agent/data/repos/moorestech_master worktree add -b feature/tutorial-equip-challenge-pin-research-desc /Users/sakastudio/hermes-agent/data/repos/moorestech-master-worktrees/tutorial-equip-challenge origin/master
```

以降 Task M* のコマンドは `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-master-worktrees/tutorial-equip-challenge` で実行する。

- [ ] **Step 2: ヘルパを新paramへ更新する**

`generate_challenges.py:44` の `pin` を置換し、`earn_pin` を追加:

```python
# ピン先はGUID直指定（mapObject）とドロップ品指定（earnItem）の2系統（ADR 0029）
# Pin targets come in two kinds: direct GUID (mapObject) and drop item (earnItem) (ADR 0029)
def pin(name, text): return ('mapObjectPin', {'pinTargetType': 'mapObject', 'pinTargetParam': {'mapObjectGuid': map_objects[name]}, 'pinText': text})
def earn_pin(item_name, text): return ('mapObjectPin', {'pinTargetType': 'earnItem', 'pinTargetParam': {'itemGuid': items[item_name]}, 'pinText': text})
```

- [ ] **Step 3: CHALLENGES 表を更新する（既存keyは不変・新規2行追加・木を伐採の tutorials/summary 変更）**

`石器を作る` の直後に挿入:

```python
    ('石器を装備する', '石器を装備する', 'インベントリを開き、石器を選択中の装備スロットへドラッグして装備しよう', 'equip', '石器', None,
     [key('GameScreen', 'Tab', 'インベントリを開いて石器を装備'), equip_drag('石器', '装備スロットへドラッグ')], '石器'),
```

`木を伐採して原木を入手する` 行を置換:

```python
    ('木を伐採して原木を入手する', '木を伐採して原木を入手する', '装備した石器で木を伐採し、原木を3個集めよう', 'item', '原木', 3,
     [earn_pin('原木', '石器で木を伐採')], '原木'),
```

`石の斧を作る` の直後に挿入:

```python
    ('石の斧を装備する', '石の斧を装備する', 'インベントリを開き、石の斧を選択中の装備スロットへドラッグして装備しよう', 'equip', '石の斧', None,
     [key('GameScreen', 'Tab', 'インベントリを開いて石の斧を装備'), equip_drag('石の斧', '装備スロットへドラッグ')], '石の斧'),
```

表ヘッダコメントの task 列挙を `'equip'=equipItem` を含めて更新する。

- [ ] **Step 4: task 分岐と到達可能性検査に equip を足す**

`:186-200` の分岐に `craft` の次として追加:

```python
    elif task == 'equip':
        c['taskCompletionType'] = 'equipItem'
        c['taskParam'] = {'itemGuid': items[target]}
```

到達可能性検査（`for _, title, _, task, target, _, _, _ in CHALLENGES:`）は `equip` も `items[target]` の獲得手段を検査する既定分岐に落ちるので追加不要（石器・石の斧はどちらもクラフト結果）。

- [ ] **Step 5: 再生成して差分を検査する**

Run: `python3 tools/tutorial_v3_port/generate_challenges.py`
Expected: `OK: 26 challenges`

Run:
```bash
python3 - <<'EOF'
import json, subprocess
new = json.load(open('server_v8/mods/moorestechAlphaMod_8/master/challenges.json'))['data'][0]['challenges']
old = json.loads(subprocess.check_output(['git', 'show', 'origin/master:server_v8/mods/moorestechAlphaMod_8/master/challenges.json']))['data'][0]['challenges']
old_guids = {c['challengeGuid'] for c in old}
new_guids = {c['challengeGuid'] for c in new}
assert old_guids <= new_guids, old_guids - new_guids
print('added:', [c['title'] for c in new if c['challengeGuid'] not in old_guids])
titles = [c['title'] for c in new]
assert titles.index('石器を作る') + 1 == titles.index('石器を装備する') == titles.index('木を伐採して原木を入手する') - 1
assert titles.index('石の斧を作る') + 1 == titles.index('石の斧を装備する') == titles.index('原始研究3を完了する') - 1
for i in range(1, len(new)): assert new[i]['prevChallengeGuids'] == [new[i-1]['challengeGuid']]
for c in new:
    for t in c['tutorials']:
        if t['tutorialType'] == 'mapObjectPin': print(c['title'], t['tutorialParam'])
EOF
```
Expected: 既存GUID消失なし、added が2件、順序・直列が成立、mapObjectPin 2件が `{'pinTargetType': 'mapObject', ...小石}` と `{'pinTargetType': 'earnItem', ...原木}`。

- [ ] **Step 6: コミットする**

```bash
git add tools/tutorial_v3_port/generate_challenges.py server_v8/mods/moorestechAlphaMod_8/master/challenges.json
git commit -m "feat(tutorial): 石器/石の斧の装備チャレンジを追加し木ピンをドロップ品指定にする (moorestech ADR 0029)"
```

---

### Task M2: mod_3 の mapObjectPin を新paramへ移行する

**Files（マスタrepo）:**
- Modify: `server/mods/moorestechAlphaMod_3/master/challenges.json`（mapObjectPin 4件）

- [ ] **Step 1: スクリプトで一括変換する**

```bash
python3 - <<'EOF'
import json
p = 'server/mods/moorestechAlphaMod_3/master/challenges.json'
d = json.load(open(p))
n = 0
def walk(o):
    global n
    if isinstance(o, dict):
        if o.get('tutorialType') == 'mapObjectPin' and 'mapObjectGuid' in o.get('tutorialParam', {}):
            tp = o['tutorialParam']
            o['tutorialParam'] = {'pinTargetType': 'mapObject', 'pinTargetParam': {'mapObjectGuid': tp['mapObjectGuid']}, 'pinText': tp['pinText']}
            n += 1
        for v in o.values(): walk(v)
    elif isinstance(o, list):
        for x in o: walk(x)
walk(d)
json.dump(d, open(p, 'w'), ensure_ascii=False, indent=2); open(p, 'a').write('\n')
print('migrated', n)
EOF
```
Expected: `migrated 4`。`git diff --stat` が当該ファイルのみで、インデント幅・末尾改行が元と同じ（違えば `indent` を元ファイルに合わせて再実行）。

- [ ] **Step 2: コミットする**

```bash
git add server/mods/moorestechAlphaMod_3/master/challenges.json
git commit -m "data(mod_3): mapObjectPin を pinTargetType/pinTargetParam 形式へ移行"
```

---

### Task M3: research.json の説明文を全ノード差し替える

**Files（マスタrepo）:**
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/research.json`（全ノードの `researchNodeDescription`）

- [ ] **Step 1: 説明文表を適用する**

下表は「解放内容」を軸にした1行（日本語）。`clearedActions` 空のノードは準備中文言。

```bash
python3 - <<'EOF'
import json
p = 'server_v8/mods/moorestechAlphaMod_8/master/research.json'
d = json.load(open(p))
desc = {
 '原始研究1': '石のレシピを解放し、砕いた石材の加工へ進む準備を整える',
 '原始研究2': '砕いた石材を使った石の斧のレシピを解放する',
 '原始研究3': '鉱脈から資源を掘り出す風力掘削機を解放する',
 '原始研究4': '石窯と木のチェストを解放し、粘土からレンガを焼けるようにする',
 '原始研究4.5': '青銅の鉱石を粉砕して青銅インゴットへ精錬するレシピを解放する',
 '原始研究5': '原始的な粉砕機と燃料式風車を解放し、鉱石の粉を量産できるようにする',
 '原始研究6': '木の歯車と歯車ベルトコンベアを解放し、動力で搬送を始める',
 '原始研究7': '原始的な加工機を解放し、板・ロッド・ワイヤーを機械で作れるようにする',
 '原始研究8': '原始的な採掘機を解放し、人力に頼らない採掘を始める',
 '原始ロジスティクス改善': '木のコンベアチェストと歯車コンベア分岐機を解放する',
 '建築土台': '基本土台を解放し、平らな建築面を作れるようにする',
 '燃料式風車の作成': '燃料で回る燃料式風車を解放する',
 '新しい燃料': '原木から木炭を作るレシピを解放する',
 '軸の変更': '木の縦シャフトとシャフトボックスを解放し、動力を縦と横に曲げる',
 '木材の組み立て': '原始的な組立機を解放し、合板やフレームを組み立てられるようにする',
 '鉄の時代': '鉄鉱石の粉砕と鉄インゴットの精錬レシピを解放する',
 '鉄の加工': '鉄のロッド・鉄板・鉄のワイヤー・鉄のフレームのレシピを解放する',
 '鉄の歯車': '鉄の歯車とシャフト類を解放し、より大きな動力を伝える',
 '蒸気機関': '蒸気機関とボイラー、鉄のパイプ、歯車ポンプを解放する',
 '新しい化石燃料': '石炭のレシピと鉄の採掘機、ふいご付き精錬炉を解放する',
 '新しいベルトコンベア': '鉄の歯車ベルトコンベア一式を解放する',
 '鉄道の時代': '蒸気機関車と駅・貨物プラットフォーム・レール橋脚を解放する',
 '鉄のチェーンポール': '歯車チェーンポールとコンパクト歯車チェーンポールを解放する',
 '新しいチェスト': '鉄のコンベアチェストとミニコンベアチェストを解放する',
 '銅の採掘': '銅の鉱石の粉砕から銅インゴット・銅のワイヤー・銅板までのレシピを解放する',
 '酸素の精製': '酸素発生装置とフィルター分岐器を解放し、酸素タンクを作れるようにする',
 '加工装置': '回転発電機・電柱・電気汎用工作装置を解放し、電子回路とモーターを作れるようにする',
 '採掘機の電化': '電気採掘機を解放する',
 '新しい電気ベルトコンベア': '電動のベルトコンベア一式を解放する',
 '石油の時代': '油井・石油蒸留機・化学プラントを解放し、プラスチックやゴムを作れるようにする',
 '新しい発電': 'ガソリンエンジン発電機を解放する',
 '高速ベルトコンベア': '高速ベルトコンベア一式を解放する',
 'スマート分岐器': '（準備中）今後の研究で解放内容が追加される',
 '半導体製造': 'クリーンルームと半導体製造装置を解放し、ICチップを作れるようにする',
 '機械オーバークロック': '（準備中）今後の研究で解放内容が追加される',
 '反物質爆弾': '（準備中）今後の研究で解放内容が追加される',
 '核融合炉': '（準備中）今後の研究で解放内容が追加される',
 'ロケット': '（準備中）今後の研究で解放内容が追加される',
 '金属超精密加工': '金属3Dプリンタのレシピを解放する',
 'ディーゼル機関車': 'ディーゼル機関車を解放する',
 '歯車システムの電化': '電力で歯車を回す回転生成機を解放する',
 '加工の電化v2': '電気炉と電気粉砕機を解放する',
 '長距離電力伝送': '高圧電柱を解放し、遠くへ電力を送れるようにする',
 '広範囲電力伝送': '広範囲電柱を解放し、広い範囲へ電力を配れるようにする',
 '液体タンク': '液体タンクと液体プラットフォームを解放する',
 '新しい配管': '鋼鉄のパイプを解放する',
 '残油処理技術': 'アスファルトのレシピとアスファルト土台を解放する',
}
missing = [n['researchNodeName'] for n in d['data'] if n['researchNodeName'] not in desc]
assert not missing, missing
for n in d['data']:
    n['researchNodeDescription'] = desc[n['researchNodeName']]
json.dump(d, open(p, 'w'), ensure_ascii=False, indent=2); open(p, 'a').write('\n')
print('updated', len(d['data']))
EOF
grep -c "New Research Description" server_v8/mods/moorestechAlphaMod_8/master/research.json
```
Expected: `updated 47`（実ノード数）、grep は `0`。research.json に表に無い名前があれば assert で止まるので表に追記する（内容は当該ノードの `clearedActions` から「解放対象の名前」を読んで1行で書く）。`git diff` でインデント・キー順が元と同じことを確認する（`json.dump(indent=2)` が元の体裁と違えば、元体裁に合わせて `indent`/`separators` を調整して再実行）。

- [ ] **Step 2: コミットする**

```bash
git add server_v8/mods/moorestechAlphaMod_8/master/research.json
git commit -m "data(research): 全ノードの説明文を解放内容ベースの1行に差し替える"
```

---

### Task M4: localization.csv（チャレンジ・チュートリアル・研究説明の行）

**Files（マスタrepo）:**
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`

- [ ] **Step 1: 行を追加・更新する**

追加/更新対象のキーと値（english は自然な英語）:
- `challenge.<石器を装備するguid>.title` = `石器を装備する` / `Equip the Stone Tool`
- `challenge.<石器を装備するguid>.summary` = `インベントリを開き、石器を選択中の装備スロットへドラッグして装備しよう` / `Open the inventory and drag the Stone Tool onto the selected equipment slot`
- `challenge.<石の斧を装備するguid>.title` = `石の斧を装備する` / `Equip the Stone Axe`
- `challenge.<石の斧を装備するguid>.summary` = `インベントリを開き、石の斧を選択中の装備スロットへドラッグして装備しよう` / `Open the inventory and drag the Stone Axe onto the selected equipment slot`
- `challenge.fb529cac-5358-57fa-bd0a-08f3a6bb43c4.summary`（木を伐採）= `装備した石器で木を伐採し、原木を3個集めよう` / `Chop trees with the equipped Stone Tool and collect 3 Logs`
- `challengeTutorial.<石器を装備する slot0 guid>.text` = `インベントリを開いて石器を装備` / `Open the inventory and equip the Stone Tool`
- `challengeTutorial.<石の斧を装備する slot0 guid>.text` = `インベントリを開いて石の斧を装備` / `Open the inventory and equip the Stone Axe`
- 木を伐採の旧 keyControl 行（`challengeTutorial.<fb529cac slot1 guid>.text`）は、再生成後の challenges.json にその tutorialGuid が無くなるので削除（uiDragGuide には文言が無いので行は元々無い）
- `research.<guid>.description` 全行 = Task M3 の日本語 / english は同内容の英訳（例: 原始研究2 `Unlocks the Stone Axe recipe that uses Crushed Stone`）

手順は次のスクリプトで行う（`csv` モジュールは CRLF 出力・引用符脱落の落とし穴があるため、行単位のテキスト操作で既存行を保全する）:

```bash
python3 - <<'EOF'
import json, csv, io
CSV = 'server_v8/mods/moorestechAlphaMod_8/localization/localization.csv'
ch = json.load(open('server_v8/mods/moorestechAlphaMod_8/master/challenges.json'))['data'][0]['challenges']
rs = json.load(open('server_v8/mods/moorestechAlphaMod_8/master/research.json'))['data']
by_title = {c['title']: c for c in ch}
def ch_guid(t): return by_title[t]['challengeGuid']
def tut_guid(t, i): return by_title[t]['tutorials'][i]['tutorialGuid']
EN_RESEARCH = {  # research name -> english description (1 line each; 1:1 with Task M3)
 '原始研究1': 'Unlocks the Stone recipe and prepares you to process crushed stone',
 '原始研究2': 'Unlocks the Stone Axe recipe that uses Crushed Stone',
 '原始研究3': 'Unlocks the Wind Driller that digs resources out of veins',
 '原始研究4': 'Unlocks the Stone Kiln and Wooden Chest so clay can be fired into bricks',
 '原始研究4.5': 'Unlocks crushing Bronze Ore and smelting it into Bronze Ingots',
 '原始研究5': 'Unlocks the Primitive Crusher and Fuel Windmill for mass-producing ore powder',
 '原始研究6': 'Unlocks Wooden Gears and gear belt conveyors to start powered transport',
 '原始研究7': 'Unlocks the Primitive Processor so plates, rods and wires can be machined',
 '原始研究8': 'Unlocks the Primitive Miner to mine without manual labor',
 '原始ロジスティクス改善': 'Unlocks the Wooden Conveyor Chest and Gear Conveyor Splitter',
 '建築土台': 'Unlocks the Basic Foundation for flat building surfaces',
 '燃料式風車の作成': 'Unlocks the fuel-burning Fuel Windmill',
 '新しい燃料': 'Unlocks the recipe that turns Logs into Charcoal',
 '軸の変更': 'Unlocks the Wooden Vertical Shaft and Shaft Box to route power vertically and sideways',
 '木材の組み立て': 'Unlocks the Primitive Assembler for plywood and frames',
 '鉄の時代': 'Unlocks crushing Iron Ore and smelting Iron Ingots',
 '鉄の加工': 'Unlocks recipes for Iron Rods, Iron Plates, Iron Wire and Iron Frames',
 '鉄の歯車': 'Unlocks Iron Gears and iron shafts to carry more power',
 '蒸気機関': 'Unlocks the Steam Engine, Boiler, Iron Pipe and Gear Pump',
 '新しい化石燃料': 'Unlocks the Coal recipe, the Iron Miner and the Bellows Smelter',
 '新しいベルトコンベア': 'Unlocks the full set of iron gear belt conveyors',
 '鉄道の時代': 'Unlocks the steam locomotive, stations, cargo platforms and rail piers',
 '鉄のチェーンポール': 'Unlocks the Gear Chain Pole and Compact Gear Chain Pole',
 '新しいチェスト': 'Unlocks the Iron Conveyor Chest and Mini Conveyor Chest',
 '銅の採掘': 'Unlocks crushing Copper Ore through to Copper Ingots, Copper Wire and Copper Plates',
 '酸素の精製': 'Unlocks the Oxygen Generator and Filter Splitter so Oxygen Tanks can be made',
 '加工装置': 'Unlocks the Rotary Generator, Utility Pole and Electric Workbench for circuits and motors',
 '採掘機の電化': 'Unlocks the Electric Miner',
 '新しい電気ベルトコンベア': 'Unlocks the full set of electric belt conveyors',
 '石油の時代': 'Unlocks the Oil Well, Oil Distiller and Chemical Plant for plastic and rubber',
 '新しい発電': 'Unlocks the Gasoline Engine Generator',
 '高速ベルトコンベア': 'Unlocks the full set of high-speed belt conveyors',
 'スマート分岐器': '(Coming soon) Unlock contents will be added in a future research update',
 '半導体製造': 'Unlocks the clean room and semiconductor equipment for making IC chips',
 '機械オーバークロック': '(Coming soon) Unlock contents will be added in a future research update',
 '反物質爆弾': '(Coming soon) Unlock contents will be added in a future research update',
 '核融合炉': '(Coming soon) Unlock contents will be added in a future research update',
 'ロケット': '(Coming soon) Unlock contents will be added in a future research update',
 '金属超精密加工': 'Unlocks the Metal 3D Printer recipe',
 'ディーゼル機関車': 'Unlocks the diesel locomotive',
 '歯車システムの電化': 'Unlocks the Rotation Generator that spins gears with electricity',
 '加工の電化v2': 'Unlocks the Electric Furnace and Electric Crusher',
 '長距離電力伝送': 'Unlocks the High-Voltage Pole for sending power over long distances',
 '広範囲電力伝送': 'Unlocks the Wide-Area Pole for distributing power across a wide area',
 '液体タンク': 'Unlocks the Fluid Tank and Fluid Platform',
 '新しい配管': 'Unlocks the Steel Pipe',
 '残油処理技術': 'Unlocks the Asphalt recipe and Asphalt Foundation',
}
updates = {
 f'challenge.{ch_guid("石器を装備する")}.title': ('石器を装備する', 'Equip the Stone Tool', '石器を装備する'),
 f'challenge.{ch_guid("石器を装備する")}.summary': (by_title['石器を装備する']['summary'], 'Open the inventory and drag the Stone Tool onto the selected equipment slot', by_title['石器を装備する']['summary']),
 f'challenge.{ch_guid("石の斧を装備する")}.title': ('石の斧を装備する', 'Equip the Stone Axe', '石の斧を装備する'),
 f'challenge.{ch_guid("石の斧を装備する")}.summary': (by_title['石の斧を装備する']['summary'], 'Open the inventory and drag the Stone Axe onto the selected equipment slot', by_title['石の斧を装備する']['summary']),
 f'challenge.{ch_guid("木を伐採して原木を入手する")}.summary': (by_title['木を伐採して原木を入手する']['summary'], 'Chop trees with the equipped Stone Tool and collect 3 Logs', by_title['木を伐採して原木を入手する']['summary']),
 f'challengeTutorial.{tut_guid("石器を装備する", 0)}.text': ('インベントリを開いて石器を装備', 'Open the inventory and equip the Stone Tool', 'インベントリを開いて石器を装備'),
 f'challengeTutorial.{tut_guid("石の斧を装備する", 0)}.text': ('インベントリを開いて石の斧を装備', 'Open the inventory and equip the Stone Axe', 'インベントリを開いて石の斧を装備'),
}
for r in rs:
    ja = r['researchNodeDescription']; en = EN_RESEARCH[r['researchNodeName']]
    updates[f'research.{r["researchNodeGuid"]}.description'] = (ja, en, ja)
valid_tut = {t['tutorialGuid'] for c in ch for t in c['tutorials'] if t['tutorialType'] != 'uiDragGuide'}
def fmt(v): return '"' + v.replace('"', '""') + '"' if any(x in v for x in ',"\n') else v
lines = open(CSV, encoding='utf-8').read().split('\n')
out = []; seen = set()
for line in lines:
    key = line.split(',', 1)[0]
    if key.startswith('challengeTutorial.') and key.split('.')[1] not in valid_tut:
        continue  # 孤児行（再生成で消えたtutorialGuid）
    if key in updates:
        ja, en, ja2 = updates[key]; out.append(','.join([key, fmt(ja), fmt(en), fmt(ja2)])); seen.add(key)
    else:
        out.append(line)
# 新規行は末尾の空行の前に追加
tail = out.pop() if out and out[-1] == '' else None
for key, (ja, en, ja2) in updates.items():
    if key not in seen: out.append(','.join([key, fmt(ja), fmt(en), fmt(ja2)]))
if tail is not None: out.append(tail)
open(CSV, 'w', encoding='utf-8', newline='').write('\n'.join(out))
# 整合確認
rows = list(csv.reader(io.StringIO(open(CSV, encoding='utf-8').read())))
keys = {r[0] for r in rows[1:]}
for c in ch:
    assert f'challenge.{c["challengeGuid"]}.title' in keys and f'challenge.{c["challengeGuid"]}.summary' in keys, c['title']
    for t in c['tutorials']:
        if t['tutorialType'] != 'uiDragGuide': assert f'challengeTutorial.{t["tutorialGuid"]}.text' in keys, (c['title'], t['tutorialType'])
for r in rs: assert f'research.{r["researchNodeGuid"]}.description' in keys
assert not any('New Research Description' in ','.join(r) for r in rows)
print('ok rows', len(rows))
EOF
```

`EN_RESEARCH` に無い研究名があれば `KeyError` で止まる（research.json に新ノードが増えた場合）。その場合は Task M3 の表と同じ要領で日英とも1行を追記してから再実行する。

- [ ] **Step 2: 体裁を検査する**

Run: `git diff --stat; file server_v8/mods/moorestechAlphaMod_8/localization/localization.csv; grep -c $'\r' server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`
Expected: 変更ファイルは csv のみ、`with CRLF` にならず、CR 数 0（元が LF）。既存の引用符付き行が引用を失っていないことを `git diff` で目視。

- [ ] **Step 3: コミットする**

```bash
git add server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git commit -m "data(localization): 装備チャレンジ/木ピン文言と研究説明文の行を更新"
```

---

### Task M5: マスタの機械検証・push・PR

- [ ] **Step 1: JSON/CSV の機械検証**

```bash
for f in server_v8/mods/moorestechAlphaMod_8/master/challenges.json server_v8/mods/moorestechAlphaMod_8/master/research.json server/mods/moorestechAlphaMod_3/master/challenges.json; do python3 -m json.tool "$f" > /dev/null && echo "ok $f"; done
grep -rn $'​' server_v8/mods/moorestechAlphaMod_8/master/challenges.json server_v8/mods/moorestechAlphaMod_8/localization/localization.csv; echo "zero-width check exit=$?"
grep -rc '"mapObjectGuid"' server_v8/mods/moorestechAlphaMod_8/master/challenges.json server/mods/moorestechAlphaMod_3/master/challenges.json
```
Expected: 3ファイル ok、zero-width は 1（不一致）、`mapObjectGuid` は v8 1件・mod_3 4件（すべて `pinTargetParam` 配下）。

- [ ] **Step 2: push と PR 作成（マージはしない）**

```bash
git push -u origin feature/tutorial-equip-challenge-pin-research-desc
gh pr create --repo moorestech/moorestech_master --title "feat(tutorial): 石器/石の斧の装備チャレンジ・木ピンのドロップ品指定・研究説明文の全差し替え (moorestech ADR 0029)" --body "$(cat <<'EOF'
## Summary
- 「石器を装備する」「石の斧を装備する」を独立チャレンジとして追加（taskCompletionType equipItem。本体 ADR 0029）
- mapObjectPin を pinTargetType/pinTargetParam 形式へ移行し、木を伐採は earnItem=原木 で最寄りの木へピン
- research.json 全ノードの説明文を解放内容ベースの1行に差し替え、localization.csv を追従
- mod_3 の mapObjectPin 4件を新形式へ移行

## 依存
本体 PR（schema 変更）と同一マージ単位。先にマージすると旧スキーマでロードできない。

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01H9smkc2WK32HcxxjYtUFgA
EOF
)"
```

---

### Task 4: 本体ピン更新とマスタ連動テスト・PlayMode 実機確認

**Files:**
- Modify: `.moorestech-external-revisions.json`（`moorestech_master.commitHash`）

- [ ] **Step 1: ピンをマスタブランチの HEAD に向ける**

```bash
MASTER_HEAD=$(git -C /Users/sakastudio/hermes-agent/data/repos/moorestech-master-worktrees/tutorial-equip-challenge rev-parse HEAD)
git checkout -- .moorestech-external-revisions.json
python3 - "$MASTER_HEAD" <<'EOF'
import json, sys
p = '.moorestech-external-revisions.json'
d = json.load(open(p))
for r in d['repositories']:
    if r['key'] == 'moorestech_master': r['commitHash'] = sys.argv[1]
json.dump(d, open(p, 'w'), indent=4); open(p, 'a').write('\n')
EOF
git diff .moorestech-external-revisions.json
```

`../moorestech_master`（共有symlink先の pin worktree）に未コミット変更が無いことを `git -C ../moorestech_master status --short` で確認し、あれば `git -C ../moorestech_master stash` で退避する（Editor の自動 checkout はローカル変更があるとスキップされる）。Editor を `uloop` で再フォーカス/リロードし、`uloop get-logs --project-path ./moorestech_client --log-type Log --search "External repository checked out"` で新コミットへ checkout されたことを確認する（出なければ `git -C ../moorestech_master fetch && git -C ../moorestech_master checkout --detach $MASTER_HEAD` を手で実行）。

- [ ] **Step 2: マスタ連動テストを実行する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "TutorialAnchorContractTest|MasterSourceTextCollectorTest|ChallengeMaster|LocalizeContent"`
Expected: 全PASS（`AllModAnchorIdsResolveToWebVocabulary` が Ignore でなく実行され、新チャレンジの `inventory.item-<guid>` / `equipment.selected-slot` アンカーが語彙に解決する。`MasterSourceTextCollectorTest` が `research.<guid>.description` の Source 収集を検査する場合、"New Research Description" 前提の期待値があれば新文言に更新する）。

- [ ] **Step 3: unityプレイ録画テスト（unity-playmode-recorded-playtest スキル）**

スキルの DSL（`.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh`）で次を通す。新シナリオは `references/write-scenario.md`（Driver API リファレンス）に従い `scenarios/` 配下へ `tutorial-equip-challenge.cs` として書き、インベントリ内のアイテム→装備スロットのドラッグは `references/input-injection.md` の InputSystem 注入で行う。結果は `result.json` と `uloop screenshot` で判定する:
1. 開始直後、小石ピンが小石に刺さる（従来どおり）
2. 小石3個拾い→石器クラフト後に「石器を装備する」が現在目標になり、`[Tab] インベントリを開いて石器を装備` が赤字で下中央に出る（`uloop screenshot` Game View で文字色が赤であること）
3. Tab→インベントリで石器スロット→選択中装備枠への矢印が旧比で大きく（56px）ゆっくり（3.2秒周期）ループする
4. 石器を選択中装備スロットへドラッグした瞬間に「石器を装備する」が達成され「木を伐採して原木を入手する」へ進む
5. 木を伐採のピンが最寄りの木（BirchTree/Fir 等）に刺さり、Console に `未破壊のMapObject` の LogError が出ない（`uloop get-logs --log-type Error --search "未破壊のMapObject"` が0件）
6. 研究画面（R）で任意ノードの説明文が "New Research Description" でない
7. 石の斧クラフト後に「石の斧を装備する」が現在目標になり、装備で達成→原始研究3へ進む

各確認のスクリーンショットを PR 本文に添付する。

- [ ] **Step 4: コミットする**

```bash
git add .moorestech-external-revisions.json
git commit -m "chore: moorestech_master ピンを ADR 0029 マスタブランチへ更新"
```

（マスタPRマージ後、本コミットの commitHash を**マージコミット**へ差し替える追いコミットを入れる。先にマージすると CI の master data checkout が失敗する。）

---

### Task 5: 最終レビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**（自動実行・ゴール文言による省略不可）。指摘の機械的修正は適用し、設計判断は AskUserQuestion でまとめて仰ぐ。
- [ ] **Step 2: 本体 PR を作成する**（pr-create スキル）。本文にマスタPR番号・同一マージ単位の注意・スクリーンショットを載せる。
- [ ] **Step 3: bd を閉じる**: `bd close moorestech-bim5 --reason="ADR 0029 実装・PR作成済み"`。マスタPRマージ後のピン差し替えが残る場合は `bd create` で子タスクを積む。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `equipItem` task 定義 | `VanillaSchema/challenges.yml` taskParam switch case | スキーマ switch | 同ファイル `completeResearch` case |
| 2 | `pinTargetType`/`pinTargetParam` | `challenges.yml` mapObjectPin case 内のネスト switch | スキーマ switch（case内） | `map.yml` `veinParam`/`handMiningParam`（同形の enum+switch）。case オブジェクト内の switch はパーサが `Parse()` を再帰するため扱える（`JsonSchemaParser.cs:376`）— **新規パターン（レビュー注目点）**: 既存 yml に「switch case の中の switch」前例は無い。生成名はコンパイルで確認する |
| 3 | `EquipItemChallengeTask` | `Game.Challenge/ChallengeTask/` | `IEquipmentInventoryUpdateEvent` 購読＋初回tick回収 | `CompleteResearchChallengeTask`（イベント購読＋`_initialCheckDone`）、`InInventoryItemChallengeTask`（全プレイヤー走査） |
| 4 | Factory 登録・定数 | `ChallengeFactory` / `VanillaChallengeType` | 既存辞書登録 | 既存4種 |
| 5 | `EquipItemTaskParam`/`pinTargetParam` 検証 | `Core.Master/Validator/ChallengeMasterUtil` | 型switch検証 | 同ファイル既存 case |
| 6 | `MapObjectPinTargetResolver`（マスタ解釈） | `Client.Game/InGame/Tutorial/`（ドメイン側） | static util が `MasterHolder.MapObjectMaster.Map.MapObjects` を読むだけ | 層原則「マスタ解釈はドメイン層」（moorestech-principles）。Core.Master へは追加しない |
| 7 | `SearchNearestMapObject(IReadOnlyList<Guid>, Vector3)` | `MapObjectGameObjectDatastore`（既存メソッドの置換） | 同一走査でGUID集合判定 | 既存メソッド（単一GUID）を置換。旧シグネチャは残さない |
| 8 | キーヒント色 | `app/tokens.css` `--key-hint-*` 群＋`:where(.keyHintText)` | CSS トークン | webui-design §7「文字様式は `:where(.keyHintText)` が唯一の正」 |
| 9 | ドラッグ矢印寸法/周期 | `app/tokens.css` 既存トークン値変更 | CSS トークン | webui-design §8.17 |
| 10 | 装備チャレンジ2件・earn_pin | `generate_challenges.py` 表＋再生成 | key由来GUID | 同スクリプト既存行・2026-08-20 M1 |
| 11 | 研究説明文 | `research.json` ＋ `localization.csv` | マスタ直編集 | 2026-08-20 M4（csv落とし穴） |
| 12 | ピン更新 | `.moorestech-external-revisions.json` | 既存 sync 機構 | 2026-08-20 M5 |

データフロー（装備チャレンジ）: 装備操作 → `EquipmentInventoryData`（既存の書き手）→ `IEquipmentInventoryUpdateEvent` → **読み手** `EquipItemChallengeTask`（購読・判定のみ。既存フローへの交差点なし）→ `OnChallengeComplete`（既存経路）。

機能パリティ（死活表）: 小石ピン（mapObject 指定）= 生きる（generator `pin` が新形式で出力）／木を伐採ピン = 復活（earnItem）／mod_3 の4ピン = 生きる（M2 移行）／インベントリ画面・研究画面のキーヒント = 生きる（色のみ赤へ。ユーザー裁定で全画面赤を選択済み）／ビルドメニュー→ホットバーの矢印 = 生きる（寸法・周期のみ変更。ADR 0029 §4 agent前提）／`inInventoryItem` の主インベントリ限定 = 不変。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0029-tutorial-equip-challenge-pin-target-and-hints.md`（裁定5件の出所を記載）。`.decisions/2026-08-22-*.md` 4件。
- planning中の判断:
  - `pinTargetParam` をネスト switch にし、新 tutorialType を増やさない — 出所: ユーザー裁定 2026-08-22 選択「ドロップ品で探す新param」（mapObjectPin の param 拡張として採択）。agent前提: ネスト switch はパーサ再帰で扱える（`JsonSchemaParser.cs:376`）が yml 前例が無いため新規パターンとしてレビュー注目点に載せる。
  - `SearchNearestMapObject` は単一GUID版を残さず集合版へ置換 — 出所: agent前提（唯一の呼び出し元が `MapObjectPin` で、二重APIを持つ理由が無い）。
  - `EquipItemChallengeTask` の達成判定は全イベントで `GetSelectedItem().Id == 対象` の1条件に畳む — 出所: ユーザー裁定 2026-08-22「選択中スロットに入った時」。スロット更新・選択index更新のどちらでも同じ判定を呼ぶ。
  - forUnitTest モッドに equipItem テストチャレンジ（`...4567-000000000102`・Test1 アイテム）を追加 — 出所: agent前提（`CompleteResearchChallengeTaskTest` が `...0101` で同形）。
  - 研究説明文の日本語表は plan に固定（Task M3）。english は Task M4 で同内容を訳す — 出所: ユーザー裁定 2026-08-22「全49ノード・解放内容ベース」。文言自体は agent起案（ユーザー裁定ではない）。
  - マスタPRと本体PRは同一マージ単位、ピンはマスタ側マージコミットへ — 出所: agent前提（2026-08-20 M5 と同手順。スキーマ変更を含むため順序逆転でロード例外）。
  - webui-design SKILL.md の更新をTask 3に含める — 出所: agent前提（§7/§8.17/§8.19 が現在値を明記しているため、値変更と同時に更新しないと規約表が嘘になる）。
