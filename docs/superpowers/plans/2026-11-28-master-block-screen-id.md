# マスタのブロック画面ID Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ブロックを開いたときの画面を、blockType ではなくマスタの各ブロックが持つ「ブロック画面ID」で選べるようにし、`openable` を廃止する。

**Architecture:** スキーマ `blocks.yml` の `openable: boolean` を必須の enum `blockScreen` に置き換える（`None` は開けない）。サーバーとクライアントの「開けるか」は `BlockScreen != None` で判定し、クライアントの2つの表（`BlockComponentRegistry`・`SectionStackConfigTable`）のキーを blockType からブロック画面IDへ替える。登録の無いIDはマスタ読込時（`MasterHolder.Load` の検証）で止め、表の引き当ても未登録なら例外で止める。

**Tech Stack:** Unity / C#、mooresmaster（YAMLスキーマ→SourceGenerator）、moorestech_master（JSON）、NUnit、UI Toolkit、Playtest DSL

## Requirements

- R1. `VanillaSchema/blocks.yml` から `openable` が消え、必須（`optional` も `default` も無し）の `blockScreen: enum` が同じ位置に入る。選択肢は `None, Generic, Chest, Machine, ElectricGenerator, FuelGearGenerator, GearToElectricGenerator, ElectricMiner, GearMiner, Pump, CleanRoomAirFilter, FluidPipe, FilterSplitter, ElectricToGearGenerator, TrainPlatform, ElectricPole, SpaceElevatorBase, RocketLaunchPad` の18個。受け入れ: 生成された `BlockMasterElement.BlockScreen`（string）と `BlockMasterElement.BlockScreenConst` が使え、`Openable` への参照がコードベースに0件。
- R2. 選択肢に無い `blockScreen` を持つブロックがあると `MasterHolder.Load` が `InvalidOperationException` で止まり、メッセージにブロック名と値が出る。受け入れ: `BlockScreenValidatorTest` が通る。
- R3. moorestech_master の全 mod（server, server_v4〜server_v8）の blocks.json と、本 repo のテスト用 blocks.json 2本（forUnitTest・EditModeInPlayingTestMod）の `openable` が、変換表どおり `blockScreen` に置き換わる。受け入れ: 変換スクリプトの件数一致の assert が通り、`git grep '"openable"'` が対象ファイルで0件。
- R4. 変換は今の画面を変えない: `openable: false` → `None`。`openable: true` は blockType で `Chest→Chest`、`ElectricMachine/GearMachine/CleanRoomMachine→Machine`、`ElectricGenerator→ElectricGenerator`、`FuelGearGenerator/SimpleGearGenerator→FuelGearGenerator`、`GearToElectricGenerator→GearToElectricGenerator`、`ElectricMiner→ElectricMiner`、`GearMiner→GearMiner`、`ElectricPump/GearPump→Pump`、`CleanRoomAirFilter→CleanRoomAirFilter`、`FluidPipe→FluidPipe`、`FilterSplitter→FilterSplitter`、`ElectricToGearGenerator→ElectricToGearGenerator`、`TrainStation/TrainItemPlatform/TrainFluidPlatform→TrainPlatform`、`ElectricPole→ElectricPole`、`SpaceElevatorBase→SpaceElevatorBase`、`RocketLaunchPad→RocketLaunchPad`、それ以外→`Generic`。
- R5. 開けるかの判定は `BlockScreen != BlockScreenConst.None`: クライアント `BlockGameObject` の `BlockInteractable` 付与、サーバー `InventoryRequestProtocol` の表示専用ブロックの成功応答。受け入れ: 既存の `RequestBlockInventoryTest` が通る。
- R6. `BlockComponentRegistry.Resolve` と `SectionStackConfigTable.Resolve` はブロック画面IDを受け、未登録なら例外を投げる（共通画面への黙った縮退を消す）。`OpenBlockInventoryPanel` は `BlockScreen` を持ち、`Components` はそこから引く。受け入れ: `BlockComponentRegistryTest`・`SectionStackConfigTableTest` が新しいキーで通り、`None` 以外の全 `BlockScreenConst` が両表で解決できる（SectionStack 本文のIDだけ `SectionStackConfigTable` に載る）ことをテストが確かめる。
- R7. 画面の都合で blockType を見ていた分岐を画面側へ移す: `OrbitalCargoLargePanelRule` は `Components.Body` が `RocketLaunchPad` か `SpaceElevator` かで決める。`TrainBlockCommands.SetTransferMode` は開いているブロックのマスタの `BlockScreen` から `BlockComponentRegistry.Resolve` で本文の種類を引いて決める（`TrainBlockCommands` はパネルではなく `SubInventoryState` を持つため）。受け入れ: 既存テストが通る。
- R8. ブロックの機能を確かめる blockType 分岐は変えない（`SpecialBlockCommands`・`TrainBlockCommands` の駅名・`BlockSlotRestrictionInput.IsFixedInletBlockType`・`BlockNetworkInfoCache`・`TrainPlatformDetailReader`）。
- R9. 画面の見た目は変わらない。受け入れ: unityプレイ録画テストで、木のチェスト・石窯・ベルトコンベア・直線歯車ベルトコンベア・フィルター分岐器・電柱を開き、今と同じ本文（チェストのグリッド・機械・搬送量パネル・分岐器の専用本文・電柱の専用本文）が出て、基本土台は開かない。
- やらないこと: mod が独自の画面を登録する仕組み、見るだけの画面など新しい画面、状態による画面の切り替え、セーブ・通信形式の変更、`mooresmaster/mooresmaster.SandBox/schema/blocks.json`（生成器の試験用の別スキーマで、ゲームのマスタではない）の変更。

