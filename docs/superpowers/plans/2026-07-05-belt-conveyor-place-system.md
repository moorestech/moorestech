# ベルトコンベア専用設置システム（BeltConveyor placeMode） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 垂直オーバーライド機構を廃止し、placeSystemマスターの新モード`BeltConveyor`で上り/下り/Nマス長尺バリアントを定義。ドラッグ設置時に直線ランを最小ブロック数（3連優先の貪欲割当）へ分解してプレビュー・設置し、長尺=旧レシピ1セットの建設コストで旧経済（1クラフト3〜5個）を実質保存する。

**Architecture:** サーバーはマルチセルブロック（blockSize [1,1,N]）を既にフル対応しているため、長尺ベルトはマスターデータで表現する。プロトコルは`PlaceInfo`にセル毎BlockIdを持たせる方式へ拡張し、単一BlockId＋垂直オーバーライドの仕組みを置き換える。クライアントは既存のコンベア経路計算（1マス刻み点列＋Up/Down判定＋立体交差）を専用計算機に移し、その出力を純関数デコンポーザで長尺バリアント列へ変換する。各ブロック実体が自分の整数コスト（requiredItems=1セット）を持つため、解体返却は既存機構のままで総量保存が厳密に成立する。

**Tech Stack:** Unity / C# / MessagePack / Mooresmaster SourceGenerator（YAMLスキーマ→自動生成） / NUnit（uloop経由） / uloop CLI

## Global Constraints

- 1ファイル200行以下、1ディレクトリ10ファイルまで、partial絶対禁止（AGENTS.md）
- 主要処理に日本語→英語の2行セットコメント（各1行厳守）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client` を実行
- .metaファイル手動作成禁止。Prefab/シーンのテキスト直編集禁止（`uloop execute-dynamic-code`経由は可）
- try-catch禁止、デフォルト引数禁止、単純getter/setterプロパティ禁止
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "..."` で実行。「Unity is reloading」エラー時は45秒待機してリトライ
- スキーマ編集時は `edit-schema` スキル、foreignKey追加時は `validate-schema` スキルの手順に従う
- moorestech_masterのJSON編集時はmooreseditor.appを終了しておくこと（起動中は数秒で書き戻される）
- moorestech_masterへのコミット後は`.moorestech-external-revisions.json`のピン更新とcheckout確認（RepositorySyncが巻き戻すため）
- 各タスク終了時に必ず全作業をコミット（git worktree頻用のため作業消失防止）
- 後方互換は考慮不要（AGENTS.md）。プロトコルのMessagePack Key番号の振り直しも自由

## 前提知識（実装者向けコンテキスト）

- **設置フロー**: ビルドメニューでブロック選択→`BlockPlacementSelection.SelectedBlockId`→`PlaceSystemStateController`が毎フレーム`PlaceSystemSelector.GetCurrentPlaceSystem(context)`で`IPlaceSystem`を選択→`ManualUpdate(context)`
- **現行プロトコル**: `va:placeBlock`（`PlaceBlockProtocol`）は単一BlockId＋セル列。セル毎のブロック差は`OverrideVerticalBlock`（上り/水平/下り）のみ。本プランでこれをセル毎BlockIdに置き換え、`OverrideVerticalBlock`をスキーマごと削除する
- **マルチセルブロック**: `BlockPositionInfo.OriginalPos`は**常に占有AABBの最小座標**（`BlockPositionInfo.cs:17`）。`WorldBlockDatastore.TryAddBlock`は全占有セルの重複チェック・登録に対応済み
- **ベルト搬送**: `VanillaBeltConveyorComponent`はスロット配列の抽象実装で座標非依存。長尺は`beltConveyorItemCount`と`timeOfItemEnterToExit`をN倍するだけで搬送が成立
- **建設コスト**: `ConstructionCostService`（Has/Consume/CreateRefundItems）。解体返却は`RemoveBlockProtocol.GetRefundItems`が各ブロック自身の`RequiredItems`全額を返す
- **対象ファミリー（本番マスタ、いずれも1クラフト複数個だった旧経済を長尺で保存する）**:

| ファミリー | 代表(1連)Guid | up / down Guid | 長尺 | requiredItems(1セット) |
|---|---|---|---|---|
| 直線歯車ベルトコンベア | 7743a779-1d62-4b94-b306-4a0670bd8b48 | 11c8a7c9-b4c9-41c6-b52e-4f7b78d7e51d / c568f762-ee82-4e5f-8c80-0d70e3cbd8a2 | 2,3連 | 青銅シートf32cdaa5×1+木の棒ef4223f8×1 |
| 鉄の歯車ベルトコンベア | 8388e6a8-8a2e-4b0d-b869-610c204889fa | 56acb4e7-a85c-40db-91db-d3efd8338aa6 / cdd97e44-4ccc-47ea-a5f4-9fecb6be1bf2 | 2,3連 | 鉄板c1102112×1+補強棒材d657339b×1 |
| ベルトコンベア | 019e0b27-1b23-765b-99c3-52d15f5cc74e | 019e0b27-23d8-73f1-b615-8ed5b8ed9596 / 019e0b27-2662-770a-a806-8f9b03fd6bb6 | 2,3,4,5連 | 019e0a82×1+231d2146×1+019e3f98×1 |
| 高速ベルトコンベア | 019eeaa5-e9b0-70bb-9ecd-13706d8a7bd4 | 019eeaa6-2832-7251-9222-554812905b12 / 019eeaa6-324c-72f9-8ea0-9060e68ad24b | 2,3,4,5連 | 019e3a98×1+019e3a9b×1+019f0ca5×1 |
| ~~基本土台~~（**対象外に変更**: blockSize[5,1,5]でバリデータの1x1xN要件に違反。旧仕様でもisLargeBlock判定によりコンベア設置は常時無効だったため、エントリ削除で退行なし — 584a14ecで削除済み） | 8e9a603c-6f86-44ff-b4cb-a84d701a9f7b | - | - | - |
| ベルトコンベア分岐器 | 019e3afb-7fce-73bc-97cd-b0bea1232254 | self / self | なし | (現状のまま) |
| 高速ベルトコンベア分岐器 | 019eeaa6-45b4-7664-badb-6a0e3cf5ec9a | self / self | なし | (現状のまま) |

- **unlock設計**: 上り/下り/長尺バリアントはresearch.jsonに登場しない（`initialUnlocked:false`のまま）。設置可否はファミリー代表（1連直線）のunlock状態で判定する（Task 2/3）。ビルドメニューには代表のみ表示（Task 5）

---

### Task 1: placeSystem.yml スキーマ拡張（BeltConveyorモード＋placeParam switch）

**Files:**
- Modify: `VanillaSchema/placeSystem.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs:8`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/PlaceSystemMasterUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/placeSystem.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`（歯車ベルト2連/3連テストブロック追加）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`（定数追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/placeSystem.json`
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/placeSystem.json`（既存4エントリへのplaceParam:{}追加のみ。BeltConveyorエントリ追加はTask 7）

**Interfaces:**
- Produces: `Mooresmaster.Model.PlaceSystemModule` に生成される `PlaceSystemMasterElement.PlaceParam` プロパティと `BeltConveyorPlaceParam` クラス（`UpBlockGuid: Guid`, `DownBlockGuid: Guid`, `StraightBlocks: StraightBlocksElement[]` — 各要素は`Length: int`と`BlockGuid: Guid`）。※生成名はSourceGeneratorの命名規則に従うため、再生成後にStep 4で実名を確認し、以降のタスクで差異があれば読み替えること

- [ ] **Step 1: edit-schemaスキルを読む**

`.claude/skills/edit-schema/SKILL.md` と `references/yaml_spec.md` を読み、switch/cases記法とリコンパイル手順を確認する。

- [ ] **Step 2: placeSystem.ymlを編集**

`VanillaSchema/placeSystem.yml` の `placeMode` enum に `BeltConveyor` を追加し、`usePlaceItems` の後に `placeParam` switchを追加する:

