# Satisfactory式設置システム プラン1: サーバー基盤 実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ブロック設置を「BlockId直指定＋建設コスト(requiredItems)消費」で行う新`PlaceBlockProtocol`と、その前提となるマスタスキーマ・アンロック状態・返却変更をサーバー側に追加する（既存プロトコルは削除しない。追加のみ）。

**Architecture:** 電線自動接続で確立済みの「Evaluate（設置前検証）→Execute（消費実行）」パターンを建設コストに一般化する。アンロック状態は既存4種（CraftRecipe/Item/ChallengeCategory/MachineRecipe）と同型のBlock/TrainCar用を追加し、200行制限を守るため既存コントローラをドメイン別ホルダーに分割する。

**Tech Stack:** Unity C# / MessagePack / UniRx / Mooresmaster SourceGenerator / NUnit / uloop CLI

**Spec:** `docs/superpowers/specs/2026-07-03-satisfactory-style-placement-design.md`

## 全体ロードマップ（本プランはその1）

サーバーコードはクライアントプロジェクトにインポートされるため、ペイロード変更は同一プランでクライアント送信側も直す必要がある。よって以下の縦切り5プラン構成とし、各プラン完了時点でコンパイル・テストが通る状態を保つ。

1. **サーバー基盤（本プラン）**: スキーマ追加・UnlockState・新PlaceBlockProtocol・RemoveBlock返却変更。すべて追加のみ
2. **マスタ追加移行**: moorestech_master v8にrequiredItems/imagePath/category/initialUnlockedを付与する変換スクリプト（クラフトレシピからの機械変換）
3. **クライアント通常ブロック**: PlacementSelection導入・ビルドメニューUI・CommonBlockPlaceSystemの新プロトコル切替
4. **特殊システム縦切り**: TrainCar設置×2・橋脚付きレール接続・電柱延長・チェーンポール延長（GearChainPoleExtendProtocol、2026-07-05追加）・接続ツールのビルドメニュー統合（サーバー＋クライアント同時）
5. **破壊的クリーンアップ**: 旧PlaceBlockFromHotBarProtocol削除・ブロック/車両アイテムとレシピ削除・itemGuid/usePlaceItemsフィールド削除・返却フォールバック削除

## Global Constraints

- 作業開始時に必ず`pwd`で現在ディレクトリを確認する（git worktree対策）。本プランは`/Users/katsumi/moorestech`で作業する
- .csファイル変更後は必ずコンパイルを実行する: `uloop compile --project-path ./moorestech_client`
- **新規サーバー.csファイルはUnity再起動しないとコンパイル対象にならないことがある**（immutable package扱い）。新規ファイル追加後にuloop compileが新ファイルを認識しない・テストが見つからない場合は、uloop-launchスキルでUnityを再起動する
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。「Unity is reloading (Domain Reload in progress)」エラーが出たら45秒待ってリトライ
- `partial`は如何なる条件でも絶対禁止
- 1ファイル200行以下。超える場合はディレクトリ構造で分割
- 1ディレクトリに入れるコードは10ファイルまで
- 単純なgetter/setterプロパティ禁止。値のSetは`public void SetHoge`メソッド
- デフォルト引数禁止。try-catch禁止（条件分岐・nullチェックで対応）
- nullチェックは外部データ・非同期ロード結果のみ。MasterHolder等のコアコンポーネントには不要
- 主要な処理セクションに日本語・英語の2行セットコメント（`// 日本語` → `// English`、各1行厳守）を約3〜10行ごとに。自明なコメントは書かない
- 複雑なメソッド内のローカル関数は`#region Internal`にまとめる。クラス直下のprivateメソッド群を`#region Internal`で囲うのは禁止
- イベントはC#標準のAction/eventではなくUniRx（Subject/IObservable）を使用
- .metaファイルは手動作成しない（Unity自動生成。Unity起動で作られた.metaのコミットは可）
- VanillaSchemaのyml編集時は必ずedit-schemaスキルを起動して手順に従う
- 各タスク完了時にコミットする（作業消失防止）

---

### Task 1: マスタスキーマ追加（blocks / train / gameAction）

**Files:**
- Modify: `VanillaSchema/blocks.yml`（`- key: data`のitems propertiesに追加）
- Modify: `VanillaSchema/train.yml`（`- key: trainCars`のitems propertiesに追加）
- Modify: `VanillaSchema/ref/gameAction.yml`（enum optionsとcasesに追加）

**Interfaces:**
- Produces: Mooresmaster生成型 `BlockMasterElement.RequiredItems`（`ConstructionRequiredItemElement[]`、各要素は`.ItemGuid`(Guid)/`.Count`(int)）、`BlockMasterElement.ImagePath`/`.Category`(string)/`.SortPriority`(int)/`.InitialUnlocked`(bool)、`TrainCarMasterElement.RequiredItems`（`TrainCarRequiredItemElement[]`）/`.InitialUnlocked`(bool)、`GameActionElement.GameActionTypeConst.unlockBlock`/`.unlockTrainCar`、`UnlockBlockGameActionParam.UnlockBlockGuids`(Guid[])、`UnlockTrainCarGameActionParam.UnlockTrainCarGuids`(Guid[])

- [ ] **Step 1: edit-schemaスキルを起動する**

`Skill(edit-schema)`を起動し、スキーマ編集とSourceGenerator再生成の正規手順を確認する。以降のStepのYAML編集はその手順に従って行う。

- [ ] **Step 2: blocks.ymlのブロック要素に建設コスト系フィールドを追加**

`VanillaSchema/blocks.yml`の`- key: data`→`items`→`properties`内、`- key: itemGuid`ブロック（`foreignKey`含む5行）の直後に以下を挿入する:

```yaml
    - key: requiredItems
      type: array
      overrideCodeGeneratePropertyName: ConstructionRequiredItemElement
      items:
        type: object
        properties:
        - key: itemGuid
          type: uuid
          foreignKey:
            schemaId: items
            foreignKeyIdPath: /data/[*]/itemGuid
            displayElementPath: /data/[*]/name
        - key: count
          type: integer
          default: 1
    - key: imagePath
      type: string
      optional: true
    - key: category
      type: string
      optional: true
    - key: sortPriority
      type: integer
      default: 0
    - key: initialUnlocked
      type: boolean
      default: false
```

注意: BaseCampのblockParam内に既に`requiredItems`という同名キーが存在する（要素キーは`amount`）。生成される要素クラス名の衝突を避けるため`overrideCodeGeneratePropertyName: ConstructionRequiredItemElement`を必ず付ける。

- [ ] **Step 3: train.ymlのtrainCars要素に建設コストと初期解放を追加**

`VanillaSchema/train.yml`の`- key: trainCars`→`items`→`properties`内、`- key: itemGuid`ブロックの直後に以下を挿入する:

```yaml
    - key: requiredItems
      type: array
      overrideCodeGeneratePropertyName: TrainCarRequiredItemElement
      items:
        type: object
        properties:
        - key: itemGuid
          type: uuid
          foreignKey:
            schemaId: items
            foreignKeyIdPath: /data/[*]/itemGuid
            displayElementPath: /data/[*]/name
        - key: count
          type: integer
          default: 1
    - key: initialUnlocked
      type: boolean
      default: false
```

- [ ] **Step 4: gameAction.ymlにunlockBlock / unlockTrainCarを追加**

`VanillaSchema/ref/gameAction.yml`の`gameActionType`のoptionsに2行追加:

```yaml
    - unlockBlock
    - unlockTrainCar
```

（`- unlockMachineRecipe`の直後に挿入）

同ファイルの`cases:`配列に以下の2ケースを追加（`- when: unlockMachineRecipe`ケースの直後に挿入）:

```yaml
    - when: unlockBlock
      type: object
      isDefaultOpen: true
      properties:
      - key: unlockBlockGuids
        type: array
        items:
          type: uuid
          foreignKey:
            schemaId: blocks
            foreignKeyIdPath: /data/[*]/blockGuid
            displayElementPath: /data/[*]/name
    - when: unlockTrainCar
      type: object
      isDefaultOpen: true
      properties:
      - key: unlockTrainCarGuids
        type: array
        items:
          type: uuid
          foreignKey:
            schemaId: train
            foreignKeyIdPath: /trainCars/[*]/trainCarGuid
            displayElementPath: /trainCars/[*]/trainCarGuid
```

- [ ] **Step 5: SourceGenerator再生成＋コンパイル**