## Global Constraints

- 作業場所: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/master-block-screen-id`（ブランチ `feat/master-block-screen-id`）。Unity は `moores-wt status` で自分の Editor を確かめてから使う。Mac mini で同時に動かす Editor は1本まで。
- master ピン: 現在 `.moorestech-external-revisions.json` の moorestech_master は `84ca888b3f8ef865f6f000c9a84a979a3ea254ac`。moores-wt が作ったピン worktree は `pin-4e5f167f`（古い）なので、moorestech_master 側の変更は `84ca888b` から切った作業ブランチで行う。
- moorestech_master の変更は push して PR を作る（AGENTS.md「関連リポジトリ」）。本 repo のピンはその PR が指す push 済みコミットにする。
- スキーマ編集は edit-schema スキルの手順に従う（SourceGenerator のトリガー方法を含む）。`Mooresmaster.Model.*` を手で書かない。
- .cs を変えたら `uloop compile --project-path ./moorestech_client` を通す。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。
- AGENTS.md の規約: 1ファイル200行未満、1ディレクトリ10ファイルまで、try-catch 禁止、デフォルト引数禁止、日英2行コメント、`.meta` を手で作らない。
- 後方互換は考えない。`optional`・既定値・未登録時の共通画面への縮退で吸収しない。
- セーブ形式は変えない（マスタの値はセーブに乗らない）ので移行ステップは不要。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件 / 検証の公開範囲を前例の internal に、テストの形を前例 RocketLaunchPadParamValidatorTest に揃えた（変更範囲の再検査を含む）。死活表で死ぬ操作なし
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強3・弱2・第3バケツ0 / プレイヤーに見える差の無い内部の形なので、すべて agent前提として判断記録に記載。開けるかの判定を1本化し、ADR 0109 の登録漏れの止め方を直した

---

### Task 1: スキーマとマスタJSONを blockScreen へ置き換え、開ける判定と検証を替える

スキーマを変えた時点で `Openable` を使う全コードがコンパイルできなくなるため、スキーマ・JSON・判定・検証を1タスクで通す。

**Files:**
- Modify: `VanillaSchema/blocks.yml:250-252`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/InventoryRequestProtocol.cs:69`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObject.cs:77-83`
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/Block/Screen/BlockScreens.cs`
- Create: `moorestech_server/Assets/Scripts/Core.Master/Validator/Block/Screen/BlockScreenValidator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Interact/BlockInteractableTest.cs:84-85`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs:30`
- Modify（コンパイルエラー駆動。`BlockMasterElement` の手書き ctor の `openable` 引数を `blockScreen` 文字列へ）: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/CommonBlockPlaceExistingBlockCauseTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/Copy/BlueprintCopyRangeCounterTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundFollowStepTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/Entry/FootprintGroundedRayEntryTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/CommonBlockPlaceRunCalculatorTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/BlockScreen/BlockScreenValidatorTest.cs`
- 別 repo: `moorestech_master` の `server/mods/moorestechAlphaMod_3/master/blocks.json`、`server_v4/mods/moorestechAlphaMod_4/master/blocks.json`、`server_v5/mods/moorestechAlphaMod_5/master/blocks.json`、`server_v6/mods/moorestechAlphaMod_6/master/blocks.json`、`server_v7/mods/moorestechAlphaMod_7/master/blocks.json`、`server_v8/mods/moorestechAlphaMod_8/master/blocks.json`
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Produces: `string BlockMasterElement.BlockScreen`、`BlockMasterElement.BlockScreenConst.None` ほか R1 の18定数（SourceGenerator 生成）。`internal static bool BlockScreens.IsKnownScreen(string blockScreen)`（Core.Master 内だけ）、`public static bool BlockScreens.IsOpenable(BlockMasterElement block)`（`BlockScreen != None`。開けるかの判定はこれ1本）。`internal static class BlockScreenValidator` の `internal static string Validate(Blocks blocks)`（エラー文。空なら正常。前例の検証クラスと同じく internal）

- [ ] **Step 1: スキーマを書き換える**

`VanillaSchema/blocks.yml` の `- key: openable`〜`default: false`（3行）を次で置き換える:

```yaml
    - key: blockScreen
      type: enum
      options:
      - None
      - Generic
      - Chest
      - Machine
      - ElectricGenerator
      - FuelGearGenerator
      - GearToElectricGenerator
      - ElectricMiner
      - GearMiner
      - Pump
      - CleanRoomAirFilter
      - FluidPipe
      - FilterSplitter
      - ElectricToGearGenerator
      - TrainPlatform
      - ElectricPole
      - SpaceElevatorBase
      - RocketLaunchPad
```

edit-schema スキルの手順で SourceGenerator を再生成させる。

- [ ] **Step 2: 変換スクリプトを scratchpad に置く**

`<scratchpad>/convert_block_screen.py`（repo には入れない）:

```python
import json, re, sys

SCREEN_BY_TYPE = {
    "Chest": "Chest",
    "ElectricMachine": "Machine", "GearMachine": "Machine", "CleanRoomMachine": "Machine",
    "ElectricGenerator": "ElectricGenerator",
    "FuelGearGenerator": "FuelGearGenerator", "SimpleGearGenerator": "FuelGearGenerator",
    "GearToElectricGenerator": "GearToElectricGenerator",
    "ElectricMiner": "ElectricMiner", "GearMiner": "GearMiner",
    "ElectricPump": "Pump", "GearPump": "Pump",
    "CleanRoomAirFilter": "CleanRoomAirFilter", "FluidPipe": "FluidPipe",
    "FilterSplitter": "FilterSplitter", "ElectricToGearGenerator": "ElectricToGearGenerator",
    "TrainStation": "TrainPlatform", "TrainItemPlatform": "TrainPlatform", "TrainFluidPlatform": "TrainPlatform",
    "ElectricPole": "ElectricPole", "SpaceElevatorBase": "SpaceElevatorBase", "RocketLaunchPad": "RocketLaunchPad",
}

def screen_of(block):
    # openable が無いブロックは旧スキーマの default false と同じ扱い
    if not block.get("openable", False):
        return "None"
    return SCREEN_BY_TYPE.get(block["blockType"], "Generic")

def convert(path):
    text = open(path, encoding="utf-8").read()
    blocks = json.loads(text)["data"]
    screens = [screen_of(b) for b in blocks]
    with_key = [b for b in blocks if "openable" in b]
    # openable を持たないブロックがあれば止める（行置換では位置を決められない）
    assert len(with_key) == len(blocks), f"{path}: openable の無いブロック {[b['name'] for b in blocks if 'openable' not in b]}"
    pattern = re.compile(r'"openable"\s*:\s*(true|false)')
    matches = pattern.findall(text)
    assert len(matches) == len(blocks), f"{path}: openable {len(matches)} 件とブロック {len(blocks)} 件が合わない"
    it = iter(screens)
    out = pattern.sub(lambda m: f'"blockScreen": "{next(it)}"', text)
    open(path, "w", encoding="utf-8").write(out)
    print(path, len(blocks), {s: screens.count(s) for s in sorted(set(screens))})

for p in sys.argv[1:]:
    convert(p)
```

`"openable"` が `data` 配列の順に1ブロック1回だけ現れることを前提にしている（件数の assert で確かめる）。

- [ ] **Step 3: 本 repo のテスト用 JSON を変換する**

Run: `python3 -I <scratchpad>/convert_block_screen.py moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json`
Expected: 2行の件数表示、assert 無し。`git grep -c '"openable"' -- '*blocks.json'` の結果に2ファイルが出ない。

- [ ] **Step 4: moorestech_master を変換して push・PR を作る**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech_master
git fetch origin
git worktree add -b feat/master-block-screen-id /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/master-block-screen-id 84ca888b3f8ef865f6f000c9a84a979a3ea254ac
cd /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/master-block-screen-id
python3 -I <scratchpad>/convert_block_screen.py server/mods/moorestechAlphaMod_3/master/blocks.json server_v4/mods/moorestechAlphaMod_4/master/blocks.json server_v5/mods/moorestechAlphaMod_5/master/blocks.json server_v6/mods/moorestechAlphaMod_6/master/blocks.json server_v7/mods/moorestechAlphaMod_7/master/blocks.json server_v8/mods/moorestechAlphaMod_8/master/blocks.json
git grep -c '"openable"' -- '*.json'   # Expected: 出力なし
git add -A && git commit -m "blocks.json の openable をブロック画面ID blockScreen へ置き換える"
git push -u origin feat/master-block-screen-id
gh pr create --repo moorestech/moorestech_master --base master --title "blocks.json: openable をブロック画面ID blockScreen へ置き換える" --body "本体 repo の ADR 0109 に対応。openable:false は None、true は blockType ごとに今の画面と同じIDへ変換した。"
```

v8 の確かめ方（ピン 84ca888b 時点の blockType と openable の組み）: `None` になるのは基本土台（Block）・ボイドパイプ（VoidPipe）・歯車チェーンポール・レール橋脚（TrainRail）・ガソリンエンジン発電機（ElectricGenerator, false）・高圧電柱（ElectricPole, false）・クリーンルームの壁/ドア/アイテムハッチ/パイプコネクタ。`Generic` になるのは木の歯車（Gear）・木のシャフト（Shaft）・歯車ベルトコンベア系（GearBeltConveyor）・ベルトコンベア系（BeltConveyor）。核融合炉（ElectricGenerator, true）は `ElectricGenerator`。スクリプトが出した内訳を PR 本文に貼る。

- [ ] **Step 5: ピンを更新する**

`.moorestech-external-revisions.json` の moorestech_master の `commitHash` を Step 4 で push したコミットの完全なハッシュへ替える。テストプレイ用のマスタ（`../moorestech_master/server_v8/mods`）は、そのコミットを指すピン worktree を `worktree-and-review-pitfalls.md` の手順で用意する。

- [ ] **Step 6: 検証を書く**

`moorestech_server/Assets/Scripts/Core.Master/Validator/Block/Screen/BlockScreens.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block.Screen
{
    /// <summary>
    /// ブロック画面IDの選択肢と「開けるか」の判定を1か所に持つ
    /// Holds the block screen ID options and the "is openable" rule in one place
    /// </summary>
    public static class BlockScreens
    {
        // 選択肢の正はスキーマから生成された定数クラスだけ。ここで一覧を書き写さない
        // The generated const class is the only source of options; never copy the list here
        private static readonly HashSet<string> KnownScreens = typeof(BlockMasterElement.BlockScreenConst)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetRawConstantValue())
            .ToHashSet();

        internal static bool IsKnownScreen(string blockScreen)
        {
            return KnownScreens.Contains(blockScreen);
        }

        public static bool IsOpenable(BlockMasterElement block)
        {
            return block.BlockScreen != BlockMasterElement.BlockScreenConst.None;
        }
    }
}
```

`moorestech_server/Assets/Scripts/Core.Master/Validator/Block/Screen/BlockScreenValidator.cs`:

```csharp
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block.Screen
{
    /// <summary>
    /// ブロック画面IDがスキーマの選択肢にあるかを検証する（生成ローダーは enum の値を検査しないため）
    /// Validates each block screen ID is a schema option (the generated loader does not check enum values)
    /// </summary>
    internal static class BlockScreenValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = string.Empty;
            foreach (var block in blocks.Data)
            {
                if (!BlockScreens.IsKnownScreen(block.BlockScreen)) logs += $"[BlockMaster] Block:{block.Name} has unknown blockScreen:{block.BlockScreen}\n";
            }
            return logs;
        }
    }
}
```

`BlockMasterUtil.Validate` の `FilterSplitterParamValidator.Validate(blocks);` の次の行に `errorLogs += BlockScreenValidator.Validate(blocks);` を足し、`using Core.Master.Validator.Block.Screen;` を加える。

- [ ] **Step 7: 開ける判定を替える**

`InventoryRequestProtocol.cs:69` の `block.BlockMasterElement.Openable` を `BlockScreens.IsOpenable(block.BlockMasterElement)` に替える（`using Core.Master.Validator.Block.Screen;` を足す。Server.Protocol の asmdef が Core.Master を参照していることを確かめる）。

`BlockGameObject.cs:79` を次にする:

```csharp
            // 開けるブロック（画面IDが None でない）のみインタラクト面を初期化
            // Initialize the interact face only for openable blocks (screen ID other than None)
            if (BlockScreens.IsOpenable(blockMasterElement))