```yaml
    - key: placeMode
      type: enum
      options:
      - TrainRail
      - TrainRailConnect
      - TrainCar
      - GearChainPoleConnect
      - ElectricWireConnect
      - BeltConveyor
    - key: priority
      type: integer
      autoIncrement:
        direction: asc
        step: 10
        startWith: 100
    - key: usePlaceItems
      type: array
      items:
        type: uuid
        foreignKey:
          schemaId: items
          foreignKeyIdPath: /data/[*]/itemGuid
          displayElementPath: /data/[*]/name
    - key: placeParam
      switch: ./placeMode
      cases:
      - when: TrainRail
        type: object
        properties: []
      - when: TrainRailConnect
        type: object
        properties: []
      - when: TrainCar
        type: object
        properties: []
      - when: GearChainPoleConnect
        type: object
        properties: []
      - when: ElectricWireConnect
        type: object
        properties: []
      - when: BeltConveyor
        type: object
        properties:
        - key: upBlockGuid
          type: uuid
          foreignKey:
            schemaId: blocks
            foreignKeyIdPath: /data/[*]/blockGuid
            displayElementPath: /data/[*]/name
        - key: downBlockGuid
          type: uuid
          foreignKey:
            schemaId: blocks
            foreignKeyIdPath: /data/[*]/blockGuid
            displayElementPath: /data/[*]/name
        - key: straightBlocks
          type: array
          items:
            type: object
            properties:
            - key: length
              type: integer
            - key: blockGuid
              type: uuid
              foreignKey:
                schemaId: blocks
                foreignKeyIdPath: /data/[*]/blockGuid
                displayElementPath: /data/[*]/name
```

設計メモ: 長さ（Nマス）は**マスターの`length`フィールドで明示設定する**（blockSizeからの導出はしない）。設置ストライドとデコンポーザの割当はこの`length`が正。blockSizeとの食い違いはStep 6のバリデーションでデータ不整合エラーとして検出する。

- [ ] **Step 3: 全placeSystem.jsonデータに placeParam を追加**

blocks.jsonの`blockParam`と同様、switchプロパティは全エントリ必須。既存エントリに`"placeParam": {}`を追加する。対象3ファイル:

1. `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/placeSystem.json`（TrainCar/GearChainPoleConnect/ElectricWireConnectの3エントリ）
2. `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/placeSystem.json`（全エントリ）
3. `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/placeSystem.json`（TrainRailConnect/TrainRail/TrainCar/GearChainPoleConnectの4エントリ。**編集前にmooreseditor.appが終了していることを確認**）

各エントリの末尾に追加する形:
```json
    {
      "placeMode": "TrainCar",
      "priority": 120,
      "usePlaceItems": ["..."],
      "placeParam": {}
    }
```

- [ ] **Step 4: SourceGenerator再生成とコンパイル**

`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs:8` の `dummyText` を現在時刻文字列に変更（例 `"2026/07/05 23:00:00"`）し、コンパイル:

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件。その後、生成された型名を確認する:

Run: `grep -rn "class.*PlaceParam" /Users/katsumi/moorestech/moorestech_client/Library/ --include="*.cs" -l 2>/dev/null | head -3` で生成物を探すか、`uloop execute-dynamic-code` で `typeof(Mooresmaster.Model.PlaceSystemModule.PlaceSystemMasterElement).GetProperty("PlaceParam")` と `BeltConveyorPlaceParam` 型の存在・プロパティ名を確認。
Expected: `PlaceParam`プロパティと`BeltConveyorPlaceParam`型（`UpBlockGuid`/`DownBlockGuid`/`StraightBlocks`。要素に`Length`/`BlockGuid`）。名前が想定と違う場合は以降のタスクで読み替え。

- [ ] **Step 5: テスト用masterに歯車ベルト2連/3連ブロックとBeltConveyorエントリを追加**

`ForUnitTestModBlockId.cs` に定数を追加（既存の`GearBeltConveyor`等の記法に合わせる。GUIDは既存の連番規則 `...00a1`(Up), `...00a2`(Down) に続けて `...00a3`(2連), `...00a4`(3連)）:

```csharp
public static readonly BlockId GearBeltConveyor2 = GetBlockId("00000000-0000-0000-1234-0000000000a3");
public static readonly BlockId GearBeltConveyor3 = GetBlockId("00000000-0000-0000-1234-0000000000a4");
```
（実際の定義形式は既存ファイルの記法を踏襲すること。GUID部分のみ上記を使用）

`forUnitTest/master/blocks.json` に2エントリ追加: 既存`GearBeltConveyor`（blockGuid末尾`...0015`）エントリをコピーし、以下だけ変更:
- blockGuid: 上記新GUID
- name: `GearBeltConveyor2` / `GearBeltConveyor3`
- blockSize: `[1,1,2]` / `[1,1,3]`
- blockParam.beltConveyorItemCount: 基準値×2 / ×3
- blockParam.timeOfItemEnterToExit: 基準値×2 / ×3
- inventoryConnectors.outputConnects の offset: `[0,0,1]` / `[0,0,2]`（先頭セルから搬出）
- gear.gearConnects: 各セルにエントリ複製（offset `[0,0,0]`〜`[0,0,N-1]`、connectorGuidは新規UUID。`uuidgen`で生成）
- requiredItems: 1連と同一

`forUnitTest/master/placeSystem.json` にBeltConveyorエントリ追加:
```json
    {
      "placeMode": "BeltConveyor",
      "priority": 140,
      "usePlaceItems": [],
      "placeParam": {
        "upBlockGuid": "00000000-0000-0000-1234-0000000000a1",
        "downBlockGuid": "00000000-0000-0000-1234-0000000000a2",
        "straightBlocks": [
          { "length": 1, "blockGuid": "00000000-0000-0000-1234-000000000015" },
          { "length": 2, "blockGuid": "00000000-0000-0000-1234-0000000000a3" },
          { "length": 3, "blockGuid": "00000000-0000-0000-1234-0000000000a4" }
        ]
      }
    }
```
（`...0015`等のGUIDは`ForUnitTestModBlockId.cs`の実際のGearBeltConveyor/Up/Down定義値と突合して正確に転記すること）

- [ ] **Step 6: PlaceSystemMasterUtilにBeltConveyorバリデーション追加**

`validate-schema`スキルの方針に従い、`Core.Master/Validator/PlaceSystemMasterUtil.cs` の `Validate` に追加（既存のUsePlaceItemsチェックと同じ流儀で）:

```csharp
// BeltConveyorモードのブロック参照と長尺構成を検証
// Validate block references and length composition of BeltConveyor mode entries
string BeltConveyorParamValidation()
{
    var logs = string.Empty;
    foreach (var element in placeSystem.Data)
    {
        if (element.PlaceParam is not BeltConveyorPlaceParam param) continue;

        logs += ValidateBlockGuidExists(param.UpBlockGuid, "upBlockGuid");
        logs += ValidateBlockGuidExists(param.DownBlockGuid, "downBlockGuid");

        var lengthOneCount = 0;
        var seenLengths = new HashSet<int>();
        foreach (var straightBlock in param.StraightBlocks)
        {
            logs += ValidateBlockGuidExists(straightBlock.BlockGuid, "straightBlocks");
            var block = blockMaster.Blocks.Data.FirstOrDefault(b => b.BlockGuid == straightBlock.BlockGuid);
            if (block == null) continue;

            // lengthは1以上・重複禁止・length==1がちょうど1件
            // Length must be >=1, unique, and exactly one length-1 entry must exist
            if (straightBlock.Length < 1)
                logs += $"[PlaceSystemMaster] BeltConveyor straight block {block.Name} has invalid length:{straightBlock.Length}\n";
            if (!seenLengths.Add(straightBlock.Length))
                logs += $"[PlaceSystemMaster] BeltConveyor duplicated length:{straightBlock.Length} block:{block.Name}\n";
            if (straightBlock.Length == 1) lengthOneCount++;

            // マスターのlengthとblockSizeの食い違いはデータ不整合として検出
            // Mismatch between master length and blockSize is reported as a data error
            if (block.BlockSize[0] != 1 || block.BlockSize[1] != 1 || block.BlockSize[2] != straightBlock.Length)
                logs += $"[PlaceSystemMaster] BeltConveyor straight block {block.Name} blockSize must be [1,1,{straightBlock.Length}]\n";
        }
        if (lengthOneCount != 1)
            logs += "[PlaceSystemMaster] BeltConveyor entry must contain exactly one length-1 straight block\n";
    }
    return logs;
}
```
（`ValidateBlockGuidExists`ローカル関数はGuidがBlockMasterに存在するかを検証しエラーログを返す小関数として同メソッド内に実装。BlockMasterElementのBlockSizeがVector3Int型なら`.x/.y/.z`でアクセスに読み替え。ファイルが200行を超える場合は`PlaceSystemBeltConveyorValidator.cs`として分離）

- [ ] **Step 7: コンパイルと既存テストの確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBlockProtocolTest|PlaceSystemMaster"`
Expected: 全パス（テストmasterのロード自体がバリデーション通過の証明になる）

- [ ] **Step 8: Commit**

```bash
git add -A && git commit -m "feat: placeSystemマスターにBeltConveyorモードとplaceParamスイッチを追加"
cd /Users/katsumi/moorestech_master && git add -A && git commit -m "chore: placeSystem既存エントリにplaceParam追加" && cd /Users/katsumi/moorestech
```