edit-schemaスキルの手順に従いSourceGeneratorを再生成し、コンパイルする:

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件。生成型`ConstructionRequiredItemElement`等が利用可能になる（生成名が想定と異なる場合はコンパイルエラーの型名から実名を確認し、以降のタスクで実名を使う）

- [ ] **Step 6: コミット**

```bash
git add VanillaSchema/
git commit -m "feat: ブロック建設コスト・ビルドメニュー・アンロック用のスキーマを追加"
```

---

### Task 2: テスト用マスタにrequiredItems / initialUnlockedを追加

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`

**Interfaces:**
- Produces: テストマスタ上の以下のデータ状態（後続タスクのテストが依存する）
  - TestBlock（blockGuid `00000000-0000-0000-0000-000000000002`）: requiredItems = Test3×2 + Test4×1、initialUnlocked = true
  - TestBeltConveyor（`...0003`）: requiredItems未定義のまま、initialUnlocked = true
  - TestElectricPole（`...0004`）: requiredItems = Test5×1、initialUnlocked = true
  - TestElectricMachine（`...0001`）: 何も追加しない（未解放ケースとして使用）

- [ ] **Step 1: blocks.jsonへデータ追加**

以下のPythonスクリプトを実行する:

```bash
python3 - <<'EOF'
import json
path = 'moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json'
data = json.load(open(path))

# ブロックGUID→追加フィールドの対応表
# Map of block guid to fields to add
additions = {
    '00000000-0000-0000-0000-000000000002': {  # TestBlock
        'requiredItems': [
            {'itemGuid': '00000000-0000-0000-1234-000000000003', 'count': 2},
            {'itemGuid': '00000000-0000-0000-1234-000000000004', 'count': 1},
        ],
        'initialUnlocked': True,
    },
    '00000000-0000-0000-0000-000000000003': {  # TestBeltConveyor
        'initialUnlocked': True,
    },
    '00000000-0000-0000-0000-000000000004': {  # TestElectricPole
        'requiredItems': [
            {'itemGuid': '00000000-0000-0000-1234-000000000005', 'count': 1},
        ],
        'initialUnlocked': True,
    },
}
for block in data['data']:
    if block['blockGuid'] in additions:
        block.update(additions[block['blockGuid']])

json.dump(data, open(path, 'w'), ensure_ascii=False, indent=4)
EOF
```

- [ ] **Step 2: 既存テストが壊れていないことを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceHotBarBlockProtocolTest|ElectricWireAutoConnectPlaceTest"`
Expected: 全件PASS（旧プロトコルはrequiredItemsを見ないため影響なし）

- [ ] **Step 3: コミット**

```bash
git add moorestech_server/Assets/Scripts/Tests.Module/
git commit -m "test: テスト用マスタに建設コストと初期解放フラグを追加"
```

---

### Task 3: PlaceInfo系DTOを共有ファイルへ移設

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlacePacketDto.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockFromHotBarProtocol.cs`（クラス内のDTO定義を削除）
- Modify: `PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack`等を修飾名で参照している全ファイル（grepで特定。既知: `RailConnectWithPlacePierProtocol` / `ElectricWireExtendProtocol` / `GearChainPoleExtendProtocol`（2026-07-05追加）とそのクライアント送信側）

**Interfaces:**
- Produces: namespace `Server.Protocol.PacketResponse`直下の`PlaceInfoMessagePack` / `BlockCreateParamMessagePack` / `PlaceInfo`（定義内容は現行と完全に同一。移動のみ）
- Consumes: なし

- [ ] **Step 1: 共有DTOファイルを作成**

`PlacePacketDto.cs`を作成し、`PlaceBlockFromHotBarProtocol.cs`の109〜146行目にある`PlaceInfoMessagePack`・`BlockCreateParamMessagePack`（クラス内ネスト定義）と149〜160行目の`PlaceInfo`を**そのまま**移動する:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using MessagePack;
using Newtonsoft.Json;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    [MessagePackObject]
    public class PlaceInfoMessagePack
    {
        [Key(0)] public Vector3IntMessagePack Position { get; set; }

        [Key(1)] public BlockDirection Direction { get; set; }

        [Key(2)] public BlockVerticalDirection VerticalDirection { get; set; }
        [Key(3)] public BlockCreateParamMessagePack[] BlockCreateParams { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PlaceInfoMessagePack() { }

        public PlaceInfoMessagePack(PlaceInfo placeInfo)
        {
            BlockCreateParams = placeInfo.CreateParams.Select(v => new BlockCreateParamMessagePack(v)).ToArray();
            Position = new Vector3IntMessagePack(placeInfo.Position);
            Direction = placeInfo.Direction;
            VerticalDirection = placeInfo.VerticalDirection;
        }
    }

    [MessagePackObject]
    public class BlockCreateParamMessagePack
    {
        [Key(0)] public string Key { get; set; }
        [Key(1)] public byte[] Value { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockCreateParamMessagePack() { }

        public BlockCreateParamMessagePack(BlockCreateParam param)
        {
            Key = param.Key;
            Value = param.Value;
        }
    }

    public class PlaceInfo
    {
        public Vector3Int Position { get; set; }
        public BlockDirection Direction { get; set; }
        public BlockVerticalDirection VerticalDirection { get; set; }

        public bool Placeable { get; set; }

        public BlockCreateParam[] CreateParams { get; set; } = Array.Empty<BlockCreateParam>();

        [JsonIgnore] public Dictionary<string, byte[]> CreateParamDictionary => CreateParams.ToDictionary(v => v.Key, v => v.Value);
    }
}
```

- [ ] **Step 2: 元ファイルからDTO定義を削除**

`PlaceBlockFromHotBarProtocol.cs`から`PlaceInfoMessagePack`クラス・`BlockCreateParamMessagePack`クラス・`PlaceInfo`クラスの定義を削除する（`SendPlaceHotBarBlockProtocolMessagePack`は残す）。

- [ ] **Step 3: 修飾参照を一括修正**

Run: `grep -rln "PlaceBlockFromHotBarProtocol\.\(PlaceInfoMessagePack\|BlockCreateParamMessagePack\|PlaceInfo\b\)" moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts`

ヒットした各ファイルで`PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack`→`PlaceInfoMessagePack`、`PlaceBlockFromHotBarProtocol.BlockCreateParamMessagePack`→`BlockCreateParamMessagePack`に置換する（同一namespaceのため修飾を外すだけでよい。namespace外のファイルは`using Server.Protocol.PacketResponse;`があることを確認）。

- [ ] **Step 4: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件（新規ファイルが認識されない場合はuloop-launchスキルでUnity再起動後に再実行）

- [ ] **Step 5: 既存テストの回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceHotBarBlockProtocolTest|RailConnect|ElectricWireExtend"`
Expected: 全件PASS（MessagePackのKey構成は不変のためシリアライズ互換）

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/ moorestech_client/
git commit -m "refactor: PlaceInfo系DTOをPlaceBlockFromHotBarProtocolから共有ファイルへ移設"
```

---

### Task 4: UnlockStateのホルダー分割とBlock/TrainCar追加

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/States/BlockUnlockStateInfo.cs`
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/States/TrainCarUnlockStateInfo.cs`
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/Holders/CraftRecipeUnlockStateHolder.cs`
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/Holders/ItemUnlockStateHolder.cs`
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/Holders/ChallengeCategoryUnlockStateHolder.cs`
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/Holders/MachineRecipeUnlockStateHolder.cs`
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/Holders/BlockUnlockStateHolder.cs`
- Create: `moorestech_server/Assets/Scripts/Game.UnlockState/Holders/TrainCarUnlockStateHolder.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.UnlockState/GameUnlockStateDatastoreController.cs`（ホルダー委譲に全面書き換え）
- Modify: `moorestech_server/Assets/Scripts/Game.UnlockState/IGameUnlockStateDatastoreController.cs`（Block/TrainCar追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlockUnlockStateTest.cs`（新規）

**Interfaces:**
- Consumes: Task 1の生成型（`BlockMasterElement.InitialUnlocked`, `TrainCarMasterElement.InitialUnlocked`）
- Produces:
  - `IGameUnlockStateData.BlockUnlockStateInfos` : `IReadOnlyDictionary<Guid, BlockUnlockStateInfo>`
  - `IGameUnlockStateData.TrainCarUnlockStateInfos` : `IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo>`
  - `IGameUnlockStateDataController.OnUnlockBlock` : `IObservable<Guid>` / `void UnlockBlock(Guid blockGuid)`
  - `IGameUnlockStateDataController.OnUnlockTrainCar` : `IObservable<Guid>` / `void UnlockTrainCar(Guid trainCarGuid)`
  - `GameUnlockStateJsonObject.BlockUnlockStateInfos` / `.TrainCarUnlockStateInfos`（セーブJSON拡張）

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Game/BlockUnlockStateTest.cs`:

```csharp
using System;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game
{
    public class BlockUnlockStateTest
    {
        private static readonly Guid TestBlockGuid = Guid.Parse("00000000-0000-0000-0000-000000000002");
        private static readonly Guid MachineBlockGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

        [Test]
        public void マスタのinitialUnlockedが初期状態に反映される()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsTrue(controller.BlockUnlockStateInfos[TestBlockGuid].IsUnlocked);
            Assert.IsFalse(controller.BlockUnlockStateInfos[MachineBlockGuid].IsUnlocked);
        }

        [Test]
        public void ブロック解放が保存とロードで維持される()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 解放イベントの発火も同時に検証する
            // Verify the unlock event also fires
            Guid unlockedGuid = default;
            controller.OnUnlockBlock.Subscribe(guid => unlockedGuid = guid);
            controller.UnlockBlock(MachineBlockGuid);

            Assert.AreEqual(MachineBlockGuid, unlockedGuid);
            Assert.IsTrue(controller.BlockUnlockStateInfos[MachineBlockGuid].IsUnlocked);

            // 別サーバーインスタンスにロードして状態が引き継がれるか
            // Load into another server instance and check the state carries over
            var saveJson = controller.GetSaveJsonObject();
            var (_, newServiceProvider) = CreateServer();
            var newController = newServiceProvider.GetService<IGameUnlockStateDataController>();
            newController.LoadUnlockState(saveJson);

            Assert.IsTrue(newController.BlockUnlockStateInfos[MachineBlockGuid].IsUnlocked);
            Assert.IsTrue(newController.BlockUnlockStateInfos[TestBlockGuid].IsUnlocked);
        }

        [Test]
        public void 列車車両の解放が保存とロードで維持される()
        {
            var (_, serviceProvider) = CreateServer();
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();

            // テストマスタの先頭車両を対象にする（initialUnlocked未設定なので初期ロック）
            // Use the first train car in the test master (locked initially)
            var trainCarGuid = Core.Master.MasterHolder.TrainUnitMaster.Train.TrainCars[0].TrainCarGuid;
            Assert.IsFalse(controller.TrainCarUnlockStateInfos[trainCarGuid].IsUnlocked);

            controller.UnlockTrainCar(trainCarGuid);
            var saveJson = controller.GetSaveJsonObject();

            var (_, newServiceProvider) = CreateServer();
            var newController = newServiceProvider.GetService<IGameUnlockStateDataController>();
            newController.LoadUnlockState(saveJson);

            Assert.IsTrue(newController.TrainCarUnlockStateInfos[trainCarGuid].IsUnlocked);
        }

        private static (Server.Protocol.PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
```

- [ ] **Step 2: テストが失敗（コンパイルエラー）することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BlockUnlockStateInfos`未定義等のコンパイルエラー

- [ ] **Step 3: StateInfo 2種を作成**

`Game.UnlockState/States/BlockUnlockStateInfo.cs`:

```csharp
using System;
using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class BlockUnlockStateInfo
    {
        public Guid BlockGuid { get; }
        public bool IsUnlocked { get; private set; }

        public BlockUnlockStateInfo(Guid blockGuid, bool isUnlocked)
        {
            BlockGuid = blockGuid;
            IsUnlocked = isUnlocked;
        }

        public BlockUnlockStateInfo(BlockUnlockStateInfoJsonObject jsonObject)
        {
            BlockGuid = Guid.Parse(jsonObject.BlockGuid);
            IsUnlocked = jsonObject.IsUnlocked;
        }

        public void Unlock()
        {
            IsUnlocked = true;
        }
    }

    public class BlockUnlockStateInfoJsonObject
    {
        [JsonProperty("guid")] public string BlockGuid;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;

        public BlockUnlockStateInfoJsonObject() { }

        public BlockUnlockStateInfoJsonObject(BlockUnlockStateInfo blockUnlockStateInfo)
        {
            BlockGuid = blockUnlockStateInfo.BlockGuid.ToString();
            IsUnlocked = blockUnlockStateInfo.IsUnlocked;
        }
    }
}
```

`Game.UnlockState/States/TrainCarUnlockStateInfo.cs`:

```csharp
using System;
using Newtonsoft.Json;

namespace Game.UnlockState.States
{
    public class TrainCarUnlockStateInfo
    {
        public Guid TrainCarGuid { get; }
        public bool IsUnlocked { get; private set; }

        public TrainCarUnlockStateInfo(Guid trainCarGuid, bool isUnlocked)
        {
            TrainCarGuid = trainCarGuid;
            IsUnlocked = isUnlocked;
        }

        public TrainCarUnlockStateInfo(TrainCarUnlockStateInfoJsonObject jsonObject)
        {
            TrainCarGuid = Guid.Parse(jsonObject.TrainCarGuid);
            IsUnlocked = jsonObject.IsUnlocked;
        }

        public void Unlock()
        {
            IsUnlocked = true;
        }
    }

    public class TrainCarUnlockStateInfoJsonObject
    {
        [JsonProperty("guid")] public string TrainCarGuid;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;

        public TrainCarUnlockStateInfoJsonObject() { }

        public TrainCarUnlockStateInfoJsonObject(TrainCarUnlockStateInfo trainCarUnlockStateInfo)
        {
            TrainCarGuid = trainCarUnlockStateInfo.TrainCarGuid.ToString();
            IsUnlocked = trainCarUnlockStateInfo.IsUnlocked;
        }
    }
}
```

- [ ] **Step 4: ホルダー6種を作成**

各ホルダーは「マスタから初期化・Unlock・Load・セーブJSON生成」を持つ小さなクラス。既存コントローラのドメイン別ロジックをそのまま移す（Unlock時のガード挙動は現行を維持: CraftRecipe/Itemは辞書直indexで存在前提、Challenge/MachineRecipeと新規のBlock/TrainCarは未存在キーをLogErrorで弾く）。

`Game.UnlockState/Holders/CraftRecipeUnlockStateHolder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;

namespace Game.UnlockState.Holders
{
    public class CraftRecipeUnlockStateHolder
    {
        public IObservable<Guid> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> Infos => _infos;

        private readonly Subject<Guid> _onUnlock = new();
        private readonly Dictionary<Guid, CraftRecipeUnlockStateInfo> _infos = new();

        public CraftRecipeUnlockStateHolder()
        {
            // マスタの全クラフトレシピを初期解放フラグ付きで登録
            // Register all craft recipes with their initial unlocked flag
            foreach (var recipe in MasterHolder.CraftRecipeMaster.GetAllCraftRecipes())
            {
                if (_infos.ContainsKey(recipe.CraftRecipeGuid)) continue;
                _infos.Add(recipe.CraftRecipeGuid, new CraftRecipeUnlockStateInfo(recipe.CraftRecipeGuid, recipe.InitialUnlocked));
            }
        }

        public void Unlock(Guid recipeGuid)
        {
            _infos[recipeGuid].Unlock();
            _onUnlock.OnNext(recipeGuid);
        }

        public void Load(List<CraftRecipeUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                var state = new CraftRecipeUnlockStateInfo(jsonObject);
                _infos[state.CraftRecipeGuid] = state;
            }
        }

        public List<CraftRecipeUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new CraftRecipeUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}
```

`Game.UnlockState/Holders/ItemUnlockStateHolder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;

namespace Game.UnlockState.Holders
{
    public class ItemUnlockStateHolder
    {
        public IObservable<ItemId> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> Infos => _infos;

        private readonly Subject<ItemId> _onUnlock = new();
        private readonly Dictionary<ItemId, ItemUnlockStateInfo> _infos = new();

        public ItemUnlockStateHolder()
        {
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                if (_infos.ContainsKey(itemId)) continue;
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                _infos.Add(itemId, new ItemUnlockStateInfo(itemId, itemMaster.InitialUnlocked));
            }
        }

        public void Unlock(ItemId itemId)
        {
            _infos[itemId].Unlock();
            _onUnlock.OnNext(itemId);
        }

        public void Load(List<ItemUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                // マスタに存在しないアイテムはスキップ
                // Skip items that don't exist in master
                if (!MasterHolder.ItemMaster.ExistItemId(Guid.Parse(jsonObject.ItemGuid))) continue;
                var state = new ItemUnlockStateInfo(jsonObject);
                _infos[state.ItemId] = state;
            }
        }

        public List<ItemUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new ItemUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}