```

（`BlockGameObject.cs` に `using Core.Master.Validator.Block.Screen;` を足す。）

- [ ] **Step 8: 手書き ctor を直しコンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
コンパイルエラーの出た手書き `new BlockMasterElement(...)` の `openable` 位置の `bool` を `BlockMasterElement.BlockScreenConst.None` に替える（上の Files 節の5ファイル。他にも出ればそれも）。`BlockInteractableTest.cs:84-85` の `FindMaster(...).Openable` は `BlockScreens.IsOpenable(FindMaster(...))` に替える（本番の判定と同じ1本を検証するため）。
Expected: ErrorCount 0

- [ ] **Step 9: 検証のテストを書く**

前例 `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/OrbitalPayload/RocketLaunchPadParamValidatorTest.cs` と同じく、テスト用 blocks.json の JToken を書き換えて `BlockMasterUtil.Validate` を通す。`moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Master/BlockScreen/BlockScreenValidatorTest.cs`:

```csharp
using System.IO;
using System.Linq;
using Core.Master;
using Core.Master.Validator;
using Mod.Config;
using Mod.Loader;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Master.BlockScreen
{
    public class BlockScreenValidatorTest
    {
        [TestCase("chest")]
        [TestCase("true")]
        [TestCase("")]
        public void スキーマに無いブロック画面IDを止める(string unknownScreen)
        {
            // 先頭のブロックの画面IDだけを未知の値にする
            // Replace only the first block's screen ID with an unknown value
            var blocksJToken = PrepareBlocksJson();
            var firstBlock = blocksJToken["data"].Children<JObject>().First();
            firstBlock["blockScreen"] = unknownScreen;

            var isValid = BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var errorLogs);

            Assert.IsFalse(isValid);
            StringAssert.Contains($"Block:{(string)firstBlock["name"]} has unknown blockScreen:{unknownScreen}", errorLogs);
        }

        [Test]
        public void テストマスタのブロック画面IDは検証を通る()
        {
            var isValid = BlockMasterUtil.Validate(new BlockMaster(PrepareBlocksJson()).Blocks, out var errorLogs);

            Assert.IsTrue(isValid, errorLogs);
        }

        private static JToken PrepareBlocksJson()
        {
            // 依存マスタ初期化しJTokenのみ変更
            // Initialize dependency masters, then edit only the in-test JToken
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            return JToken.Parse(File.ReadAllText(blocksJsonPath));
        }
    }
}
```

`using` は前例の通りに置き、使わないものはコンパイルの警告を見て消す。

- [ ] **Step 10: テストを回す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockScreenValidatorTest|RequestBlockInventoryTest|BlockMaster"`
Expected: 全 PASS。続けて `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\."` でサーバーテスト全体を回し、このタスク前から落ちていたもの以外に失敗が無い（前からの失敗は `git stash` 前の master で同じテストを回して見比べ、報告に書く）。

- [ ] **Step 11: コミットする**

```bash
git add VanillaSchema/blocks.yml moorestech_server moorestech_client .moorestech-external-revisions.json
git commit -m "openable をブロック画面ID blockScreen へ置き換え、未知のIDをマスタ検証で止める"
```