---

### Task 2: BeltConveyorファミリー解決ユーティリティ

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorPlaceFamilyUtil.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorPlaceFamilyUtilTest.cs`

**Interfaces:**
- Consumes: Task 1の`BeltConveyorPlaceParam`（`MasterHolder.PlaceSystemMaster.PlaceSystem.Data`の`PlaceParam`）
- Produces:
  - `bool TryGetFamily(BlockId blockId, out BeltConveyorPlaceParam param)`
  - `bool TryGetFamilyByGuid(Guid blockGuid, out BeltConveyorPlaceParam param)`
  - `BlockId GetRepresentativeBlockId(BeltConveyorPlaceParam param)` — マスター定義`length==1`の直線ブロック
  - `List<(int length, BlockId blockId)> GetStraightVariantsDesc(BeltConveyorPlaceParam param)` — マスター定義`length`の降順
  - `bool IsHiddenVariant(Guid blockGuid)` — ファミリー所属かつ代表Guidでない（up/down/長尺。self参照のファミリーでは常にfalse）

- [ ] **Step 1: 失敗するテストを書く**

`Tests/UnitTest/Game/BeltConveyorPlaceFamilyUtilTest.cs`（初期化は既存UnitTestの流儀 = `new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory))` でMasterHolderをロード）:

```csharp
[Test]
public void 歯車ベルト系ブロックはファミリー解決できる()
{
    Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor, out _));
    Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor3, out _));
    // up/downバリアントもファミリー所属
    Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.TestGearBeltConveyorUp, out _));
    // 非ベルトブロックは解決されない
    Assert.IsFalse(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.MachineId, out _));
}

[Test]
public void 代表ブロックと長さ降順バリアントが正しい()
{
    BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor3, out var param);
    Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, BeltConveyorPlaceFamilyUtil.GetRepresentativeBlockId(param));

    var variants = BeltConveyorPlaceFamilyUtil.GetStraightVariantsDesc(param);
    Assert.AreEqual(3, variants.Count);
    Assert.AreEqual(3, variants[0].length);
    Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor3, variants[0].blockId);
    Assert.AreEqual(1, variants[2].length);
}

[Test]
public void 隠しバリアント判定は代表のみfalse()
{
    var upGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearBeltConveyorUp).BlockGuid;
    var repGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockGuid;
    var longGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor3).BlockGuid;

    Assert.IsTrue(BeltConveyorPlaceFamilyUtil.IsHiddenVariant(upGuid));
    Assert.IsTrue(BeltConveyorPlaceFamilyUtil.IsHiddenVariant(longGuid));
    Assert.IsFalse(BeltConveyorPlaceFamilyUtil.IsHiddenVariant(repGuid));
}
```
（`TestGearBeltConveyorUp`等の定数名は`ForUnitTestModBlockId.cs`の実名に合わせること）

- [ ] **Step 2: テスト失敗確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BeltConveyorPlaceFamilyUtil`未定義のコンパイルエラー

- [ ] **Step 3: 実装**

`Game.Block.Interface/Extension/BeltConveyorPlaceFamilyUtil.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.PlaceSystemModule;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// placeSystemマスターのBeltConveyorエントリからファミリー（代表・斜面・長尺）を解決する
    /// Resolves belt conveyor families (representative, slopes, length variants) from BeltConveyor placeSystem entries
    /// </summary>
    public static class BeltConveyorPlaceFamilyUtil
    {
        public static bool TryGetFamily(BlockId blockId, out BeltConveyorPlaceParam beltParam)
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            return TryGetFamilyByGuid(blockGuid, out beltParam);
        }

        public static bool TryGetFamilyByGuid(Guid blockGuid, out BeltConveyorPlaceParam beltParam)
        {
            // 全BeltConveyorエントリを走査しメンバー照合（エントリ数は少数のためキャッシュ不要）
            // Scan all BeltConveyor entries for membership (few entries, no cache needed)
            foreach (var element in MasterHolder.PlaceSystemMaster.PlaceSystem.Data)
            {
                if (element.PlaceParam is not BeltConveyorPlaceParam param) continue;
                if (param.UpBlockGuid == blockGuid || param.DownBlockGuid == blockGuid)
                {
                    beltParam = param;
                    return true;
                }
                foreach (var straightBlock in param.StraightBlocks)
                {
                    if (straightBlock.BlockGuid != blockGuid) continue;
                    beltParam = param;
                    return true;
                }
            }
            beltParam = null;
            return false;
        }

        public static BlockId GetRepresentativeBlockId(BeltConveyorPlaceParam beltParam)
        {
            // 代表はマスター定義length==1の直線ブロック（バリデータが1件のみを保証）
            // The representative is the master-defined length-1 straight block (validator guarantees exactly one)
            foreach (var straightBlock in beltParam.StraightBlocks)
            {
                if (straightBlock.Length == 1) return MasterHolder.BlockMaster.GetBlockId(straightBlock.BlockGuid);
            }
            throw new InvalidOperationException("BeltConveyor entry has no length-1 straight block");
        }

        public static List<(int length, BlockId blockId)> GetStraightVariantsDesc(BeltConveyorPlaceParam beltParam)
        {
            // 長さはマスター定義のlengthをそのまま使用する（blockSizeからは導出しない）
            // Lengths come straight from the master-defined length field (never derived from blockSize)
            var variants = new List<(int length, BlockId blockId)>();
            foreach (var straightBlock in beltParam.StraightBlocks)
            {
                variants.Add((straightBlock.Length, MasterHolder.BlockMaster.GetBlockId(straightBlock.BlockGuid)));
            }
            variants.Sort((a, b) => b.length.CompareTo(a.length));
            return variants;
        }

        public static bool IsHiddenVariant(Guid blockGuid)
        {
            if (!TryGetFamilyByGuid(blockGuid, out var param)) return false;
            var representativeGuid = MasterHolder.BlockMaster.GetBlockMaster(GetRepresentativeBlockId(param)).BlockGuid;
            return blockGuid != representativeGuid;
        }
    }
}
```
（`StraightBlocks`要素の生成型名はTask 1 Step 4で確認した実名に合わせる。`MasterHolder.BlockMaster.GetBlockId(Guid)`が無い場合は既存のGuid→BlockId変換APIを`BlockMaster`から探して使用）

- [ ] **Step 4: テストパス確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorPlaceFamilyUtilTest"`
Expected: 3件全パス

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: BeltConveyorファミリー解決ユーティリティを追加"
```

---

### Task 3: プロトコルのセル毎BlockId化

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlacePacketDto.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs:53-57`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs:146-152`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs:214`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlacePointCalculator.cs:361-385`（CalcPlaceable近辺でBlockId充填）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PlacementPreviewBlockGameObjectController.cs:42`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTest.cs`

**Interfaces:**
- Consumes: Task 2の`BeltConveyorPlaceFamilyUtil.TryGetFamily`/`GetRepresentativeBlockId`
- Produces:
  - `PlaceInfo`に`public BlockId BlockId;`フィールド追加（POCO）
  - `PlaceInfoMessagePack`に`[Key(4)] public int BlockIdInt`＋`[IgnoreMember] public BlockId BlockId`追加
  - `SendPlaceBlockProtocolMessagePack`から単一BlockIdInt削除。新シグネチャ `SendPlaceBlockProtocolMessagePack(int playerId, List<PlaceInfo> placeInfos)`（Key(2)=PlayerId, Key(3)=PlacePositions）
  - `VanillaApiSendOnly.PlaceBlock(List<PlaceInfo> placeInfos)`（blockId引数削除）
  - `PlaceSystemUtil.SendPlaceBlockProtocol(List<PlaceInfo> currentPlaceInfos)`

- [ ] **Step 1: 失敗するテストを書く**

`PlaceBlockProtocolTest.cs`のペイロード生成ヘルパをセル毎BlockId形式に書き換え、新テスト2件を追加:

```csharp
[Test]
public void セル毎に異なるBlockIdを一括設置できる()
{
    var (packet, serviceProvider) = CreateServer();
    // 素材付与（歯車ベルト1セット×2）
    // Grant two cost sets of the gear belt family materials
    GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 2);

    var placeInfos = new List<PlaceInfo>
    {
        new() { Position = new Vector3Int(10, 0, 10), Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = ForUnitTestModBlockId.GearBeltConveyor },
        new() { Position = new Vector3Int(10, 0, 11), Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Up, BlockId = ForUnitTestModBlockId.TestGearBeltConveyorUp },
    };
    packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext());

    Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(10, 0, 10)));
    Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(10, 0, 11)));
    Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp,
        ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(10, 0, 11)).BlockId);
}