```

`Game.UnlockState/Holders/ChallengeCategoryUnlockStateHolder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;
using UnityEngine;

namespace Game.UnlockState.Holders
{
    public class ChallengeCategoryUnlockStateHolder
    {
        public IObservable<Guid> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> Infos => _infos;

        private readonly Subject<Guid> _onUnlock = new();
        private readonly Dictionary<Guid, ChallengeCategoryUnlockStateInfo> _infos = new();

        public ChallengeCategoryUnlockStateHolder()
        {
            foreach (var challenge in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements)
            {
                if (_infos.ContainsKey(challenge.CategoryGuid)) continue;
                _infos.Add(challenge.CategoryGuid, new ChallengeCategoryUnlockStateInfo(challenge.CategoryGuid, challenge.InitialUnlocked));
            }
        }

        public void Unlock(Guid categoryGuid)
        {
            if (!_infos.ContainsKey(categoryGuid))
            {
                Debug.LogError($"[UnlockChallenge] Challenge category not found: {categoryGuid}");
                return;
            }
            _infos[categoryGuid].Unlock();
            _onUnlock.OnNext(categoryGuid);
        }

        public void Load(List<ChallengeUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                var state = new ChallengeCategoryUnlockStateInfo(jsonObject);
                _infos[state.ChallengeCategoryGuid] = state;
            }
        }

        public List<ChallengeUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new ChallengeUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}
```

`Game.UnlockState/Holders/MachineRecipeUnlockStateHolder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;
using UnityEngine;

namespace Game.UnlockState.Holders
{
    public class MachineRecipeUnlockStateHolder
    {
        public IObservable<Guid> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> Infos => _infos;

        private readonly Subject<Guid> _onUnlock = new();
        private readonly Dictionary<Guid, MachineRecipeUnlockStateInfo> _infos = new();

        public MachineRecipeUnlockStateHolder()
        {
            foreach (var machineRecipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (_infos.ContainsKey(machineRecipe.MachineRecipeGuid)) continue;
                _infos.Add(machineRecipe.MachineRecipeGuid, new MachineRecipeUnlockStateInfo(machineRecipe.MachineRecipeGuid, machineRecipe.InitialUnlocked));
            }
        }

        public void Unlock(Guid machineRecipeGuid)
        {
            if (!_infos.ContainsKey(machineRecipeGuid))
            {
                Debug.LogError($"[UnlockMachineRecipe] Machine recipe not found: {machineRecipeGuid}");
                return;
            }
            _infos[machineRecipeGuid].Unlock();
            _onUnlock.OnNext(machineRecipeGuid);
        }

        public void Load(List<MachineRecipeUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                var state = new MachineRecipeUnlockStateInfo(jsonObject);
                _infos[state.MachineRecipeGuid] = state;
            }
        }

        public List<MachineRecipeUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new MachineRecipeUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}
```

`Game.UnlockState/Holders/BlockUnlockStateHolder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;
using UnityEngine;

namespace Game.UnlockState.Holders
{
    public class BlockUnlockStateHolder
    {
        public IObservable<Guid> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> Infos => _infos;

        private readonly Subject<Guid> _onUnlock = new();
        private readonly Dictionary<Guid, BlockUnlockStateInfo> _infos = new();

        public BlockUnlockStateHolder()
        {
            foreach (var block in MasterHolder.BlockMaster.Blocks.Data)
            {
                if (_infos.ContainsKey(block.BlockGuid)) continue;
                _infos.Add(block.BlockGuid, new BlockUnlockStateInfo(block.BlockGuid, block.InitialUnlocked));
            }
        }

        public void Unlock(Guid blockGuid)
        {
            if (!_infos.ContainsKey(blockGuid))
            {
                Debug.LogError($"[UnlockBlock] Block not found: {blockGuid}");
                return;
            }
            _infos[blockGuid].Unlock();
            _onUnlock.OnNext(blockGuid);
        }

        public void Load(List<BlockUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                // マスタに存在しないブロックはスキップ
                // Skip blocks that don't exist in master
                if (MasterHolder.BlockMaster.GetBlockIdOrNull(Guid.Parse(jsonObject.BlockGuid)) == null) continue;
                var state = new BlockUnlockStateInfo(jsonObject);
                _infos[state.BlockGuid] = state;
            }
        }

        public List<BlockUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new BlockUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}
```

`Game.UnlockState/Holders/TrainCarUnlockStateHolder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState.States;
using UniRx;
using UnityEngine;

namespace Game.UnlockState.Holders
{
    public class TrainCarUnlockStateHolder
    {
        public IObservable<Guid> OnUnlock => _onUnlock;
        public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> Infos => _infos;

        private readonly Subject<Guid> _onUnlock = new();
        private readonly Dictionary<Guid, TrainCarUnlockStateInfo> _infos = new();

        public TrainCarUnlockStateHolder()
        {
            foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
            {
                if (_infos.ContainsKey(trainCar.TrainCarGuid)) continue;
                _infos.Add(trainCar.TrainCarGuid, new TrainCarUnlockStateInfo(trainCar.TrainCarGuid, trainCar.InitialUnlocked));
            }
        }

        public void Unlock(Guid trainCarGuid)
        {
            if (!_infos.ContainsKey(trainCarGuid))
            {
                Debug.LogError($"[UnlockTrainCar] Train car not found: {trainCarGuid}");
                return;
            }
            _infos[trainCarGuid].Unlock();
            _onUnlock.OnNext(trainCarGuid);
        }

        public void Load(List<TrainCarUnlockStateInfoJsonObject> jsonObjects)
        {
            if (jsonObjects == null) return;
            foreach (var jsonObject in jsonObjects)
            {
                var state = new TrainCarUnlockStateInfo(jsonObject);
                _infos[state.TrainCarGuid] = state;
            }
        }

        public List<TrainCarUnlockStateInfoJsonObject> GetSaveJsonObject()
        {
            return _infos.Values.Select(i => new TrainCarUnlockStateInfoJsonObject(i)).ToList();
        }
    }
}
```

- [ ] **Step 5: インターフェースにBlock/TrainCarを追加**

`IGameUnlockStateDatastoreController.cs`の`IGameUnlockStateData`に追加:

```csharp
        public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; }
        public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; }
```

`IGameUnlockStateDataController`に追加:

```csharp
        public IObservable<Guid> OnUnlockBlock { get; }
        void UnlockBlock(Guid blockGuid);

        public IObservable<Guid> OnUnlockTrainCar { get; }
        void UnlockTrainCar(Guid trainCarGuid);