### Task 2: クライアントの画面の表をブロック画面IDで引く

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Logic/Panel/BlockComponentRegistry.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/SectionStack/SectionStackConfigTable.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/BlockInventoryPanelState.cs:64-81`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Panel/BlockInventoryPanelStateFactory.cs:23`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/SectionStack/SectionStackBodyFactory.cs:27-29`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui/Screens/BlockInventory/Registry/BlockInventoryPartRegistry.cs:43-46`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/BlockInventory/Special/Cargo/OrbitalCargoLargePanelRule.cs:20`
- Modify: `moorestech_client/Assets/Scripts/Client.Ui.State/Train/Platform/Edit/TrainBlockCommands.cs:39-42`
- Modify（コンパイルエラー駆動）: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel/SectionStackConfigTableTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel/BlockInventoryPanelStateFactoryTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/Panel/BlockComponentRegistryTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/BlockInventoryTitleActionTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/SectionStack/SectionDetachTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/Ui/Screens/BlockInventory/Special/SectionStack/SpecialBlockDetailsTest.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel/BlockRegistryCoverageTest.cs`（と `.meta`）、`moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel/Fixtures/v8-block-ui-registry.json`（と `.meta`。Fixtures が空になればディレクトリと `.meta` も）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Ui/State/BlockInventory/PanelModel/BlockScreenCoverageTest.cs`

**Interfaces:**
- Consumes: `BlockMasterElement.BlockScreen`、`BlockMasterElement.BlockScreenConst`（Task 1）
- Produces: `public static BlockInventoryComponents BlockComponentRegistry.Resolve(string blockScreen)`（未登録・`None` は `InvalidOperationException`）、`public static SectionStackConfig SectionStackConfigTable.Resolve(string blockScreen)`（SectionStack 本文のID以外は `InvalidOperationException`）、`public string OpenBlockInventoryPanel.BlockScreen { get; private set; }`、`OpenBlockInventoryPanel` の internal ctor は `(string blockType, string blockScreen, Guid blockGuid, BlockId blockId, BlockInstanceId blockInstanceId, Vector3Int position, IReadOnlyList<SlotItem> itemSlots, BlockInventoryPanelError error)`、`SectionStackBodyFactory.Create(string blockScreen, BlockInventoryBodyMount mount)`

- [ ] **Step 1: BlockComponentRegistry をブロック画面IDで引く**

`BlockComponentRegistry.cs` の `BlockComponentRegistry` クラス（enum と struct はそのまま）を次で置き換える。`using System;` と `using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;` を足す:

```csharp
    /// <summary>
    /// マスタのブロック画面ID → 本文の種類と題の操作。None（開けない）と未登録は引けない
    /// Master block screen ID → body kind and title action; None (not openable) and unregistered IDs never resolve
    /// </summary>
    public static class BlockComponentRegistry
    {
        private static readonly IReadOnlyDictionary<string, BlockInventoryComponents> ByScreen = new Dictionary<string, BlockInventoryComponents>
        {
            [S.Generic] = Body(BlockInventoryBodyKind.SectionStack),
            [S.Chest] = Body(BlockInventoryBodyKind.SectionStack),
            [S.Machine] = Body(BlockInventoryBodyKind.SectionStack),
            [S.ElectricGenerator] = Body(BlockInventoryBodyKind.SectionStack),
            [S.FuelGearGenerator] = Body(BlockInventoryBodyKind.SectionStack),
            [S.GearToElectricGenerator] = Body(BlockInventoryBodyKind.SectionStack),
            [S.ElectricMiner] = Body(BlockInventoryBodyKind.SectionStack),
            [S.GearMiner] = Body(BlockInventoryBodyKind.SectionStack),
            [S.Pump] = Body(BlockInventoryBodyKind.SectionStack),
            [S.CleanRoomAirFilter] = Body(BlockInventoryBodyKind.SectionStack),
            [S.FluidPipe] = Body(BlockInventoryBodyKind.SectionStack),
            [S.FilterSplitter] = Body(BlockInventoryBodyKind.FilterSplitter),
            [S.ElectricToGearGenerator] = Body(BlockInventoryBodyKind.ElectricToGear),
            [S.TrainPlatform] = Body(BlockInventoryBodyKind.TrainPlatform),
            [S.ElectricPole] = Body(BlockInventoryBodyKind.ElectricPole),
            [S.SpaceElevatorBase] = Body(BlockInventoryBodyKind.SpaceElevator),
            [S.RocketLaunchPad] = new BlockInventoryComponents(BlockInventoryBodyKind.RocketLaunchPad, BlockInventoryTitleActionKind.RocketLaunchPadCommand),
        };

        public static BlockInventoryComponents Resolve(string blockScreen)
        {
            // 未知の値はマスタ検証が止め、None は開けないので、ここへ来たら登録漏れ
            // Unknown values are stopped by master validation and None never opens, so reaching here is a missing registration
            if (ByScreen.TryGetValue(blockScreen, out var components)) return components;
            throw new InvalidOperationException($"ブロック画面ID {blockScreen} の本文が登録されていない");
        }

        private static BlockInventoryComponents Body(BlockInventoryBodyKind body)
        {
            return new BlockInventoryComponents(body, BlockInventoryTitleActionKind.None);
        }
    }