[Test]
public void バリアントの設置可否はファミリー代表のunlock状態で決まる()
{
    var (packet, serviceProvider) = CreateServer();
    GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor3, 1);

    // 代表（GearBeltConveyor）が未解放なら長尺バリアントも設置不可
    // A length variant cannot be placed while the family representative is locked
    LockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
    var placeInfos = new List<PlaceInfo>
    {
        new() { Position = new Vector3Int(20, 0, 10), Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = ForUnitTestModBlockId.GearBeltConveyor3 },
    };
    packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext());
    Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(20, 0, 10)));

    // 代表を解放すると長尺バリアントが設置できる
    // Unlocking the representative allows the variant placement
    UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
    packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext());
    Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(20, 0, 10)));
}
```

補助関数（`CreatePlacePayload`/`GrantRequiredItems`/`LockBlock`/`UnlockBlock`）は既存テストの素材付与・unlock操作コード（`素材不足のセルはスキップされ賄える分だけ設置される`と`未解放ブロックは設置されない`テスト）を流用して実装する。既存テスト群のペイロード生成もすべて`PlaceInfo.BlockId`充填＋新コンストラクタに更新。テスト用masterのGearBeltConveyorのunlock初期状態（initialUnlocked）に応じてLock/Unlock操作を調整。

- [ ] **Step 2: コンパイル失敗確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PlaceInfo.BlockId`未定義エラー

- [ ] **Step 3: DTO実装**

`PlacePacketDto.cs`:
- `PlaceInfo`クラスに `public BlockId BlockId;` を追加
- `PlaceInfoMessagePack`に追加:
```csharp
[Key(4)] public int BlockIdInt { get; set; }
[IgnoreMember] public BlockId BlockId => new(BlockIdInt);
```
コンストラクタ`PlaceInfoMessagePack(PlaceInfo placeInfo)`で`BlockIdInt = placeInfo.BlockId.AsPrimitive();`を設定。

`PlaceBlockProtocol.cs`の`SendPlaceBlockProtocolMessagePack`:
```csharp
[MessagePackObject]
public class SendPlaceBlockProtocolMessagePack : ProtocolMessagePackBase
{
    [Key(2)] public int PlayerId { get; set; }
    [Key(3)] public List<PlaceInfoMessagePack> PlacePositions { get; set; }

    public SendPlaceBlockProtocolMessagePack(int playerId, List<PlaceInfo> placeInfos)
    {
        Tag = ProtocolTag;
        PlayerId = playerId;
        PlacePositions = placeInfos.ConvertAll(v => new PlaceInfoMessagePack(v));
    }

    [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
    public SendPlaceBlockProtocolMessagePack() { }
}
```

- [ ] **Step 4: サーバープロトコル実装**

`PlaceBlockProtocol.GetResponse`を書き換え（全体像。既存の電線・コスト処理は維持）:

```csharp
public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
{
    var data = MessagePackSerializer.Deserialize<SendPlaceBlockProtocolMessagePack>(payload);
    var inventoryData = _playerInventoryDataStore.GetInventoryData(data.PlayerId);

    foreach (var placeInfo in data.PlacePositions)
    {
        PlaceBlock(placeInfo);
    }

    return null;

    #region Internal

    void PlaceBlock(PlaceInfoMessagePack placeInfo)
    {
        // すでにブロックがある場合は何もしない
        // Do nothing when a block already exists
        if (ServerContext.WorldBlockDatastore.Exists(placeInfo.Position)) return;

        var placeBlockId = placeInfo.BlockId;
        var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(placeBlockId);

        // 未解放セルはスキップ（バリアントはファミリー代表のunlock状態で判定）
        // Skip locked cells; variants resolve unlock via their family representative
        if (!IsUnlocked(placeBlockId, blockMaster.BlockGuid)) return;

        var createParams = placeInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();

        // コスト不足セルはスキップ
        // Skip cells whose construction cost cannot be covered (place only what is affordable)
        var inventory = inventoryData.MainOpenableInventory;
        if (!ConstructionCostService.HasRequiredItems(blockMaster.RequiredItems, inventory.InventoryItems)) return;

        // 電気なら自動接続を事前検証（既存処理をそのまま維持）
        // For electric blocks, validate the auto-connect plan before placement (existing logic unchanged)
        var isElectric = ElectricWireBlockParamResolver.TryGetWireParam(blockMaster.BlockParam, out _, out _);
        var plan = default(ElectricWireAutoConnectPlan);
        if (isElectric)
        {
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

        if (isElectric) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventory);
    }

    bool IsUnlocked(BlockId blockId, Guid blockGuid)
    {
        // ベルトファミリーは代表ブロックのunlock状態を参照する
        // Belt families resolve unlock state through the representative block
        var unlockGuid = BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var beltParam)
            ? MasterHolder.BlockMaster.GetBlockMaster(BeltConveyorPlaceFamilyUtil.GetRepresentativeBlockId(beltParam)).BlockGuid
            : blockGuid;
        return _gameUnlockStateDataController.BlockUnlockStateInfos[unlockGuid].IsUnlocked;
    }

    #endregion
}
```
`GetVerticalOverrideBlockId`の呼び出しは削除（using含め）。`Game.Block.Interface.Extension`をusingに追加。

- [ ] **Step 5: クライアント送信側の追随**

- `VanillaApiSendOnly.PlaceBlock`: シグネチャを`PlaceBlock(List<PlaceInfo> placeInfos)`へ変更し`new SendPlaceBlockProtocolMessagePack(_playerId, placeInfos)`を送信
- `PlaceSystemUtil.SendPlaceBlockProtocol(List<PlaceInfo> currentPlaceInfos)`: blockId引数削除
- `CommonBlockPlaceSystem.cs:214`: `SendPlaceBlockProtocol(_currentPlaceInfos);`に変更
- `CommonBlockPlacePointCalculator`: `CalcPlaceable`（:361-372）の直前または内部で各PlaceInfoに`info.BlockId = holdingBlockMasterElement.BlockGuid.GetVerticalOverrideBlockId(info.VerticalDirection);`を設定（:381の既存解決ロジックを移設。Task 6で垂直オーバーライド削除時に単純化される暫定措置）
- `PlacementPreviewBlockGameObjectController.cs:42`: `var blockId = placeInfo.BlockId;`に変更（GetVerticalOverrideBlockId呼び出し削除）

- [ ] **Step 6: テストパス確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBlockProtocolTest|ElectricWireAutoConnectPlaceTest|RemoveBlockRefundTest"`
Expected: 全パス（既存の建設コスト・電線・返却テスト含む）

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: va:placeBlockをセル毎BlockId方式に変更しファミリーunlock判定を導入"
```

---

### Task 4: 長尺分解の純ロジック（BeltConveyorRunDecomposer）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorRunDecomposer.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorRunDecomposerTest.cs`

**Interfaces:**
- Consumes: `PlaceInfo`（Task 3でBlockIdフィールド追加済み）
- Produces:
  - `static List<PlaceInfo> Decompose(IReadOnlyList<PlaceInfo> cells, IReadOnlyList<(int length, BlockId blockId)> straightVariantsDesc, BlockId upBlockId, BlockId downBlockId)`
  - 入力は1マス刻みの経路セル列（経路順）。出力は設置エンティティ列（長尺はPosition=占有AABB最小座標、Direction=進行方向）

- [ ] **Step 1: 失敗するテストを書く（純NUnit、Unity/サーバーDI不要）**