```

- [ ] **Step 6: コントローラをホルダー委譲に書き換え**

`GameUnlockStateDatastoreController.cs`全体を以下に置き換える（クラス名`GameUnlockStateDataController`とセーブJSONキーは現行維持。既存の`BackfillMachineRecipeUnlockStateInfos`はコンストラクタ初期化が同じ役割を果たすため廃止）:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.UnlockState.Holders;
using Game.UnlockState.States;
using Newtonsoft.Json;

namespace Game.UnlockState
{
    public class GameUnlockStateDataController : IGameUnlockStateDataController
    {
        // ドメイン別ホルダーに解放状態の管理を委譲する
        // Delegate unlock state management to per-domain holders
        private readonly CraftRecipeUnlockStateHolder _craftRecipe = new();
        private readonly ItemUnlockStateHolder _item = new();
        private readonly ChallengeCategoryUnlockStateHolder _challengeCategory = new();
        private readonly MachineRecipeUnlockStateHolder _machineRecipe = new();
        private readonly BlockUnlockStateHolder _block = new();
        private readonly TrainCarUnlockStateHolder _trainCar = new();

        public IObservable<Guid> OnUnlockCraftRecipe => _craftRecipe.OnUnlock;
        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos => _craftRecipe.Infos;
        public void UnlockCraftRecipe(Guid recipeGuid) => _craftRecipe.Unlock(recipeGuid);

        public IObservable<ItemId> OnUnlockItem => _item.OnUnlock;
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos => _item.Infos;
        public void UnlockItem(ItemId itemId) => _item.Unlock(itemId);

        public IObservable<Guid> OnUnlockChallengeCategory => _challengeCategory.OnUnlock;
        public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos => _challengeCategory.Infos;
        public void UnlockChallenge(Guid categoryGuid) => _challengeCategory.Unlock(categoryGuid);

        public IObservable<Guid> OnUnlockMachineRecipe => _machineRecipe.OnUnlock;
        public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos => _machineRecipe.Infos;
        public void UnlockMachineRecipe(Guid machineRecipeGuid) => _machineRecipe.Unlock(machineRecipeGuid);

        public IObservable<Guid> OnUnlockBlock => _block.OnUnlock;
        public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos => _block.Infos;
        public void UnlockBlock(Guid blockGuid) => _block.Unlock(blockGuid);

        public IObservable<Guid> OnUnlockTrainCar => _trainCar.OnUnlock;
        public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos => _trainCar.Infos;
        public void UnlockTrainCar(Guid trainCarGuid) => _trainCar.Unlock(trainCarGuid);

        #region SaveLoad

        public void LoadUnlockState(GameUnlockStateJsonObject stateJsonObject)
        {
            _craftRecipe.Load(stateJsonObject.CraftRecipeUnlockStateInfos);
            _item.Load(stateJsonObject.ItemUnlockStateInfos);
            _challengeCategory.Load(stateJsonObject.ChallengeCategoryUnlockStateInfos);
            _machineRecipe.Load(stateJsonObject.MachineRecipeUnlockStateInfos);
            _block.Load(stateJsonObject.BlockUnlockStateInfos);
            _trainCar.Load(stateJsonObject.TrainCarUnlockStateInfos);
        }

        public GameUnlockStateJsonObject GetSaveJsonObject()
        {
            return new GameUnlockStateJsonObject
            {
                CraftRecipeUnlockStateInfos = _craftRecipe.GetSaveJsonObject(),
                ItemUnlockStateInfos = _item.GetSaveJsonObject(),
                ChallengeCategoryUnlockStateInfos = _challengeCategory.GetSaveJsonObject(),
                MachineRecipeUnlockStateInfos = _machineRecipe.GetSaveJsonObject(),
                BlockUnlockStateInfos = _block.GetSaveJsonObject(),
                TrainCarUnlockStateInfos = _trainCar.GetSaveJsonObject(),
            };
        }

        #endregion
    }

    public class GameUnlockStateJsonObject
    {
        [JsonProperty("craftRecipeUnlockStateInfos")] public List<CraftRecipeUnlockStateInfoJsonObject> CraftRecipeUnlockStateInfos;
        [JsonProperty("itemUnlockStateInfos")] public List<ItemUnlockStateInfoJsonObject> ItemUnlockStateInfos;
        [JsonProperty("challengeCategoryUnlockStateInfos")] public List<ChallengeUnlockStateInfoJsonObject> ChallengeCategoryUnlockStateInfos;
        [JsonProperty("machineRecipeUnlockStateInfos")] public List<MachineRecipeUnlockStateInfoJsonObject> MachineRecipeUnlockStateInfos;
        [JsonProperty("blockUnlockStateInfos")] public List<BlockUnlockStateInfoJsonObject> BlockUnlockStateInfos;
        [JsonProperty("trainCarUnlockStateInfos")] public List<TrainCarUnlockStateInfoJsonObject> TrainCarUnlockStateInfos;
    }
}
```

- [ ] **Step 7: コンパイル＋テスト実行**

Run: `uloop compile --project-path ./moorestech_client`（新規ファイル未認識ならuloop-launchでUnity再起動）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockUnlockStateTest"`
Expected: 3件PASS

- [ ] **Step 8: アンロック関連の既存テスト回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Unlock|Challenge|Research"`
Expected: 全件PASS（リファクタで挙動不変のこと）

- [ ] **Step 9: コミット**

```bash
git add moorestech_server/
git commit -m "feat: アンロック状態をホルダー分割しブロック・列車車両の解放状態を追加"
```

---

### Task 5: UnlockedEventPacketとGetGameUnlockStateProtocolの拡張

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/UnlockedEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetGameUnlockStateProtocol.cs`

**Interfaces:**
- Consumes: Task 4の`OnUnlockBlock` / `OnUnlockTrainCar` / `BlockUnlockStateInfos` / `TrainCarUnlockStateInfos`
- Produces:
  - `UnlockEventType.Block` / `UnlockEventType.TrainCar`（enum追加）、`UnlockEventMessagePack.UnlockedBlockGuidStr`(Key 5) / `.UnlockedTrainCarGuidStr`(Key 6)
  - `ResponseGameUnlockStateProtocolMessagePack.LockedBlockGuidsStr`(Key 10) / `.UnlockedBlockGuidsStr`(Key 11) / `.LockedTrainCarGuidsStr`(Key 12) / `.UnlockedTrainCarGuidsStr`(Key 13)（クライアントはプラン3で消費）

- [ ] **Step 1: UnlockedEventPacketにBlock/TrainCarを追加**

`UnlockedEventPacket.cs`に以下の変更を加える:

コンストラクタ末尾に購読を2行追加:

```csharp
            unlockState.OnUnlockBlock.Subscribe(b => AddBroadcastEvent(new UnlockEventMessagePack(UnlockEventType.Block, b)));
            unlockState.OnUnlockTrainCar.Subscribe(t => AddBroadcastEvent(new UnlockEventMessagePack(UnlockEventType.TrainCar, t)));
```

`UnlockEventMessagePack`にアクセサとフィールドを追加:

```csharp
        [IgnoreMember] public Guid UnlockedBlockGuid => Guid.Parse(UnlockedBlockGuidStr);
        [IgnoreMember] public Guid UnlockedTrainCarGuid => Guid.Parse(UnlockedTrainCarGuidStr);

        [Key(5)] public string UnlockedBlockGuidStr { get; set; }
        [Key(6)] public string UnlockedTrainCarGuidStr { get; set; }
```

`UnlockEventMessagePack(UnlockEventType, Guid)`コンストラクタのswitchにケースを追加:

```csharp
                case UnlockEventType.Block:
                    UnlockedBlockGuidStr = guid.ToString();
                    break;
                case UnlockEventType.TrainCar:
                    UnlockedTrainCarGuidStr = guid.ToString();
                    break;
```

enumに追加:

```csharp
    public enum UnlockEventType
    {
        CraftRecipe,
        Item,
        ChallengeCategory,
        MachineRecipe,
        Block,
        TrainCar,
    }
```

- [ ] **Step 2: GetGameUnlockStateProtocolにBlock/TrainCarを追加**

`GetResponse`内、機械レシピの集計の後に追加:

```csharp
            // ブロックと列車車両のアンロック状態を取得
            // Get block and train car unlock states
            var lockedBlock = new List<string>();
            var unlockedBlock = new List<string>();
            foreach (var block in gameUnlockStateData.BlockUnlockStateInfos.Values)
            {
                if (block.IsUnlocked) unlockedBlock.Add(block.BlockGuid.ToString());
                else lockedBlock.Add(block.BlockGuid.ToString());
            }

            var lockedTrainCar = new List<string>();
            var unlockedTrainCar = new List<string>();
            foreach (var trainCar in gameUnlockStateData.TrainCarUnlockStateInfos.Values)
            {
                if (trainCar.IsUnlocked) unlockedTrainCar.Add(trainCar.TrainCarGuid.ToString());
                else lockedTrainCar.Add(trainCar.TrainCarGuid.ToString());
            }
```

`ResponseGameUnlockStateProtocolMessagePack`にアクセサ・フィールドを追加:

```csharp
            [IgnoreMember] public List<Guid> UnlockedBlockGuids => UnlockedBlockGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedBlockGuids => LockedBlockGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> UnlockedTrainCarGuids => UnlockedTrainCarGuidsStr.Select(Guid.Parse).ToList();
            [IgnoreMember] public List<Guid> LockedTrainCarGuids => LockedTrainCarGuidsStr.Select(Guid.Parse).ToList();

            [Key(10)] public List<string> LockedBlockGuidsStr { get; set; }
            [Key(11)] public List<string> UnlockedBlockGuidsStr { get; set; }
            [Key(12)] public List<string> LockedTrainCarGuidsStr { get; set; }
            [Key(13)] public List<string> UnlockedTrainCarGuidsStr { get; set; }
```

コンストラクタの引数末尾に4引数を追加し代入する（呼び出し側の`GetResponse`も合わせて修正）:

```csharp
            public ResponseGameUnlockStateProtocolMessagePack(
                List<string> unlockedCraftRecipeGuidsStr, List<string> lockedCraftRecipeGuidsStr,
                List<int> lockedItemIds, List<int> unlockedItemIds,
                List<string> lockedChallengeCategoryGuidsStr, List<string> unlockedChallengeCategoryGuidsStr,
                List<string> lockedMachineRecipeGuidsStr, List<string> unlockedMachineRecipeGuidsStr,
                List<string> lockedBlockGuidsStr, List<string> unlockedBlockGuidsStr,
                List<string> lockedTrainCarGuidsStr, List<string> unlockedTrainCarGuidsStr)