```

`Registered` プロパティは削除する（参照は Delete する `BlockRegistryCoverageTest` だけ）。

- [ ] **Step 2: SectionStackConfigTable をブロック画面IDで引く**

`SectionStackConfigTable.cs` の `using T = ...BlockTypeConst;` を `using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;` に替え、`using System;` を足し、クラス本体を次にする（各行の gridId・rowId・真偽値は今の表から写す）:

```csharp
    public static class SectionStackConfigTable
    {
        private static readonly Dictionary<string, SectionStackConfig> ByScreen = new()
        {
            [S.Generic] = new SectionStackConfig(Grid("generic-block-grid", false), Row("generic-block-fluids", true)),
            [S.FluidPipe] = new SectionStackConfig(SectionStackItemGrid.Hidden, new DetailsSectionStackFluid("fluid-pipe-fluid-slots")),
            [S.Chest] = new SectionStackConfig(Grid("chest-grid", true), SectionStackFluid.Hidden),
            [S.Machine] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("machine-fluid-slots", false)),
            [S.ElectricGenerator] = new SectionStackConfig(Grid("generator-fuel-grid", false), Row("generator-fluid-slots", false)),
            [S.FuelGearGenerator] = new SectionStackConfig(Grid("generator-fuel-grid", true), SectionStackFluid.Hidden),
            [S.GearToElectricGenerator] = new SectionStackConfig(SectionStackItemGrid.Hidden, SectionStackFluid.Hidden),
            [S.ElectricMiner] = new SectionStackConfig(Grid("miner-output-grid", true), SectionStackFluid.Hidden),
            [S.GearMiner] = new SectionStackConfig(Grid("gear-miner-output-grid", true), SectionStackFluid.Hidden),
            [S.Pump] = new SectionStackConfig(SectionStackItemGrid.Hidden, Row("pump-fluid-slots", false)),
            [S.CleanRoomAirFilter] = new SectionStackConfig(Grid("clean-room-filter-grid", true), SectionStackFluid.Hidden),
        };

        public static SectionStackConfig Resolve(string blockScreen)
        {
            // 標準の本文を持つIDだけが載る。専用本文のIDで呼ぶのは配線の誤り
            // Only IDs with the standard body are listed; calling with a dedicated-body ID is a wiring error
            if (ByScreen.TryGetValue(blockScreen, out var config)) return config;
            throw new InvalidOperationException($"ブロック画面ID {blockScreen} の標準の本文の設定が無い");
        }

        private static SectionStackItemGrid Grid(string gridId, bool renderEmpty) => new ShownSectionStackItemGrid(gridId, renderEmpty);
        private static SectionStackFluid Row(string rowId, bool showProgress) => new RowSectionStackFluid(rowId, showProgress);
    }
```

`public static readonly SectionStackConfig Generic` を参照している箇所がコンパイルエラーで出たら `SectionStackConfigTable.Resolve(BlockMasterElement.BlockScreenConst.Generic)` に替える。XML ドキュメントコメントの「blockType」は「ブロック画面ID」に直す。

- [ ] **Step 3: パネルにブロック画面IDを持たせる**

`BlockInventoryPanelState.cs` の `OpenBlockInventoryPanel` に `public string BlockScreen { get; private set; }` を `BlockType` の次に足し、ctor を次にする:

```csharp
        internal OpenBlockInventoryPanel(string blockType, string blockScreen, Guid blockGuid, BlockId blockId, BlockInstanceId blockInstanceId, Vector3Int position, IReadOnlyList<SlotItem> itemSlots, BlockInventoryPanelError error) : base(itemSlots, error)
        {
            BlockType = blockType;
            BlockScreen = blockScreen;
            BlockGuid = blockGuid;
            BlockId = blockId;
            BlockInstanceId = blockInstanceId;
            Position = position;
            Components = BlockComponentRegistry.Resolve(blockScreen);
        }
```

`BlockInventoryPanelStateFactory.cs:23` を `return new OpenBlockInventoryPanel(master.BlockType, master.BlockScreen, master.BlockGuid, blockId, blockInstanceId, position, itemSlots, error);` にする。

- [ ] **Step 4: 本文の組み立てをブロック画面IDで行う**

`SectionStackBodyFactory.Create(string blockType, ...)` の引数名を `blockScreen` にし、`SectionStackConfigTable.Resolve(blockScreen)` で引く。XML コメントの「blockType の設定」を「ブロック画面IDの設定」に直す。
`BlockInventoryPartRegistry.cs:43` の `_sectionStack.Create(panel.BlockType, mount)` を `_sectionStack.Create(panel.BlockScreen, mount)` に替える。`:46`（専用本文の工場が登録されていないときの逃げ道）は `_sectionStack.Create(BlockMasterElement.BlockScreenConst.Generic, mount)` にし、`:45` の警告文の `blockType={panel.BlockType}` を `blockScreen={panel.BlockScreen}` に替える（専用本文のIDで `SectionStackConfigTable.Resolve` を呼ぶと例外になるため。警告と共通画面で描く今の挙動を保つ）。`using Mooresmaster.Model.BlocksModule;` を足す。

- [ ] **Step 5: 画面の都合の分岐を本文の種類で決める**

`OrbitalCargoLargePanelRule.IsLargePanel` を次にし、不要になった `using Mooresmaster.Model.BlocksModule;` を消す。コメント「ブロックの種類だけで決まり」は「本文の種類だけで決まり」に直す:

```csharp
        public bool IsLargePanel(OpenBlockInventoryPanel panel)
        {
            return panel.Components.Body == BlockInventoryBodyKind.RocketLaunchPad || panel.Components.Body == BlockInventoryBodyKind.SpaceElevator;
        }
```

（`using Client.Ui.State.BlockInventory.Logic.Panel;` を足す。）

`TrainBlockCommands` の `TryResolveOpenBlock` を、blockType の代わりにブロックのマスタを返す形にする（`using Core.Master;` を足す）:

```csharp
        private bool TryResolveOpenBlock(out BlockSubInventorySource source, out BlockMasterElement master)
        {
            master = null;
            source = _subInventoryState.CurrentSubInventorySource as BlockSubInventorySource;
            if (source == null || !source.TryGetBlockIdentity(out var blockGuid, out _)) return false;
            master = MasterHolder.BlockMaster.GetBlockMaster(blockGuid);
            return true;
        }