```csharp
public class BeltConveyorRunDecomposerTest
{
    private static readonly BlockId Straight1 = new(101);
    private static readonly BlockId Straight2 = new(102);
    private static readonly BlockId Straight3 = new(103);
    private static readonly BlockId UpBlock = new(104);
    private static readonly BlockId DownBlock = new(105);

    private static readonly List<(int length, BlockId blockId)> Variants = new() { (3, Straight3), (2, Straight2), (1, Straight1) };

    private static PlaceInfo Cell(int x, int y, int z, BlockDirection dir, BlockVerticalDirection vertical, bool placeable)
    {
        return new PlaceInfo { Position = new Vector3Int(x, y, z), Direction = dir, VerticalDirection = vertical, Placeable = placeable };
    }

    [Test]
    public void 直線9マスは3連x3に分解される()
    {
        var cells = Enumerable.Range(0, 9).Select(i => Cell(0, 0, i, BlockDirection.North, BlockVerticalDirection.Horizontal, true)).ToList();
        var result = BeltConveyorRunDecomposer.Decompose(cells, Variants, UpBlock, DownBlock);

        Assert.AreEqual(3, result.Count);
        Assert.IsTrue(result.All(r => r.BlockId == Straight3));
        Assert.AreEqual(new Vector3Int(0, 0, 0), result[0].Position);
        Assert.AreEqual(new Vector3Int(0, 0, 3), result[1].Position);
        Assert.AreEqual(new Vector3Int(0, 0, 6), result[2].Position);
    }

    [Test]
    public void 直線7マスは3連x2と1連x1()
    {
        var cells = Enumerable.Range(0, 7).Select(i => Cell(0, 0, i, BlockDirection.North, BlockVerticalDirection.Horizontal, true)).ToList();
        var result = BeltConveyorRunDecomposer.Decompose(cells, Variants, UpBlock, DownBlock);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(Straight3, result[0].BlockId);
        Assert.AreEqual(Straight3, result[1].BlockId);
        Assert.AreEqual(Straight1, result[2].BlockId);
    }

    [Test]
    public void 西向きランの原点は占有範囲の最小座標になる()
    {
        // 西向き（-X方向）に3マス: (5,0,0)→(3,0,0)。3連の原点は最小座標(3,0,0)
        // Westward 3-cell run: the 3-length variant's origin must be the min corner (3,0,0)
        var cells = new List<PlaceInfo>
        {
            Cell(5, 0, 0, BlockDirection.West, BlockVerticalDirection.Horizontal, true),
            Cell(4, 0, 0, BlockDirection.West, BlockVerticalDirection.Horizontal, true),
            Cell(3, 0, 0, BlockDirection.West, BlockVerticalDirection.Horizontal, true),
        };
        var result = BeltConveyorRunDecomposer.Decompose(cells, Variants, UpBlock, DownBlock);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(Straight3, result[0].BlockId);
        Assert.AreEqual(new Vector3Int(3, 0, 0), result[0].Position);
        Assert.AreEqual(BlockDirection.West, result[0].Direction);
    }

    [Test]
    public void カーブでランが分割される()
    {
        // 北向き2マス→東向き2マスのL字。2連+2連に分解される
        // L-shape: 2 north cells then 2 east cells decompose into two 2-length variants
        var cells = new List<PlaceInfo>
        {
            Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
            Cell(0, 0, 1, BlockDirection.East, BlockVerticalDirection.Horizontal, true),
            Cell(1, 0, 1, BlockDirection.East, BlockVerticalDirection.Horizontal, true),
            Cell(2, 0, 1, BlockDirection.East, BlockVerticalDirection.Horizontal, true),
        };
        var result = BeltConveyorRunDecomposer.Decompose(cells, Variants, UpBlock, DownBlock);

        // 先頭セルは方向が異なるため1連、続く東向き3マスは3連
        // The corner cell stays 1-length; the following 3 east cells merge into one 3-length
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(Straight1, result[0].BlockId);
        Assert.AreEqual(Straight3, result[1].BlockId);
        Assert.AreEqual(new Vector3Int(0, 0, 1), result[1].Position);
    }

    [Test]
    public void 斜面セルは専用ブロックの1マスになる()
    {
        var cells = new List<PlaceInfo>
        {
            Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
            Cell(0, 0, 1, BlockDirection.North, BlockVerticalDirection.Up, true),
            Cell(0, 1, 2, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
        };
        var result = BeltConveyorRunDecomposer.Decompose(cells, Variants, UpBlock, DownBlock);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(Straight1, result[0].BlockId);
        Assert.AreEqual(UpBlock, result[1].BlockId);
        Assert.AreEqual(BlockVerticalDirection.Up, result[1].VerticalDirection);
        Assert.AreEqual(Straight1, result[2].BlockId);
    }

    [Test]
    public void 設置不可セルはランに含まれず1連のまま残る()
    {
        var cells = new List<PlaceInfo>
        {
            Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
            Cell(0, 0, 1, BlockDirection.North, BlockVerticalDirection.Horizontal, false),
            Cell(0, 0, 2, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
            Cell(0, 0, 3, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
        };
        var result = BeltConveyorRunDecomposer.Decompose(cells, Variants, UpBlock, DownBlock);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(Straight1, result[0].BlockId);
        Assert.IsFalse(result[1].Placeable);
        Assert.AreEqual(Straight1, result[1].BlockId);
        Assert.AreEqual(Straight2, result[2].BlockId);
        Assert.AreEqual(new Vector3Int(0, 0, 2), result[2].Position);
    }

    [Test]
    public void 位置が連続しないセルはランが分割される()
    {
        // 同方向でも座標が飛んでいる場合は結合しない（立体交差の高さ変化等）
        // Cells with a positional gap (e.g. overpass height change) must not merge
        var cells = new List<PlaceInfo>
        {
            Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
            Cell(0, 1, 1, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
        };
        var result = BeltConveyorRunDecomposer.Decompose(cells, Variants, UpBlock, DownBlock);

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.All(r => r.BlockId == Straight1));
    }
}
```

- [ ] **Step 2: コンパイル失敗確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BeltConveyorRunDecomposer`未定義エラー

- [ ] **Step 3: 実装**

```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// 1マス刻みの経路セル列を長尺バリアント・斜面ブロックの設置エンティティ列へ分解する純ロジック
    /// Pure logic that decomposes per-cell path infos into length-variant and slope placement entities
    /// </summary>
    public static class BeltConveyorRunDecomposer
    {
        public static List<PlaceInfo> Decompose(
            IReadOnlyList<PlaceInfo> cells,
            IReadOnlyList<(int length, BlockId blockId)> straightVariantsDesc,
            BlockId upBlockId,
            BlockId downBlockId)
        {
            var result = new List<PlaceInfo>();
            var index = 0;
            while (index < cells.Count)
            {
                var cell = cells[index];

                // 斜面・設置不可セルは1マスエンティティとして確定
                // Slope cells and unplaceable cells become single-cell entities
                if (cell.VerticalDirection == BlockVerticalDirection.Up) { result.Add(CreateSingle(cell, upBlockId)); index++; continue; }
                if (cell.VerticalDirection == BlockVerticalDirection.Down) { result.Add(CreateSingle(cell, downBlockId)); index++; continue; }
                if (!cell.Placeable) { result.Add(CreateSingle(cell, GetLengthOneBlockId())); index++; continue; }

                // 水平ランの長さを検出し、長い順の貪欲割当で最小ブロック数に分解
                // Detect the horizontal run length, then greedily assign longest variants first
                var runLength = DetectRunLength(index);
                var offset = 0;
                while (offset < runLength)
                {
                    var (variantLength, variantBlockId) = SelectVariant(runLength - offset);
                    result.Add(CreateStraight(index + offset, variantLength, variantBlockId));
                    offset += variantLength;
                }
                index += runLength;
            }

            return result;

            #region Internal

            int DetectRunLength(int startIndex)
            {
                var length = 1;
                while (startIndex + length < cells.Count && IsRunContinuation(cells[startIndex + length - 1], cells[startIndex + length])) length++;
                return length;
            }

            bool IsRunContinuation(PlaceInfo current, PlaceInfo next)
            {
                if (next.VerticalDirection != BlockVerticalDirection.Horizontal || !next.Placeable) return false;
                if (next.Direction != current.Direction) return false;
                // 進行方向に1マスちょうど隣接していることを要求（高さ変化や飛びは分割）
                // Require exact one-cell adjacency along the travel direction (splits on height jumps/gaps)
                return next.Position - current.Position == ToVector(current.Direction);
            }

            (int length, BlockId blockId) SelectVariant(int remaining)
            {
                foreach (var variant in straightVariantsDesc)
                {
                    if (variant.length <= remaining) return variant;
                }
                return (1, GetLengthOneBlockId());
            }

            BlockId GetLengthOneBlockId()
            {
                return straightVariantsDesc[straightVariantsDesc.Count - 1].blockId;
            }

            PlaceInfo CreateStraight(int startIndex, int length, BlockId blockId)
            {
                // マルチセルブロックの原点は占有範囲の最小座標（BlockPositionInfoの規約）
                // Multi-cell block origin is the min corner of the occupied range (BlockPositionInfo convention)
                var origin = cells[startIndex].Position;
                for (var i = 1; i < length; i++) origin = Vector3Int.Min(origin, cells[startIndex + i].Position);

                return new PlaceInfo
                {
                    Position = origin,
                    Direction = cells[startIndex].Direction,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    Placeable = true,
                    BlockId = blockId,
                };
            }

            PlaceInfo CreateSingle(PlaceInfo cell, BlockId blockId)
            {
                return new PlaceInfo
                {
                    Position = cell.Position,
                    Direction = cell.Direction,
                    VerticalDirection = cell.VerticalDirection,
                    Placeable = cell.Placeable,
                    BlockId = blockId,
                };
            }

            Vector3Int ToVector(BlockDirection direction)
            {
                return direction switch
                {
                    BlockDirection.North => new Vector3Int(0, 0, 1),
                    BlockDirection.East => new Vector3Int(1, 0, 0),
                    BlockDirection.South => new Vector3Int(0, 0, -1),
                    BlockDirection.West => new Vector3Int(-1, 0, 0),
                    _ => Vector3Int.zero,
                };
            }

            #endregion
        }
    }
}
```
（`BlockDirection`のNorth=+Z等の対応は`BlockDirection.cs`の既存定義と突合し、異なる場合はToVectorを修正。テストのカーブケースの期待値も既存`CommonBlockPlacePointCalculator.GetBlockDirectionWithNextBlock`の座標系に合わせて検証）

- [ ] **Step 4: テストパス確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorRunDecomposerTest"`
Expected: 7件全パス

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: ベルト経路を長尺バリアントへ最小個数分解するデコンポーザを追加"
```

---

### Task 5: BeltConveyorPlaceSystem本体と統合

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlacePointCalculator.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemSelector.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:181`付近
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostPreviewCalculator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuView.cs:57-61`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorPlacePointCalculatorTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTest.cs`（長尺設置・解体テスト追加）