```

（既存代入の下に4行追加: `LockedBlockGuidsStr = lockedBlockGuidsStr;` 等）

- [ ] **Step 3: コンパイル＋回帰テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UnlockState|BlockUnlockStateTest"`
Expected: 全件PASS

- [ ] **Step 4: コミット**

```bash
git add moorestech_server/
git commit -m "feat: ブロック・列車車両のアンロックをイベントと状態同期プロトコルに追加"
```

---

### Task 6: GameActionExecutorにunlockBlock / unlockTrainCarを追加

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Action/GameActionExecutor.cs`

**Interfaces:**
- Consumes: Task 1の生成型（`UnlockBlockGameActionParam` / `UnlockTrainCarGameActionParam`、`GameActionTypeConst.unlockBlock` / `.unlockTrainCar`）、Task 4の`UnlockBlock` / `UnlockTrainCar`
- Produces: research/challengesのclearedActionsで`unlockBlock` / `unlockTrainCar`が実行可能になる（実データはプラン2で投入）

- [ ] **Step 1: ExecuteUnlockActionsのフィルタに2ケース追加**

`ExecuteUnlockActions`内のswitchに追加:

```csharp
                    case GameActionElement.GameActionTypeConst.unlockBlock:
                    case GameActionElement.GameActionTypeConst.unlockTrainCar:
```

（既存の`unlockMachineRecipe`ケース行の直後、`ExecuteAction(action, context); break;`の前に挿入）

- [ ] **Step 2: ExecuteActionに実行ケースを追加**

switchにケース追加:

```csharp
                case GameActionElement.GameActionTypeConst.unlockBlock:
                    UnlockBlock();
                    break;

                case GameActionElement.GameActionTypeConst.unlockTrainCar:
                    UnlockTrainCar();
                    break;
```

`#region Internal`内にローカル関数を追加（`UnlockMachineRecipe()`の直後）:

```csharp
            void UnlockBlock()
            {
                var blockGuids = ((UnlockBlockGameActionParam)action.GameActionParam).UnlockBlockGuids;
                foreach (var guid in blockGuids)
                {
                    _gameUnlockStateDataController.UnlockBlock(guid);
                }
            }

            void UnlockTrainCar()
            {
                var trainCarGuids = ((UnlockTrainCarGameActionParam)action.GameActionParam).UnlockTrainCarGuids;
                foreach (var guid in trainCarGuids)
                {
                    _gameUnlockStateDataController.UnlockTrainCar(guid);
                }
            }
```

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件（生成パラメータ型名が異なる場合はコンパイルエラーの型名に合わせて修正）

- [ ] **Step 4: コミット**

```bash
git add moorestech_server/
git commit -m "feat: gameActionにunlockBlock・unlockTrainCarの実行を追加"
```

---

### Task 7: ConstructionCostService（建設コストの検証・消費・返却）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/ConstructionCostService.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ConstructionCostServiceTest.cs`

**Interfaces:**
- Consumes: Task 1の`ConstructionRequiredItemElement`（`.ItemGuid` / `.Count`）、Task 2のTestBlockマスタデータ
- Produces:
  - `static bool ConstructionCostService.HasRequiredItems(ConstructionRequiredItemElement[] requiredItems, IReadOnlyList<IItemStack> inventoryItems)`
  - `static void ConstructionCostService.ConsumeRequiredItems(ConstructionRequiredItemElement[] requiredItems, IOpenableInventory inventory)`
  - `static List<IItemStack> ConstructionCostService.CreateRefundItems(ConstructionRequiredItemElement[] requiredItems)`

- [ ] **Step 1: 失敗するテストを書く**

`Tests/UnitTest/Server/ConstructionCostServiceTest.cs`:

```csharp
using System;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.Construction;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Server
{
    public class ConstructionCostServiceTest
    {
        private const int PlayerId = 1;

        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4

        // TestBlockのrequiredItems = Test3×2 + Test4×1 をコスト定義として使う
        // Use TestBlock's requiredItems (Test3 x2 + Test4 x1) as the cost definition

        [Test]
        public void 所持数が足りればHasRequiredItemsはtrue()
        {
            var serviceProvider = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;

            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 2));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 1));

            Assert.IsTrue(ConstructionCostService.HasRequiredItems(requiredItems, inventory.InventoryItems));
        }

        [Test]
        public void 一部素材が不足していればHasRequiredItemsはfalse()
        {
            var serviceProvider = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;

            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 1));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 1));

            Assert.IsFalse(ConstructionCostService.HasRequiredItems(requiredItems, inventory.InventoryItems));
        }

        [Test]
        public void ConsumeRequiredItemsは複数スロットにまたがって減算する()
        {
            var serviceProvider = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var material1Id = MasterHolder.ItemMaster.GetItemId(Material1Guid);
            var material2Id = MasterHolder.ItemMaster.GetItemId(Material2Guid);

            // Test3を2スロットに分割して配置し、先頭スロットから消費されることを確認する
            // Split Test3 across two slots and verify consumption starts from the first slot
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(material1Id, 1));
            inventory.SetItem(5, ServerContext.ItemStackFactory.Create(material1Id, 3));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(material2Id, 2));

            ConstructionCostService.ConsumeRequiredItems(requiredItems, inventory);

            Assert.AreEqual(0, inventory.GetItem(0).Count);
            Assert.AreEqual(2, inventory.GetItem(5).Count);
            Assert.AreEqual(1, inventory.GetItem(1).Count);
        }

        [Test]
        public void CreateRefundItemsはコスト全額のスタックを返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;

            var refundItems = ConstructionCostService.CreateRefundItems(requiredItems);

            Assert.AreEqual(2, refundItems.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(Material1Guid), refundItems[0].Id);
            Assert.AreEqual(2, refundItems[0].Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(Material2Guid), refundItems[1].Id);
            Assert.AreEqual(1, refundItems[1].Count);
        }

        private static ServiceProvider CreateServer()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider;
        }

        private static Core.Inventory.IOpenableInventory GetInventory(ServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
        }
    }
}
```