```

`SetStationName` は `out var master` で受けて `master.BlockType != BlockTypeConst.TrainStation` を見る（駅名はブロックの機能なので blockType のまま。R8）。`SetTransferMode` は `out var master` で受けて `BlockComponentRegistry.Resolve(master.BlockScreen).Body != BlockInventoryBodyKind.TrainPlatform` を見る。

- [ ] **Step 6: 網羅テストを置き換える**

`BlockRegistryCoverageTest.cs` と fixture を `git rm` し、`BlockScreenCoverageTest.cs` を作る:

```csharp
using System.Linq;
using System.Reflection;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.SectionStack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// None 以外の全ブロック画面IDが本文を引け、標準の本文のIDだけが欄の設定を持つ
    /// Every block screen ID except None resolves a body, and exactly the standard-body IDs have section settings
    /// </summary>
    public class BlockScreenCoverageTest
    {
        [Test]
        public void None以外の全ブロック画面IDが登録されている()
        {
            var screens = typeof(BlockMasterElement.BlockScreenConst).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => (string)field.GetRawConstantValue())
                .Where(screen => screen != BlockMasterElement.BlockScreenConst.None)
                .ToArray();
            foreach (var screen in screens)
            {
                // 標準の本文なら欄の設定も引ける
                // A standard-body ID also resolves its section settings
                var components = BlockComponentRegistry.Resolve(screen);
                if (components.Body == BlockInventoryBodyKind.SectionStack) Assert.IsNotNull(SectionStackConfigTable.Resolve(screen), screen);
            }
            Assert.Throws<System.InvalidOperationException>(() => BlockComponentRegistry.Resolve(BlockMasterElement.BlockScreenConst.None));
        }
    }
}
```

- [ ] **Step 7: 既存テストを直す**

Run: `uloop compile --project-path ./moorestech_client`
Files 節のテストで出るエラーを直す。blockType 文字列で `Resolve` していたテストケースは、R4 の変換表で対応するブロック画面IDに置き換え、期待値はそのまま（例: `Resolve(BlockTypeConst.ElectricMachine)` の期待 → `Resolve(BlockScreenConst.Machine)`）。`SectionStackConfigTable.Resolve` に未登録を渡して Generic を期待していたケースは、未登録で `InvalidOperationException` を期待する形に替える。
Expected: ErrorCount 0

- [ ] **Step 8: テストを回す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockInventory|SectionStack|BlockComponentRegistry|BlockScreen|TrainPlatform|OrbitalCargo|SpecialBlockDetails"`
Expected: 全 PASS

- [ ] **Step 9: コミットする**

```bash
git add moorestech_client
git commit -m "ブロック画面の表をブロック画面IDで引き、未登録は例外で止める"
```

### Task 3: unityプレイ録画テストで画面が変わらないことを確かめる

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/Scenarios/BlockScreenIdScenario.cs`（Scenarios ディレクトリの置き場・命名は unity-playmode-recorded-playtest スキルの `references/write-scenario.md` に従う。既存の置き場が別ならそちらに合わせる）

**Interfaces:**
- Consumes: Task 1・2 の全変更、ピン更新済みのマスタ

- [ ] **Step 1: シナリオを書く**

`references/write-scenario.md` のテンプレートで、`SetupDebugEnvironment` → `SkipOpeningSkit` の後、プレイヤーの前 2m 以内に1つずつ `PlaceBlockDirect` で置き、照準を合わせて `PressInteract()` で開き、出た画面を確かめて閉じる。対象と確かめる内容:

| ブロック名 | 確かめること |
|---|---|
| 木のチェスト | testid `chest-grid` が出る |
| 石窯 | 機械の本文の testid（今の master で同じシナリオを回して控えたもの）が出る |
| ベルトコンベア | 搬送量パネルが出る |
| 直線歯車ベルトコンベア | 搬送量パネルが出る |
| フィルター分岐器 | フィルター分岐器の専用本文が出る |
| 電柱 | 電柱の専用本文が出る |
| 基本土台 | `PressInteract()` で何も開かず `CurrentUiState` が GameScreen のまま |

各ブロックで `p.Screenshot("NN-<ブロック名>")` を撮る。testid は今の master のブランチで同じシナリオを `Record = false` で回して実在を確かめてから使う（推測の testid で書かない）。

- [ ] **Step 2: 実行する**

unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh` で、調査は `Record = false`、最後の1回だけ `Record = true`。
Expected: `result.json` の `Success: true`、Asserts 全通過。録画とスクショで今の master と同じ画面であることを目で確かめる。