**Interfaces:**
- Consumes: Task 2 `BeltConveyorPlaceFamilyUtil`、Task 4 `BeltConveyorRunDecomposer`、Task 3 `PlaceSystemUtil.SendPlaceBlockProtocol(List<PlaceInfo>)`
- Produces:
  - `BeltConveyorPlaceSystem : IPlaceSystem`（Enable/ManualUpdate/Disable）
  - `BeltConveyorPlacePointCalculator.CalculatePoint(...)` — `CommonBlockPlacePointCalculator`のコンベアモード（1マス刻み・カーブ・傾斜・立体交差）を常時有効で提供
  - `ConstructionCostPreviewCalculator.CalculateAffordableEntityCount(IReadOnlyList<ConstructionRequiredItemElement[]> entityCosts, IEnumerable<IItemStack> inventoryItems)`

- [ ] **Step 1: BeltConveyorPlacePointCalculatorを作成**

`CommonBlockPlacePointCalculator.cs`からコンベア専用ロジックを**コピー**して独立させる（Task 6で元を削除するため、この時点では重複してよい）:
- `CalcPositionsForConveyor`（:53-157）
- `CalcPlaceDirection`のコンベア分岐（:210-334の該当部）と`GetBlockDirectionWithNextBlock`（:336-359）
- `ConveyorOverpassRaiser`呼び出し（:42-45）

シグネチャ（BlockMasterElementへの依存から`EnableConveyorPlacement`/`isLargeBlock`判定を除去し、常時コンベアモード）:
```csharp
public static List<PlaceInfo> CalculatePoint(
    Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection,
    BlockMasterElement representativeBlockMaster,
    Func<PlaceInfo, BlockMasterElement, bool> isNotExistBlock, Func<Vector3Int, bool> isOccupied)
```
出力の各セルには`BlockId`を充填しない（デコンポーザが充填する）。テスト`BeltConveyorPlacePointCalculatorTest.cs`は既存`CommonBlockPlacePointCalculatorTest`のコンベア系ケース（enableConveyorPlacement=true のケース）を移植して同一期待値で検証する。

- [ ] **Step 2: ConstructionCostPreviewCalculatorに累積コスト版を追加**

```csharp
/// <summary>
/// エンティティ列の先頭から所持素材で賄える個数を返す（長尺分解後のコストプレビュー用）
/// Returns how many leading entities the inventory can afford (for decomposed cost preview)
/// </summary>
public static int CalculateAffordableEntityCount(IReadOnlyList<ConstructionRequiredItemElement[]> entityCosts, IEnumerable<IItemStack> inventoryItems)
{
    var remaining = new Dictionary<ItemId, int>();
    foreach (var stack in inventoryItems)
    {
        remaining.TryGetValue(stack.Id, out var current);
        remaining[stack.Id] = current + stack.Count;
    }

    var affordableCount = 0;
    foreach (var cost in entityCosts)
    {
        if (cost == null || cost.Length == 0) { affordableCount++; continue; }

        // 全素材が足りる場合のみ消費を確定して次へ
        // Advance only when every material of this entity is affordable, then commit consumption
        var canAfford = true;
        foreach (var requiredItem in cost)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
            remaining.TryGetValue(itemId, out var held);
            if (held < requiredItem.Count) { canAfford = false; break; }
        }
        if (!canAfford) break;

        foreach (var requiredItem in cost)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
            remaining[itemId] -= requiredItem.Count;
        }
        affordableCount++;
    }
    return affordableCount;
}
```

- [ ] **Step 3: BeltConveyorPlaceSystemを作成**

`CommonBlockPlaceSystem.cs`を雛形に新規作成。差分は以下（それ以外の回転・高さオフセット・クリック開始位置等のフローは同一）:

1. `SetCurrentPlaceInfo`相当: `BeltConveyorPlacePointCalculator.CalculatePoint`でセル列を計算後、
```csharp
// ファミリー定義を解決し、セル列を長尺エンティティ列へ分解
// Resolve the family definition and decompose cells into length-variant entities
BeltConveyorPlaceFamilyUtil.TryGetFamily(context.SelectedBlockId.Value, out var beltParam);
var variants = BeltConveyorPlaceFamilyUtil.GetStraightVariantsDesc(beltParam);
var upBlockId = MasterHolder.BlockMaster.GetBlockId(beltParam.UpBlockGuid);
var downBlockId = MasterHolder.BlockMaster.GetBlockId(beltParam.DownBlockGuid);
_currentPlaceInfos = BeltConveyorRunDecomposer.Decompose(cellInfos, variants, upBlockId, downBlockId);
```
2. コスト判定: `MarkInsufficientItemPreviewsAsNotPlaceable`相当を`CalculateAffordableEntityCount`版に変更（各エンティティのコストは`MasterHolder.BlockMaster.GetBlockMaster(info.BlockId).RequiredItems`で取得し、賄えない後続エンティティを`Placeable=false`）
3. 電線プレビュー（`ElectricWireAutoConnectPreview`）は持たない
4. 送信: `PlaceSystemUtil.SendPlaceBlockProtocol(_currentPlaceInfos.Where(p => p.Placeable).ToList())`

プレビューは既存`PlacementPreviewBlockGameObjectController`をそのまま利用（Task 3で`placeInfo.BlockId`参照になっているため、長尺・斜面プレハブが自動で混在表示される）。200行を超える場合はコスト判定部を`BeltConveyorCostPreviewMarker.cs`等に分離。

- [ ] **Step 4: セレクタ・DI・ビルドメニュー統合**

- `PlaceSystemSelector.cs`: コンストラクタに`BeltConveyorPlaceSystem`を追加し、`GetCurrentPlaceSystem`のSelectedBlockId分岐（:71-74）の直前に挿入:
```csharp
// ビルドメニュー選択がベルトファミリーなら専用設置システムを使う
// Route belt-family build menu selections to the dedicated place system
if (context.SelectedBlockId.HasValue && BeltConveyorPlaceFamilyUtil.TryGetFamily(context.SelectedBlockId.Value, out _))
    return _beltConveyorPlaceSystem;
```
placeMode switch（:51-59）にも`PlaceModeConst.BeltConveyor => _beltConveyorPlaceSystem`を追加（防御的網羅）。
- `MainGameStarter.cs:181`付近: `builder.Register<BeltConveyorPlaceSystem>(Lifetime.Singleton);`
- `BuildMenuView.RebuildBlockList`（:57-61）: フィルタに`.Where(b => !BeltConveyorPlaceFamilyUtil.IsHiddenVariant(b.BlockGuid))`を追加（up/down/長尺を一覧から除外）

- [ ] **Step 5: サーバー側の長尺設置・解体テストを追加**

`PlaceBlockProtocolTest.cs`に追加:

```csharp
[Test]
public void 長尺ベルトは全セルを占有しコスト1セットで設置される()
{
    var (packet, serviceProvider) = CreateServer();
    GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor3, 1);

    var placeInfos = new List<PlaceInfo>
    {
        new() { Position = new Vector3Int(30, 0, 10), Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = ForUnitTestModBlockId.GearBeltConveyor3 },
    };
    packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext());

    // 3セル全て同一ブロックとして占有される
    // All three cells are occupied by the same block entity
    var block = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(30, 0, 10));
    Assert.IsNotNull(block);
    Assert.AreEqual(block, ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(30, 0, 12)));

    // コストは1セットのみ消費（素材残0）
    // Exactly one cost set consumed (no materials remain)
    AssertInventoryEmptyOfRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor3);
}
```