- [ ] **Step 2: テストが失敗（コンパイルエラー）することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ConstructionCostService`未定義のコンパイルエラー

- [ ] **Step 3: 実装を書く**

`Server.Protocol/PacketResponse/Util/Construction/ConstructionCostService.cs`:

```csharp
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 建設コスト(requiredItems)の検証・消費・返却スタック生成を行う
    /// Validates, consumes, and creates refund stacks for construction costs (requiredItems)
    /// </summary>
    public static class ConstructionCostService
    {
        public static bool HasRequiredItems(ConstructionRequiredItemElement[] requiredItems, IReadOnlyList<IItemStack> inventoryItems)
        {
            if (requiredItems == null || requiredItems.Length == 0) return true;

            // 素材ごとにインベントリ全スロットの合計所持数を数える
            // Sum held counts across all inventory slots per material
            foreach (var requiredItem in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                var total = 0;
                foreach (var stack in inventoryItems)
                {
                    if (stack.Id != itemId) continue;
                    total += stack.Count;
                }
                if (total < requiredItem.Count) return false;
            }

            return true;
        }

        public static void ConsumeRequiredItems(ConstructionRequiredItemElement[] requiredItems, IOpenableInventory inventory)
        {
            if (requiredItems == null || requiredItems.Length == 0) return;

            // 先頭スロットから順に減算する共通処理（電線消費と同一実装）を再利用する
            // Reuse the shared first-slot-onward consumption logic used by wire consumption
            foreach (var requiredItem in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                ElectricWireSystemUtil.ConsumeItem(inventory, itemId, requiredItem.Count);
            }
        }

        public static List<IItemStack> CreateRefundItems(ConstructionRequiredItemElement[] requiredItems)
        {
            var result = new List<IItemStack>();
            if (requiredItems == null) return result;

            foreach (var requiredItem in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                result.Add(ServerContext.ItemStackFactory.Create(itemId, requiredItem.Count));
            }

            return result;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop compile --project-path ./moorestech_client`（新規ファイル未認識ならUnity再起動）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConstructionCostServiceTest"`
Expected: 4件PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/
git commit -m "feat: 建設コストの検証・消費・返却を行うConstructionCostServiceを追加"
```

---

### Task 8: 新PlaceBlockProtocol

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`（登録追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTest.cs`

**Interfaces:**
- Consumes: Task 3の`PlaceInfoMessagePack` / `PlaceInfo`、Task 4の`BlockUnlockStateInfos`、Task 7の`ConstructionCostService`、既存の`ElectricWireAutoConnectService.EvaluateAutoConnect / ExecuteAutoConnect`（EvaluateはStep 3で予約リスト引数に一般化する）
- Produces: プロトコル`va:placeBlock`、`PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(int playerId, BlockId blockId, List<PlaceInfo> placeInfos)`（プラン3でクライアントが送信に使う）

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTest.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PlaceBlockProtocolTest
    {
        private const int PlayerId = 3;

        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3 (TestBlockコスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4 (TestBlockコスト×1)
        private static readonly Guid PoleMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000005"); // Test5 (電柱コスト×1)
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-4649-000000000001"); // TestElectricWire

        [Test]
        public void 建設コストを消費して設置される()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);

            SetItem(inventory, 0, Material1Guid, 5);
            SetItem(inventory, 1, Material2Guid, 3);

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (2, 4)), new PacketResponseContext());

            Assert.AreEqual(ForUnitTestModBlockId.BlockId, ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(2, 4)).BlockId);
            Assert.AreEqual(3, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(2, GetItemCount(inventory, Material2Guid));
        }

        [Test]
        public void 素材不足のセルはスキップされ賄える分だけ設置される()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);

            // コストはセルあたりTest3×2+Test4×1。素材は2セル分しかない
            // Cost per cell is Test3 x2 + Test4 x1; materials cover only two cells
            SetItem(inventory, 0, Material1Guid, 5);
            SetItem(inventory, 1, Material2Guid, 2);

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (10, 0), (11, 0), (12, 0)), new PacketResponseContext());

            var world = ServerContext.WorldBlockDatastore;
            Assert.IsTrue(world.Exists(new Vector3Int(10, 0)));
            Assert.IsTrue(world.Exists(new Vector3Int(11, 0)));
            Assert.IsFalse(world.Exists(new Vector3Int(12, 0)));
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(0, GetItemCount(inventory, Material2Guid));
        }

        [Test]
        public void 未解放ブロックは設置されない()
        {
            var (packet, serviceProvider) = CreateServer();
            GetInventory(serviceProvider);

            // TestElectricMachineはinitialUnlocked未設定（=ロック中）かつコスト未定義
            // TestElectricMachine is locked (no initialUnlocked) and has no cost
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.MachineId, (5, 5)), new PacketResponseContext());

            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(5, 5)));
        }

        [Test]
        public void requiredItems未定義かつ解放済みなら無償で設置される()
        {
            var (packet, serviceProvider) = CreateServer();
            GetInventory(serviceProvider);

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BeltConveyorId, (6, 6)), new PacketResponseContext());

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(6, 6)));
        }

        [Test]
        public void 既存ブロックと重なる場合は素材を消費しない()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);

            SetItem(inventory, 0, Material1Guid, 4);
            SetItem(inventory, 1, Material2Guid, 2);

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (7, 7)), new PacketResponseContext());
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (7, 7)), new PacketResponseContext());

            Assert.AreEqual(2, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
        }

        [Test]
        public void 電柱設置で自動接続の電線と建設コストが同時に消費される()
        {
            var (packet, serviceProvider) = CreateServer();
            var world = ServerContext.WorldBlockDatastore;

            // 距離1の位置に未接続機械を先に置いてから電柱をプロトコルで設置する
            // Pre-place an unconnected machine at distance 1, then place a pole via the protocol
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            var inventory = GetInventory(serviceProvider);
            SetItem(inventory, 0, PoleMaterialGuid, 2);
            SetItem(inventory, 1, WireItemGuid, 5);

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.ElectricPoleId, (0, 0)), new PacketResponseContext());

            var pole = world.GetBlock(new Vector3Int(0, 0, 0));
            Assert.IsNotNull(pole);
            Assert.IsTrue(pole.GetComponent<IElectricWireConnector>().ContainsWireConnection(machine.GetComponent<IElectricWireConnector>().BlockInstanceId));
            Assert.AreEqual(1, GetItemCount(inventory, PoleMaterialGuid));
            Assert.AreEqual(4, GetItemCount(inventory, WireItemGuid));
        }

        #region TestUtil

        private static (PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static IOpenableInventory GetInventory(ServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
        }

        private static void SetItem(IOpenableInventory inventory, int slot, Guid itemGuid, int count)
        {
            inventory.SetItem(slot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(itemGuid), count));
        }

        private static int GetItemCount(IOpenableInventory inventory, Guid itemGuid)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
            var total = 0;
            foreach (var stack in inventory.InventoryItems)
            {
                if (stack.Id != itemId) continue;
                total += stack.Count;
            }
            return total;
        }

        private static byte[] CreatePlaceBlockPayload(BlockId blockId, params (int x, int y)[] positions)
        {
            var placeInfos = new List<PlaceInfo>();
            foreach (var (x, y) in positions)
            {
                placeInfos.Add(new PlaceInfo
                {
                    Position = new Vector3Int(x, y),
                    Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                });
            }
            return MessagePackSerializer.Serialize(new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(PlayerId, blockId, placeInfos));
        }

        #endregion
    }
}
```

注意: 電柱テストのセル座標は`(0, 0)`＝`Vector3Int(0, 0, 0)`。機械は`(1, 0, 0)`なので距離1、テストマスタの電線`consumptionPerLength`は1のため電線消費は1本。

- [ ] **Step 2: テストが失敗（コンパイルエラー）することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PlaceBlockProtocol`未定義のコンパイルエラー

- [ ] **Step 3: EvaluateAutoConnectの予約引数を一般化**

2026-07-05の電線改修で`EvaluateAutoConnect`に`ItemId placingItemId`引数（設置アイテム自身が電線を兼ねる場合に+1予約する）が追加された。新プロトコルには保持アイテムが存在しないため、この予約を「予約アイテムリスト」に一般化する。

`Util/ElectricWire/ElectricWireAutoConnectService.cs`のシグネチャを変更:

```csharp
        public static ElectricWireAutoConnectPlan EvaluateAutoConnect(BlockId blockId, Vector3Int position, BlockDirection direction, IReadOnlyList<(ItemId itemId, int count)> reservedItems, IReadOnlyList<IItemStack> inventoryItems)
```

`TrySelectWireItem`内の`requiredTotal`算出を変更:

```csharp
                    // 建設コスト等で予約済みの数量を上乗せして所持数を判定する
                    // Add quantities reserved by construction costs when judging held counts
                    var reservedCount = 0;
                    foreach (var reserved in reservedItems)
                    {
                        if (reserved.itemId == candidateItemId) reservedCount += reserved.count;
                    }
                    var requiredTotal = totalRequired + reservedCount;
                    if (!HasEnoughItem(candidateItemId, requiredTotal)) continue;
```

既存呼び出し元2箇所を更新する:
- `PlaceBlockFromHotBarProtocol.cs`（67行目付近）: `item.Id` → `new[] { (item.Id, 1) }`
- `Util/ElectricWire/ElectricWireExtendService.cs`（134行目付近）: `inventory.GetItem(poleInventorySlot).Id` → `new[] { (inventory.GetItem(poleInventorySlot).Id, 1) }`

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireAutoConnectPlaceTest|ElectricWireExtendProtocolTest"`
Expected: 全件PASS（+1予約の挙動が予約リストで維持されること）

- [ ] **Step 4: プロトコルを実装**

`Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Protocol.PacketResponse.Util.ElectricWire;

namespace Server.Protocol.PacketResponse
{
    public class PlaceBlockProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:placeBlock";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;

        public PlaceBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _gameUnlockStateDataController = serviceProvider.GetService<IGameUnlockStateDataController>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<SendPlaceBlockProtocolMessagePack>(payload);
            var inventoryData = _playerInventoryDataStore.GetInventoryData(data.PlayerId);

            // 未解放ブロックは全セルの設置を拒否する
            // Reject all cells when the block is locked
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(data.BlockId).BlockGuid;
            if (!_gameUnlockStateDataController.BlockUnlockStateInfos[blockGuid].IsUnlocked) return null;

            foreach (var placeInfo in data.PlacePositions)
            {
                PlaceBlock(placeInfo, data.BlockId, inventoryData);
            }

            return null;
        }

        private static void PlaceBlock(PlaceInfoMessagePack placeInfo, BlockId blockId, PlayerInventoryData inventoryData)
        {
            // すでにブロックがある場合は何もしない
            // Do nothing when a block already exists
            if (ServerContext.WorldBlockDatastore.Exists(placeInfo.Position)) return;

            var placeBlockId = blockId.GetVerticalOverrideBlockId(placeInfo.VerticalDirection);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(placeBlockId);
            var createParams = placeInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();

            // 建設コストを賄えないセルはスキップする（足りる分だけ設置）
            // Skip cells whose construction cost cannot be covered (place only what is affordable)
            var inventory = inventoryData.MainOpenableInventory;
            if (!ConstructionCostService.HasRequiredItems(blockMaster.RequiredItems, inventory.InventoryItems)) return;

            // 電気系ブロックなら自動接続計画を設置前に検証する。電線不足なら設置しない
            // For electric blocks, validate the auto-connect plan before placement; skip when wires are insufficient
            var isElectric = ElectricWireBlockParamResolver.TryGetWireParam(blockMaster.BlockParam, out _, out _);
            var plan = default(ElectricWireAutoConnectPlan);
            if (isElectric)
            {
                // 建設コストで消費予定の素材を予約として渡し、電線の所持数判定から除外する
                // Pass construction-cost materials as reservations to exclude them from wire availability
                var reservedItems = blockMaster.RequiredItems == null
                    ? Array.Empty<(ItemId, int)>()
                    : blockMaster.RequiredItems.Select(v => (MasterHolder.ItemMaster.GetItemId(v.ItemGuid), v.Count)).ToArray();
                plan = ElectricWireAutoConnectService.EvaluateAutoConnect(placeBlockId, placeInfo.Position, placeInfo.Direction, reservedItems, inventory.InventoryItems);
                if (!plan.IsPlaceable) return;
            }

            // 設置に失敗した場合はコストを消費しない
            // Do not consume the cost when placement fails
            if (!ServerContext.WorldBlockDatastore.TryAddBlock(placeBlockId, placeInfo.Position, placeInfo.Direction, createParams, out var block)) return;

            ConstructionCostService.ConsumeRequiredItems(blockMaster.RequiredItems, inventory);

            // 検証済みの計画を実行してワイヤーを張り、電線を消費する
            // Execute the validated plan: add wires and consume wire items
            if (isElectric) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventory);
        }

        [MessagePackObject]
        public class SendPlaceBlockProtocolMessagePack : ProtocolMessagePackBase
        {
            [IgnoreMember] public BlockId BlockId => new(BlockIdInt);

            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public int BlockIdInt { get; set; }
            [Key(4)] public List<PlaceInfoMessagePack> PlacePositions { get; set; }

            public SendPlaceBlockProtocolMessagePack(int playerId, BlockId blockId, List<PlaceInfo> placeInfos)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                BlockIdInt = blockId.AsPrimitive();
                PlacePositions = placeInfos.ConvertAll(v => new PlaceInfoMessagePack(v));
            }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SendPlaceBlockProtocolMessagePack() { }
        }
    }
}
```

- [ ] **Step 5: PacketResponseCreatorに登録**

`PacketResponseCreator.cs`の`PlaceBlockFromHotBarProtocol`登録行（38行目付近）の直後に追加:

```csharp
            _packetResponseDictionary.Add(PlaceBlockProtocol.ProtocolTag, new PlaceBlockProtocol(serviceProvider));
```

- [ ] **Step 6: テストが通ることを確認**

Run: `uloop compile --project-path ./moorestech_client`（新規ファイル未認識ならUnity再起動）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBlockProtocolTest"`
Expected: 6件PASS

- [ ] **Step 7: コミット**

```bash
git add moorestech_server/
git commit -m "feat: 建設コスト消費で設置する新PlaceBlockProtocolを追加"
```

---

### Task 9: RemoveBlockProtocolの返却をrequiredItems全額に変更

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RemoveBlockRefundTest.cs`（新規）

**Interfaces:**
- Consumes: Task 7の`ConstructionCostService.CreateRefundItems`、Task 2のTestBlockマスタデータ
- Produces: `RemoveBlockProtocol`の返却仕様変更（requiredItems定義ブロック→素材全額、未定義→従来どおりitemGuid1個。プラン5でフォールバック削除予定）

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Server/PacketTest/RemoveBlockRefundTest.cs`:

```csharp
using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemoveBlockRefundTest
    {
        private const int PlayerId = 3;

        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4
        private static readonly Guid TestBlockItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002"); // Test2 (TestBlockの旧アイテム)

        [Test]
        public void requiredItems定義ブロックの破壊で素材が全額返却される()
        {
            var (packet, serviceProvider) = CreateServer();
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.BlockId, new Vector3Int(3, 3), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var inventory = GetInventory(serviceProvider);
            packet.GetPacketResponse(CreateRemovePayload(3, 3), new PacketResponseContext());

            // 素材(Test3×2+Test4×1)が返り、旧ブロックアイテム(Test2)は返らない
            // Materials (Test3 x2 + Test4 x1) are refunded; the old block item (Test2) is not
            Assert.IsFalse(world.Exists(new Vector3Int(3, 3)));
            Assert.AreEqual(2, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
            Assert.AreEqual(0, GetItemCount(inventory, TestBlockItemGuid));
        }

        [Test]
        public void requiredItems未定義ブロックは従来どおりブロックアイテムを返す()
        {
            var (packet, serviceProvider) = CreateServer();
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(4, 4), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var inventory = GetInventory(serviceProvider);
            packet.GetPacketResponse(CreateRemovePayload(4, 4), new PacketResponseContext());

            // TestBeltConveyorのitemGuidはTest3。従来どおり1個返る
            // TestBeltConveyor's itemGuid is Test3; one item is refunded as before
            Assert.IsFalse(world.Exists(new Vector3Int(4, 4)));
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
        }

        #region TestUtil

        private static (PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static IOpenableInventory GetInventory(ServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
        }

        private static int GetItemCount(IOpenableInventory inventory, Guid itemGuid)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
            var total = 0;
            foreach (var stack in inventory.InventoryItems)
            {
                if (stack.Id != itemId) continue;
                total += stack.Count;
            }
            return total;
        }

        private static byte[] CreateRemovePayload(int x, int y)
        {
            return MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(PlayerId, new Vector3Int(x, y)));
        }

        #endregion
    }
}
```

- [ ] **Step 2: テストを実行し1件目が失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RemoveBlockRefundTest"`
Expected: `requiredItems定義ブロックの破壊で素材が全額返却される`がFAIL（現状はTest2が1個返るため）。2件目はPASS（新規テストファイルが認識されない場合はUnity再起動）

- [ ] **Step 3: GetRefundItemsを変更**

`RemoveBlockProtocol.cs`の`GetRefundItems()`ローカル関数内、「破壊したブロック自体のアイテムを追加」部分を以下に置き換える:

```csharp
                // requiredItems定義ブロックは建設コストを全額返却し、未定義は従来どおりアイテム1個返す（プラン5でフォールバック削除予定）
                // Refund the full construction cost when requiredItems is defined; otherwise fall back to one block item (fallback removed in plan 5)
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);
                if (blockMaster.RequiredItems != null && blockMaster.RequiredItems.Length != 0)
                {
                    result.AddRange(ConstructionCostService.CreateRefundItems(blockMaster.RequiredItems));
                }
                else
                {
                    result.Add(ServerContext.ItemStackFactory.Create(itemId, 1));
                }
```

ファイル先頭に`using Server.Protocol.PacketResponse.Util.Construction;`を追加する。

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RemoveBlockRefundTest|RemoveBlockProtocolTest"`
Expected: 全件PASS（既存RemoveBlockProtocolTestはrequiredItems未定義ブロックを使っているため影響なしの想定。FAILした場合は対象ブロックのrequiredItems有無を確認して期待値を直す）

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/
git commit -m "feat: ブロック破壊時の返却を建設コスト全額に変更"
```

---

### Task 10: 全体回帰テストとプラン完了確認

**Files:** なし（検証のみ）

- [ ] **Step 1: サーバー系テストを広めに回す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests.CombinedTest.Server|Tests.UnitTest.Server"`
Expected: 全件PASS

- [ ] **Step 2: コンパイル最終確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件・警告増加なし

- [ ] **Step 3: 未コミットの変更がないことを確認してコミット**

```bash
git status
git add -A && git commit -m "chore: プラン1(サーバー基盤)の残作業をコミット"
```

（差分がなければコミット不要）