- [ ] **Step 3: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Playtest
git commit -m "ブロック画面IDで今と同じ画面が開くことを確かめる録画シナリオを追加"
```

### Task 4: 全ブランチレビュー

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映が判定経路・条件式に触れたら、Task 3 の unityプレイ録画テストを反映後のバイナリで回し直してから完了とする。

### Task 5: PR 作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-create スキルで PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する（解消の実作業は pr-create 経由で opus subagent に委譲される）。PR 本文には moorestech_master の PR の URL を書く。全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。PR 作成後 `moores-wt rm master-block-screen-id` で worktree と Editor を畳む。

---

## 判断記録（ADR）

- 設計: `docs/adr/0109-block-screen-chosen-by-master-screen-id.md`、`.decisions/2026-11-27-ブロック画面はマスタに画面IDを書きクライアントのC#登録で引く.md`、`.decisions/2026-11-27-openableはマスタの画面指定にまとめて廃止する.md`、`.decisions/2026-11-27-マスタから指せる画面IDは本体に登録した画面だけにする.md`、`.decisions/2026-11-28-画面IDは今ある画面だけにし見るだけの版は作らない.md`
- ブロック画面IDの名前は `vanilla:chest` 形ではなくスキーマ enum の PascalCase（`Chest`）にする。出所: agent前提（ADR 0109 の agent前提1 を planning 中に変更。生成器は選択肢をそのまま C# の定数名にするので `:` を含められない〈`mooresmaster/mooresmaster.Generator/CodeGenerate/CodeGenerator.cs:103-118`〉。mod は新しい画面を作れない裁定なので名前空間の接頭辞は不要）
- ID の一覧はスキーマ enum を正とし、マスタ検証も網羅テストも生成定数クラスをリフレクションで列挙する（一覧を書き写さない）。出所: agent前提（生成ローダーは enum の値を検査しない〈`LoaderGenerate/LoaderGenerator.cs` に enum 検査なし〉。生成器を改修して一覧の配列を出させる案は生成器全体に波及するので採らない）
- 検証は `Core.Master/Validator/Block/Screen/` に置く。出所: agent前提（`Validator/Block/` は既に .cs 10本で、1ディレクトリ10ファイルの規約に当たる）
- `OpenBlockInventoryPanel` は `BlockType` を残したまま `BlockScreen` を足す。出所: agent前提（ブロックの機能の確認〈R8〉が blockType を使い続けるため）
- 表の引き当ての未登録は共通画面へ縮退せず例外。出所: agent前提（ADR 0109 agent前提3。未知の値はマスタ検証が先に止めるので、例外に来るのは登録漏れだけ）
- Task 1 はスキーマ・JSON・判定・検証を1タスクにする。出所: agent前提（スキーマを変えた時点で `Openable` 参照がコンパイルできなくなり、分けると途中のコミットがビルドできない）
- `mooresmaster/mooresmaster.SandBox/schema/blocks.json` は変えない。出所: agent前提（生成器の試験用の別スキーマで、ゲームのマスタではない）
- ランタイム挙動（ブロックを開く操作と画面）に触れるので unityプレイ録画テスト（Task 3）を入れる。
- `VanillaSchema/blocks.yml`: `openable` を消し必須の enum `blockScreen` を同じ位置に入れる（optional・default を付けない）。出所: ユーザー裁定 2026-11-27 選択「画面の指定にまとめる（推奨）」＋ agent前提（AGENTS.md「optional で吸収しない」）
- `InventoryRequestProtocol.cs`: 表示専用ブロックの成功応答の条件を `Openable` から `BlockScreen != None` へ替えるだけ。応答の形・通信形式は変えない。出所: agent前提（ADR 0109 agent前提5）
- `BlockScreenValidator.cs`・`BlockMasterUtil.cs`: 既存の `BlockMasterUtil.Validate` の連結に検証を1本足す形（前例: 同ファイルの `FilterSplitterParamValidator.Validate` など）。出所: agent前提（ADR 0109 agent前提3）
- 開けるかの判定は `Core.Master/Validator/Block/Screen/BlockScreens.cs` の `IsOpenable` 1本にし、`BlockGameObject.cs`・`InventoryRequestProtocol.cs`・`BlockInteractableTest.cs` が使う。出所: agent前提（Phase 2.6 検査6〈弱〉: 同じ式が本番2か所とテストに出ていた）
- `BlockComponentRegistry.Resolve` と `SectionStackConfigTable.Resolve` は失敗を例外1種で表し、型を分けない。出所: agent前提（Phase 2.6 5-A〈強〉。どちらの失敗も開発時の登録漏れで、プレイヤーに見える差が無い）
- ブロック画面IDは C# の enum 型ではなく string ＋生成定数で扱う。出所: agent前提（Phase 2.6 5-I〈強〉。mooresmaster の生成器が enum を string と `*Const` で出すため。生成器の改修は範囲外）
- `TrainBlockCommands.TryResolveOpenBlock` の false の理由（ブロック画面でない／GUID が取れない）は区別しない。出所: agent前提（Phase 2.6 5-C〈弱〉。既存の形を引き継ぎ、どちらも `BlockNotOpen` で返す）
- 登録漏れの止め方: スキーマに無い値はマスタ読込（`BlockScreenValidator`）で起動時に止め、スキーマの選択肢にクライアントの画面が登録されていないことは `BlockScreenCoverageTest`（CI）で止める。開いた時の `Resolve` の例外は最後の網。ADR 0109 agent前提3 をこの形に直した。出所: agent前提（Phase 2.6 検査7〈強〉。選択肢もクライアントの表も同じビルドに入るコードなので、食い違いはビルドの検査で必ず見つかる）
- `BlockScreenValidator` と `BlockScreens.IsKnownScreen` は internal（前例: `Core.Master/Validator/Block/RocketLaunchPadParamValidator.cs` などの検証クラス）。`BlockScreens` と `IsOpenable` は Client.Game・Server.Protocol が使うので public（前例: `FilterSplitter/FilterSplitterOutputSideResolver.cs`）。テストは前例 `RocketLaunchPadParamValidatorTest` と同じく JSON を書き換えて `BlockMasterUtil.Validate` を通す形にし、`Tests/UnitTest/Core/Master/BlockScreen/` に置く。出所: agent前提（配置検査の違反1件を修正）
- `BlockInventoryPartRegistry` の「専用本文の工場が無いときは警告して共通画面で描く」逃げ道は残し、共通画面の設定は `Generic` で引く。出所: agent前提（配置検査の注目点。工場の登録漏れは本番では起きず、工場を登録しないテスト構成が通る道なので今の挙動を保つ）
- 本番コードで生成定数クラスをリフレクションで列挙するのは `BlockScreens` が初めて（前例はテストのみ）。出所: agent前提（配置検査の注目点。生成器を改修しない判断の帰結。レビューの注目点として PR 本文に書く）