`RemoveBlockRefundTest.cs`に追加: 長尺3連を設置→解体し、1セット（1連と同額）が返却されることを検証（既存テストの解体手順を流用）。

- [ ] **Step 6: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyor|PlaceBlockProtocolTest|RemoveBlockRefundTest"`
Expected: 全パス

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: BeltConveyorPlaceSystemを実装しセレクタ・DI・ビルドメニューへ統合"
```

---

### Task 6: 垂直オーバーライド機構とenableConveyorPlacementの削除

**Files:**
- Modify: `VanillaSchema/blocks.yml:877-905`（overrideVerticalBlock/enableConveyorPlacement削除）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs:8`
- Delete: `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BlockMasterExtension.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs`（:18-19の呼び出し2行、:181-216、:255-298を削除）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockFromHotBarProtocol.cs:53`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GearChainPoleExtendProtocol.cs:53`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RailConnectWithPlacePierProtocol.cs:49`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/ElectricWireExtendService.cs:43`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlacePointCalculator.cs`（コンベア分岐一式削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/CommonBlockPlacePointCalculatorTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConveyorOverpass/ConveyorOverpassConveyanceTest.cs:97`
- Modify: テスト用master 2箇所の`blocks.json`（overrideVerticalBlock/enableConveyorPlacementキー除去）

- [ ] **Step 1: スキーマから2フィールドを削除し再生成**

`blocks.yml`から`overrideVerticalBlock`（:877-901）と`enableConveyorPlacement`（:902-905）を削除。`_CompileRequester.cs`のdummyTextを更新。

Run: `uloop compile --project-path ./moorestech_client`
Expected: `OverrideVerticalBlock`/`EnableConveyorPlacement`参照箇所のコンパイルエラー多数（これがTODOリストになる）

- [ ] **Step 2: サーバー側の参照を削除**

- `BlockMasterExtension.cs`削除（`GetVerticalOverrideBlockId`本体）
- `PlaceBlockFromHotBarProtocol.cs:53`: `blockId = blockId.GetVerticalOverrideBlockId(placeInfo.VerticalDirection);` の行を削除（blockIdをそのまま使用）
- `GearChainPoleExtendProtocol.cs:53` / `RailConnectWithPlacePierProtocol.cs:49` / `ElectricWireExtendService.cs:43`: `.GetVerticalOverrideBlockId(...)`の呼び出し部分を除去（電柱・橋脚は垂直オーバーライド未使用のため恒等変換だった）
- `BlockMasterUtil.cs`: `OverrideVerticalBlockValidation`/`OverrideVerticalRequiredItemsValidation`とヘルパ（`ValidateOverrideRequiredItems`/`IsSameRequiredItems`）、呼び出し2行（:18-19）を削除。`ExistsBlockGuid`は他で使用するため残す

- [ ] **Step 3: クライアント側のコンベア分岐を削除**

`CommonBlockPlacePointCalculator.cs`:
- `isLargeBlock`/`enableConveyorPlacement`判定（:32-34）、`CalcPositionsForConveyor`（:53-157）、コンベア用`CalcPlaceDirection`分岐、`GetBlockDirectionWithNextBlock`（:336-359）、`ConveyorOverpassRaiser`呼び出し（:42-45）を削除（Task 5で`BeltConveyorPlacePointCalculator`へ移植済み）
- BlockId充填（Task 3 Step 5で追加した箇所）を`info.BlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMasterElement.BlockGuid);`（自身のID固定）に単純化
- `ConveyorOverpassRaiser`等のOverpass 3クラスは`BeltConveyorPlacePointCalculator`が参照し続けるため削除しない

- [ ] **Step 4: テストの追随**

- `CommonBlockPlacePointCalculatorTest.cs`: `new BlockMasterElement(...)`のコンストラクタ引数から削除2フィールド分を除去。コンベアモードのテストケースはTask 5で`BeltConveyorPlacePointCalculatorTest`に移植済みのため、こちらからは削除
- `ConveyorOverpassConveyanceTest.cs:97`: `GetVerticalOverrideBlockId`を`BeltConveyorPlaceFamilyUtil`＋`BeltConveyorRunDecomposer`ベースの解決に置き換え（Up/Downは`beltParam.UpBlockGuid`/`DownBlockGuid`から解決）
- 他の`new BlockMasterElement(`手書き箇所をgrepし、全て引数を修正: `grep -rn "new BlockMasterElement(" --include="*.cs" moorestech_server moorestech_client`

- [ ] **Step 5: テスト用masterのJSONクリーンアップ**

`forUnitTest/master/blocks.json`と`EditModeInPlayingTestMod/master/blocks.json`から全ブロックの`"overrideVerticalBlock"`と`"enableConveyorPlacement"`キーを除去（jqまたはpythonワンライナー。編集後にJSONバリデーション）。

- [ ] **Step 6: 全テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceSystem|PlaceBlock|BeltConveyor|ConveyorOverpass|BlockMaster"`
Expected: 全パス

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "refactor: 垂直オーバーライド機構とenableConveyorPlacementを削除"
```

---

### Task 7: 本番マスタデータ投入（moorestech_master）

**Files:**
- Create: `/Users/katsumi/moorestech_master/tools/belt_variant_migration/migrate.py`
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/items.json`
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/placeSystem.json`
- Modify: `/Users/katsumi/moorestech/.moorestech-external-revisions.json`（ピン更新）

**Interfaces:**
- Consumes: 冒頭の対象ファミリー表のGuid群
- Produces: 12長尺ブロック（歯車2種×{2,3連}＋電気2種×{2,3,4,5連}）、対応する12隠しアイテム、BeltConveyorエントリ7件

- [ ] **Step 1: mooreseditor終了確認と作業ブランチ**

mooreseditor.appが起動中なら終了（起動中はJSON編集が数秒で書き戻される）。moorestech_masterは`plan2-master-migration`ブランチで作業を継続する。

- [ ] **Step 2: 移行スクリプトを書く**

`tools/belt_variant_migration/migrate.py`（plan2の`tools/plan2_migration/`と同じ流儀。一度きり実行・非冪等でよいが、実行済み検出（新Guidの存在チェック）で二重実行を防ぐこと）:

処理内容:
1. **blocks.json**: 全ブロックから`overrideVerticalBlock`/`enableConveyorPlacement`キーを削除
2. **blocks.json**: 12長尺ブロックを追加。各ファミリーの1連エントリをdeep-copyし、以下をパッチ:
   - `blockGuid`: 新規UUID（スクリプト内にハードコードした事前生成値。`uuidgen`で12個生成して埋め込む）
   - `name`: `直線歯車ベルトコンベア(2連)`のように`(N連)`サフィックス
   - `itemGuid`: Step 3で追加する対応隠しアイテムのGuid
   - `blockSize`: `[1,1,N]`
   - `blockParam.beltConveyorItemCount`: 1連の値×N
   - `blockParam.timeOfItemEnterToExit`: 1連の値×N
   - `inventoryConnectors.inputConnects`: 1連のinputエントリをセル毎に複製（offset `[0,0,i]`、connectorGuidは新規UUID）
   - `inventoryConnectors.outputConnects`: 先頭セルのみ（offset `[0,0,N-1]`、新規UUID）
   - 歯車系のみ`blockParam.gear.gearConnects`: セル毎に複製（offset `[0,0,i]`、新規UUID）
   - `blockPrefabAddressablesPath`: `{1連のパス} {N}`（例 `Vanilla/Block/gear belt conveyor 3`。Task 8のプレハブ登録と一致させる）
   - `requiredItems`: 1連と同一（=旧レシピ1セット）
   - `sortPriority`: 1連と同値（メニュー非表示のため実質不使用）
   - `initialUnlocked`: false
3. **items.json**: 12隠しアイテムを追加（既存の`上り歯車ベルトコンベア`アイテムエントリ（itemGuid `45ab6580-83fa-4696-a4e4-10ed1be294c8`）をdeep-copyし、itemGuid=新規UUID・name=ブロックと同名に変更。プラン5でitemGuidフィールドごと削除される暫定措置）
4. **placeSystem.json**: BeltConveyorエントリ7件を追加（priority 140〜200、usePlaceItems=[]、冒頭の表のとおり。`straightBlocks`は`{ "length": N, "blockGuid": ... }`形式で**長さを明示設定**する。例: 歯車ベルトは`[{length:1,1連Guid},{length:2,2連Guid},{length:3,3連Guid}]`、電気ベルトはlength 1〜5の5件。基本土台と分岐器2種はup/down=自身Guid・straightBlocks=`[{length:1, 自身Guid}]`）

- [ ] **Step 3: 実行と検証**

Run: `cd /Users/katsumi/moorestech_master && python3 tools/belt_variant_migration/migrate.py`
Expected: 追加12ブロック・12アイテム・7エントリのサマリ出力

検証スクリプト（同ディレクトリに`verify.py`）: blocks.jsonに`overrideVerticalBlock`キーが0件、長尺ブロックのblockSize/コネクタoffset/requiredItemsが仕様どおり、placeSystem.jsonの全Guidがblocks.jsonに実在することをassert。

- [ ] **Step 4: Unityでのロード検証**

Unity（moorestech_client）でプレイモードを起動し、マスターバリデーションエラーがないことを確認:

Run: `uloop control-play-mode --project-path ./moorestech_client --action Play`（起動後50秒待機）
Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: マスター関連のエラー0件（プレハブ未作成のためAddressablesロードエラーは長尺ブロック設置時のみ発生し得る=この時点では出ない）。確認後Stop。

- [ ] **Step 5: コミットとピン更新**

```bash
cd /Users/katsumi/moorestech_master && git add -A && git commit -m "feat: ベルト長尺バリアント12種とBeltConveyor placeSystemエントリを追加"
```
コミットハッシュを`/Users/katsumi/moorestech/.moorestech-external-revisions.json`のピンに反映し、moorestech_masterのcheckoutが巻き戻っていないことを確認（`git -C /Users/katsumi/moorestech_master log --oneline -1`）。moorestech側もコミット:
```bash
git add -A && git commit -m "chore: moorestech_masterピンをベルト長尺バリアント追加コミットへ更新"
```

---

### Task 8: 長尺プレハブ作成（uloop execute-dynamic-code）

**Files:**
- Create（Unity経由）: 長尺ベルトプレハブ12個＋Addressables登録
- 参照: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BeltConveyorItemPath.cs`（パスオーサリング対象コンポーネント）

**Interfaces:**
- Consumes: Task 7の`blockPrefabAddressablesPath`命名（`{基準パス} {N}`）
- Produces: 各長尺ブロックのプレビュー・実体表示とベルト上アイテム移動表示

- [ ] **Step 1: 基準プレハブの構造を調査**

`uloop execute-dynamic-code`で基準プレハブ（例: Addressables address `Vanilla/Block/gear belt conveyor`）をロードし、ルートのコンポーネント一覧・子階層・`BeltConveyorItemPath`のSerializeField構造（connector guidペアとBezierPath点列）をダンプする。ダンプ結果に基づき次StepのスクリプトのコンポーネントコピーとPath設定部を確定する。

- [ ] **Step 2: プレハブ生成スクリプトを実行**

`uloop execute-dynamic-code`で12プレハブを生成する。スクリプト骨子（Step 1の結果で細部を調整）:

```csharp
// 基準プレハブをロードし、N連プレハブを組み立てて保存する
// Load the base prefab and assemble/save the N-length prefab variant
var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
foreach (var (baseAddress, lengths, connectorGuidTable) in targets)
{
    var baseEntry = settings.groups.SelectMany(g => g.entries).First(e => e.address == baseAddress);
    var basePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(baseEntry.AssetPath);

    foreach (var n in lengths)
    {
        var root = new GameObject(basePrefab.name + " " + n);
        // 見た目: 基準モデルをNセル分タイル配置（ルートのBlockGameObject系コンポーネントはコピー）
        // Visuals: tile the base model N times; copy root-level BlockGameObject components
        for (var i = 0; i < n; i++)
        {
            var segment = UnityEngine.Object.Instantiate(basePrefab, root.transform);
            segment.transform.localPosition = new Vector3(0, 0, i);
            // 子側の重複コンポーネント（BlockGameObject/BeltConveyorItemPath等）はDestroyImmediateで除去
            // Strip duplicated logic components from segments, keep meshes/visuals only
        }
        // ルートへ必須コンポーネントを移植し、BeltConveyorItemPathへ直線パスを設定
        // Attach required components to the root and author straight item paths
        // （入力コネクタguid(セルi) → 出力コネクタguidのペア毎に、z=i+0.5からz=n-0.5への直線Bezier）

        var dir = System.IO.Path.GetDirectoryName(baseEntry.AssetPath);
        var assetPath = dir + "/" + root.name + ".prefab";
        var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        var entry = settings.CreateOrMoveEntry(UnityEditor.AssetDatabase.AssetPathToGUID(assetPath), baseEntry.parentGroup);
        entry.address = baseAddress + " " + n;
        UnityEngine.Object.DestroyImmediate(root);
    }
}
UnityEditor.AssetDatabase.SaveAssets();
```
connectorGuidTableはTask 7のmigrate.pyが出力した各長尺ブロックのコネクタGuid（入力セル毎＋出力）を使用する（migrate.pyにGuid対応表をJSON出力させておくと確実）。

- [ ] **Step 3: 表示検証**

プレイモードで各ファミリーの長尺ブロックを`va:placeBlock`スニペットで設置し、`uloop screenshot`でGame Viewを確認。ベルトへアイテムを流し、長尺上のアイテム移動表示が破綻しないことを目視確認。

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: Addressablesロードエラー・NullReference無し

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: ベルト長尺バリアントのプレハブとAddressables登録を追加"
```
（.metaファイルはUnity生成のものをそのままコミット）

---

### Task 9: E2E検証と申し送り更新

**Files:**
- Modify: `docs/superpowers/plans/2026-07-05-satisfactory-placement-handoff.md`

- [ ] **Step 1: 全テストスイート実行**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "."`（全EditModeテスト。ドメインリロードエラー時は45秒待機リトライ）
Expected: 全パス（既存失敗があれば本プラン起因かをmasterブランチとの差で切り分け）

- [ ] **Step 2: 実機プレイ検証**

プレイモードで以下を通しで確認（uloopスニペット＋目視）:
1. ビルドメニューに代表4ベルト＋基本土台のみ表示（上り/下り/長尺が出ていない）
2. 歯車ベルトを9マスドラッグ→プレビューが3連×3で表示→設置→素材が3セットだけ減る
3. 7マスドラッグ→3連×2＋1連、素材3セット消費（切り上げ課金）
4. 傾斜を含むドラッグ→斜面セルが上り/下りブロックで設置される
5. 3連を解体→1セット返却（増殖なし）
6. 素材不足時→賄える分だけ緑プレビュー
7. 基本土台のドラッグ設置が従来どおり動く（BeltConveyorエントリ経由）

- [ ] **Step 3: 申し送り文書更新**

`2026-07-05-satisfactory-placement-handoff.md`に追記:
- ベルト4種は長尺バリアント方式に移行済み（本プラン参照）。プラン4の「除外9ブロックへのrequiredItems投入」時に旧レシピ産出数>1がないか要チェック（今回の該当はベルト4種のみだった）
- プラン5への影響: `PlaceBlockFromHotBarProtocol`は垂直オーバーライド呼び出しが除去済み。ベルト旧アイテム＋今回追加の隠しアイテム12個は、itemGuidフィールド削除時に一括削除対象
- 垂直オーバーライド機構は完全削除済み（スキーマ・コード・データ）

- [ ] **Step 4: 最終Commit**

```bash
git add -A && git commit -m "docs: ベルト長尺バリアント移行の完了を申し送りに反映"
```

---

## Self-Review結果

- **Spec coverage**: 垂直オーバーライド廃止（Task 6）、BeltConveyor placeSystemマスター（Task 1）、上下＋Nメートル定義（Task 1/7）、最小ブロック数分解プレビュー・設置（Task 4/5）、長尺=1セットコスト＝旧経済保存（Task 5/7、端数切り上げはデコンポーザの貪欲割当で成立）— 全要件にタスクあり
- **既知の不確定要素**（実装時に確認するポイントとして明示）: (1) SourceGenerator生成型名`BeltConveyorPlaceParam`は再生成後に確認（Task 1 Step 4） (2) `BlockDirection`の軸対応はToVector実装時に突合（Task 4 Step 3） (3) プレハブ内部構造はTask 8 Step 1の調査で確定
- **Type consistency**: `BeltConveyorPlaceFamilyUtil`のシグネチャはTask 2/3/5/6で一致、`PlaceInfo.BlockId`はTask 3で導入しTask 4/5が消費、`SendPlaceBlockProtocol(List<PlaceInfo>)`はTask 3で変更しTask 5が使用 — 整合確認済み
