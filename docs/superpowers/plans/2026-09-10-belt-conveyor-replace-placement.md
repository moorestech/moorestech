# ベルトコンベア張替え設置（ファミリーのロール対応） Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 既設ベルトコンベアを起点にドラッグすると、既設の座標・向き・ロール（直線/上り/下り/分岐器）を保ったまま BlockId だけを手持ちファミリーの同ロールへ差し替える「張替え設置」を、サーバー・クライアント・マスタの3層で実装する。

**Architecture:** マスタの `beltConveyorFamilies` に分岐器ロール（`splitterBlockGuid`）を足し、1行＝1ティアにする。クライアントは既設ライン追従の張替え経路（`BeltReplaceRunBuilder`）で `PlaceInfo.IsReplace` 付きのセル列を作り、既存の `va:placeBlock` で送る。サーバーは `PlaceBlockProtocol` が isReplace セルを `BeltReplacePlacementService` へ委譲し、「撤去返却（財布経由）→ 撤去 → 設置 → 設置消費（財布経由）→ 搬送品を復元（進行率は歯車ベルトでは維持されない。R11 参照）」を1セルごとに行う。Undo は逆張替えレコードで同経路を再送する。

**Tech Stack:** Unity C# (moorestech_server / moorestech_client), MessagePack, NUnit (uloop run-tests), mooresmaster SourceGenerator (VanillaSchema/*.yml), プレイテストDSL (Client.Playtest)。

## Requirements

設計ADR: `docs/adr/0055-belt-conveyor-replace-placement-by-family-role.md`（本planの全タスク共通の裁定）。用語: `CONTEXT.md`「ベルトコンベアの張替え」節。

- R1 `beltConveyorFamilies` の各行に `splitterBlockGuid`（optional・blocks外部キー）を追加し、分岐器の単独ファミリー行を直線側の行へ吸収する。受け入れ: 実マスタ（moorestech_master）が 8行→4行、forUnitTest が 4行→3行、バリデータが分岐器メンバーの blockType/1x1x1/多重所属を検証する。
- R2 ロールは 直線 / 上り / 下り / 分岐器 の4つ。`BeltConveyorFamily` から「BlockId→ロール」「ロール→BlockId」を引ける。受け入れ: ユニットテストで分岐器を含む往復解決が通る。
- R3 ファミリー同士は無制限に相互張替え可。張替え可否の関係はマスタに持たない。受け入れ: スキーマに可否フィールドが無く、サーバーは「既設・手持ちの両方がファミリー所属」＋「同ロール」だけを検証する。
- R4 財布・解放の直線代表正規化は坂ロール（上り/下り）に限定し、分岐器は自身の財布・解放を保つ。受け入れ: `ConstructionWalletUtil.ResolveWalletBlockId(分岐器)==分岐器`、`(上り)==直線`。
- R5 張替えドラッグの発動は「起点セル（天面ヒットで1段浮いた座標は1段下も見る）に既設ベルトファミリーブロックがある」ときだけ。単クリックは起点=終点の張替え。空き地起点の通常ドラッグは既設ベルトに重なっても張り替えない。受け入れ: クライアントユニットテストで起点解決と通常経路不変を確認。
- R6 張替えは形状維持。既設の座標・向き・ロールを維持し BlockId だけ手持ちファミリーの同ロールへ。手持ちが坂/分岐器でも「ファミリー指定」として扱う。受け入れ: 手持ち=上り鉄で木の直線ラインをなぞると鉄の直線に張り替わる。
- R7 既設ラインの高さは自動追従。XZ は現行のドラッグ経路（L字可）、Y は直前に見つけた既設ベルトの Y±1 で探し、見つからないセルは直前の Y を保つ。張替え中は立体交差の持ち上げ・坂の自動判定・高さオフセットを使わない。受け入れ: 坂で1段上がるラインを平面ドラッグで全セル拾う。
- R8 既設ベルトの無いセル（空・別種ブロック・フィルター分岐器）は何もせず飛ばして続行する。受け入れ: `[木][木][空][木]` を4セルなぞると3セルが張り替わる。
- R9 手持ちファミリーに対応ロールが無いセルはプレビュー不可色＋理由ツールチップ（`ReplaceRoleMissing`）で送信せず、後続セルは続行する。同ファミリー同ロールのセルは no-op（送信しない・プレビューにも出さない）。
- R10 サーバーは `PlaceBlockProtocol` のセル単位 `isReplace` で分岐し、1セルにつき「旧ブロックの撤去返却（財布）→撤去→設置→新ブロックの設置消費（財布）」をアトミックに行う。既設/手持ちがファミリー外・ロール不一致・未解放・返却不能・コスト不足は拒否理由をログに出してそのセルだけスキップ。無料設置（デバッグ）時はコスト検証・消費・返却を全て飛ばす。
- R11 搬送中アイテムは新ブロックへ引き継ぎ、失わない。収まらない分はプレイヤーインベントリへ、それも入らないならそのセルは失敗（ロスト・地面ドロップ禁止）。**進行率の維持は歯車ベルトでは効かないため保証しない**（2026-09-11 ユーザー裁定・別タスクへ送済み）: 設置直後の搬送時間は `VanillaGearBeltConveyorTemplate.cs:45-47` の `float.PositiveInfinity` と `GearBeltConveyorComponent.cs:41,48` の `SetTicksOfItemEnterToExit(uint.MaxValue)` により停止中扱いで、復元は入口スロットへ置かれ、動力復帰時に `VanillaBeltConveyorInventoryItem.cs:55` の `ResetTicksOnSpeedRecovery` が進捗を0へ戻す。受け入れ基準は「張替え後も品がロストしていない」までとし、進行率はアサートしない。
- R12 `BlockRemoveReason.Replace` を追加し、張替えによる撤去は `Replace` で発火する。
- R13 プレビューは張替えセルを専用色（`MaterialConst.ReplaceColor`）で塗り分ける。
- R14 Undo は逆張替え。張替えレコードを積み、Ctrl+Z で同セルを旧 BlockId へ isReplace 送信で差し戻す。既に別ブロックへ変わっているセルは触らない。
- R15 フィルター分岐器（blockType FilterSplitter）は対象外。ファミリーに載せず、張替えドラッグでは R8 の「無いセル」として飛ぶ。
- R16 moorestech_master の JSON 変更は push＋PR を作り、本repoのピン（`.moorestech-external-revisions.json`）をそのコミットへ更新する。
- R17 unityプレイ録画テストで「木の歯車ラインを鉄の歯車ティアへ張替え（直線・上り・分岐器を含む）」を通しで検証する。
- やらないこと: 向きの引き直し（撤去→再設置で行う）／張替え可否の制約定義／フィルター分岐器の張替え／坂単体設置（ADR 0050・別タスク moorestech-drn）／コストの精密先読み（クライアントは張替えセルを新コスト要求として数える簡略化のまま）。

## Global Constraints

- AGENTS.md 全規約: partial禁止・`Func<>`新設禁止・1ファイル200行以下・1ディレクトリ10ファイル以下・デフォルト引数禁止・try-catch原則禁止・イベントはUniRx・`#region Internal` はメソッド内ローカル関数限定・日本語→英語の2行コメント・fail-closed経路は必ずログ。
- `.metaファイル` は手作成しない（Unity起動で生成されたものをコミットする）。
- 時間計測は `GameUpdater` のティックのみ。
- スキーマ変更は edit-schema スキルの手順（`VanillaSchema/blocks.yml` 編集 → `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` を変更 → コンパイル）。`Mooresmaster.Model.*` は手で書かない。生成プロパティ名は `BeltConveyorFamiliesElement.SplitterBlockGuid`（`Guid?`）。
- foreignKey 追加後は validate-schema スキルに従い C# バリデータを追加する（Task 1 で実施）。
- 作業場所: worktree `/Users/katsumi/moorestech-belt-replace`（branch `feature/belt-conveyor-replace-placement`）。コマンドは全てこの worktree ルートで実行する（最初に `pwd` 確認）。Unity を初めて起動する前に `Library/` をメイン worktree（`/Users/katsumi/moorestech/moorestech_client/Library`）からコピーする。
- コンパイル: `uloop compile --project-path ./moorestech_client`。テスト: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "<クラス名>"`（`--filter-type regex` 可）。「Unity is reloading」エラーは45秒待って再試行。
- `.moorestech-external-revisions.json` と `_CompileRequester.cs` は Unity が自動書き換えする。意図した変更（Task 1 の dummyText、Task 7 のピン）以外はコミットしない。
- コミットは各タスク末尾で行い、メッセージ末尾に `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>` と `Claude-Session: https://claude.ai/code/session_01MXrZXXES68AH182zKQmtpw` を付ける。
- テスト用ブロック定数（`Tests.Module.TestMod.ForUnitTestModBlockId`）: `BeltConveyorId`（TestBeltConveyor, 無料, PPC=1）、`SmallGearBeltConveyor`（無料, PPC=1）、`GearBeltConveyor`（コスト item 1234-3 ×1 + 1234-4 ×1, PPC=3）、`TestGearBeltConveyorUp/Down`、`GearBeltConveyorSplitter`、`MachineId`。Task 1 で `SmallGearBeltConveyorSplitter` を追加する。

---

## File Structure

**サーバー（新規）**
- `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorRole.cs` — ロール enum
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/BeltTransitItem.cs` — 退避した搬送品（ItemId / ItemInstanceId / 進行率）
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/BeltConveyorTransitCarryOver.cs` — 搬送品の退避・復元（ベルトコンポーネントを読む/書く static util）
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/BeltReplacePlacementService.cs` — 1セルの張替え（検証→返却→撤去→設置→消費→復元）
- テスト: `Tests/UnitTest/Game/BeltConveyorRoleTest.cs`、`Tests/CombinedTest/Core/BeltConveyorTransitCarryOverTest.cs`、`Tests/CombinedTest/Server/PacketTest/BeltReplacePlaceTest.cs`、`Tests/CombinedTest/Server/PacketTest/BeltReplacePlaceEdgeTest.cs`

**サーバー（変更）**
- `VanillaSchema/blocks.yml` — `splitterBlockGuid`
- `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — dummyText
- `moorestech_server/Assets/Scripts/Core.Master/Validator/BeltConveyorFamilyValidator.cs` — 分岐器メンバー検証
- `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorFamily.cs` — SplitterBlockId・ロール解決
- `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorPlaceFamilyUtil.cs` — 分岐器メンバー・坂代表解決
- `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorPlacementUnlockSourceMap.cs` — 坂代表のみ直線へ
- `moorestech_server/Assets/Scripts/Game.Construction/Wallet/ConstructionWalletUtil.cs` — 坂代表のみ直線へ
- `moorestech_server/Assets/Scripts/Game.Block.Interface/BlockRemoveReason.cs` — Replace
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlacePacketDto.cs` — IsReplace
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs` — isReplace 分岐
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/VanillaBeltConveyorComponent.cs` — `TicksOfItemEnterToExit` 公開・`TryRestoreItem`
- `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json` — 分岐器吸収＋`SmallGearBeltConveyorSplitter` 追加
- `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs` — 定数追加
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorFamilyTest.cs` — 分岐器の期待値
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTestSupport.cs` — 張替えペイロード等のヘルパ

**クライアント（新規）**
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Replace/BeltReplaceRunBuilder.cs` — 既設ライン追従の張替え経路
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/ReplaceOperationRecord.cs` — 逆張替えレコード
- テスト: `Client.Tests/PlaceSystem/BeltConveyor/BeltReplaceRunBuilderTest.cs`、`Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorHoldingBlockTest.cs`、`Client.Tests/PlaceSystem/Undo/ReplaceOperationRecordTest.cs`

**クライアント（変更）**
- `.../PlaceSystem/BeltConveyor/Parts/BeltConveyorHoldingBlock.cs` — Role・RunUp/RunDown
- `.../PlaceSystem/BeltConveyor/Parts/BeltConveyorStraightCellBlockResolver.cs` — 引数を BlockId 直指定へ
- `.../PlaceSystem/BeltConveyor/Parts/BeltConveyorPlaceRunBuilder.cs` — 張替え分岐
- `.../PlaceSystem/BeltConveyor/Parts/BeltConveyorPlacementBlockReason.cs` — ReplaceRoleMissing
- `.../PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs` — 張替え送信分岐
- `.../PlaceSystem/Util/PlaceBlockProtocolSender.cs` — 張替え送信＋レコード
- `.../PlaceSystem/Common/PreviewController/PlacementPreviewBlockGameObjectController.cs` / `BlockPreviewObject.cs` — 張替え色
- `moorestech_client/Assets/Scripts/Client.Common/MaterialConst.cs` — ReplaceColor
- `Localization/localization.csv` — ツールチップ1行
- `Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorStraightCellBlockResolverTest.cs` — 署名追従

**別repo / 検証**
- `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（worktree経由）・`.moorestech-external-revisions.json`
- `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-replace-tier-via-ui.cs`

---

### Task 1: マスタ（分岐器ロール）とファミリーモデル・代表解決・バリデータ

**Files:**
- Modify: `VanillaSchema/blocks.yml:1092-1122`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs:45`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorRole.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorFamily.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorPlaceFamilyUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorPlacementUnlockSourceMap.cs:13-19`
- Modify: `moorestech_server/Assets/Scripts/Game.Construction/Wallet/ConstructionWalletUtil.cs:12-15`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BeltConveyorFamilyValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorFamilyTest.cs:18-32`
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorRoleTest.cs`

**Interfaces:**
- Consumes: `MasterHolder.BlockMaster.Blocks.BeltConveyorFamilies`（生成物。`SplitterBlockGuid : Guid?` が生える）
- Produces:
  - `enum BeltConveyorRole { Straight, Up, Down, Splitter }`（namespace `Game.Block.Interface.Extension`）
  - `BeltConveyorFamily(BlockId straightBlockId, BlockId? upBlockId, BlockId? downBlockId, BlockId? splitterBlockId)`、`public readonly BlockId? SplitterBlockId`
  - `bool BeltConveyorFamily.TryGetRole(BlockId blockId, out BeltConveyorRole role)`
  - `bool BeltConveyorFamily.TryGetBlockIdOfRole(BeltConveyorRole role, out BlockId blockId)`
  - `bool BeltConveyorFamily.TryGetSlopeDirection(BlockId, out BlockVerticalDirection)`（既存・維持）
  - `static BlockId BeltConveyorPlaceFamilyUtil.ResolveSlopeRepresentativeBlockId(BlockId blockId)`（坂→直線、他は自身）
  - `static Guid BeltConveyorPlaceFamilyUtil.ResolveSlopeRepresentativeGuid(Guid blockGuid)`
  - `ForUnitTestModBlockId.SmallGearBeltConveyorSplitter`（guid `4e0b2f6a-9c1d-4e0a-8b2f-1a2b3c4d5e60`）

- [ ] **Step 1: スキーマに splitterBlockGuid を追加する**

`VanillaSchema/blocks.yml` の `beltConveyorFamilies` を次の内容に置き換える（`downBlockGuid` の後・`straightBlockGuid` の前に挿入）:

```yaml
# ベルトコンベアの設置ファミリー定義。1エントリが1ティア（直線・任意の上り/下り・任意の分岐器）を表す
# Belt conveyor placement families; each entry is one tier (straight, optional up/down, optional splitter)
- key: beltConveyorFamilies
  type: array
  items:
    type: object
    properties:
    # 斜面を持たないファミリーでは上り/下りを持たない
    # Families without slopes omit up/down
    - key: upBlockGuid
      type: uuid
      optional: true
      foreignKey:
        schemaId: blocks
        foreignKeyIdPath: /data/[*]/blockGuid
        displayElementPath: /data/[*]/name
    - key: downBlockGuid
      type: uuid
      optional: true
      foreignKey:
        schemaId: blocks
        foreignKeyIdPath: /data/[*]/blockGuid
        displayElementPath: /data/[*]/name
    # ティアの分岐器。張替えで既設分岐器を同ロールへ差し替えるために使う
    # The tier's splitter; replace placement maps an existing splitter to this role
    - key: splitterBlockGuid
      type: uuid
      optional: true
      foreignKey:
        schemaId: blocks
        foreignKeyIdPath: /data/[*]/blockGuid
        displayElementPath: /data/[*]/name
    # ファミリーの1セル直線コンベア
    # The family's single-cell straight conveyor
    - key: straightBlockGuid
      type: uuid
      foreignKey:
        schemaId: blocks
        foreignKeyIdPath: /data/[*]/blockGuid
        displayElementPath: /data/[*]/name
```

`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` の値を `"belt-family-splitter-role"` に変更する。

- [ ] **Step 2: forUnitTest の blocks.json を更新する**

`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`:

(a) `beltConveyorFamilies` を次の3行にする（分岐器行を吸収し、SmallGear に新分岐器を付ける）:

```json
"beltConveyorFamilies": [
  { "straightBlockGuid": "00000000-0000-0000-0000-000000000003" },
  { "straightBlockGuid": "00000000-0000-0000-0000-000000000030",
    "splitterBlockGuid": "4e0b2f6a-9c1d-4e0a-8b2f-1a2b3c4d5e60" },
  { "straightBlockGuid": "00000000-0000-0000-0000-000000000015",
    "upBlockGuid": "00000000-0000-0000-0000-0000000000a1",
    "downBlockGuid": "00000000-0000-0000-0000-0000000000a2",
    "splitterBlockGuid": "eccb9f59-4439-4caf-9ae8-67da50549040" }
]
```

(b) `data` 配列の `GearBeltConveyorSplitter`（blockGuid `eccb9f59-...`）エントリの直後に、同エントリを丸ごと複製して `name` を `"SmallGearBeltConveyorSplitter"`、`blockGuid` を `"4e0b2f6a-9c1d-4e0a-8b2f-1a2b3c4d5e60"` に変えたエントリを追加する（他のフィールド・connectorGuid は複製のままでよい。connectorGuid は同一ブロック内で一意であればよく、ブロック間の重複は問題にならない）。

(c) `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs` の L45 `GearBeltConveyorSplitter` の直後に追加:

```csharp
        public static BlockId SmallGearBeltConveyorSplitter => GetBlock("4e0b2f6a-9c1d-4e0a-8b2f-1a2b3c4d5e60");
```

- [ ] **Step 3: BeltConveyorRole と BeltConveyorFamily を書く**

Create `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorRole.cs`:

```csharp
namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// ファミリー内でブロックが担うロール。張替えは既設ロールを手持ちファミリーの同ロールへ写す
    /// Role a block plays within its family; replace placement maps an existing role to the same role of the held family
    /// </summary>
    public enum BeltConveyorRole
    {
        Straight,
        Up,
        Down,
        Splitter,
    }
}
```

Replace `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorFamily.cs` 全文:

```csharp
using Core.Master;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// 解決済みのベルトファミリー（1ティア）。直線は必須、坂・分岐器は任意
    /// A resolved belt family (one tier); straight is required, slopes and splitter are optional
    /// </summary>
    public class BeltConveyorFamily
    {
        public readonly BlockId StraightBlockId;
        public readonly BlockId? UpBlockId;
        public readonly BlockId? DownBlockId;
        public readonly BlockId? SplitterBlockId;

        public BeltConveyorFamily(BlockId straightBlockId, BlockId? upBlockId, BlockId? downBlockId, BlockId? splitterBlockId)
        {
            StraightBlockId = straightBlockId;
            UpBlockId = upBlockId;
            DownBlockId = downBlockId;
            SplitterBlockId = splitterBlockId;
        }

        // メンバーのロールを引く。非メンバーはfalse
        // Resolve a member's role; non-members return false
        public bool TryGetRole(BlockId blockId, out BeltConveyorRole role)
        {
            if (blockId == StraightBlockId) { role = BeltConveyorRole.Straight; return true; }
            if (Matches(UpBlockId, blockId)) { role = BeltConveyorRole.Up; return true; }
            if (Matches(DownBlockId, blockId)) { role = BeltConveyorRole.Down; return true; }
            if (Matches(SplitterBlockId, blockId)) { role = BeltConveyorRole.Splitter; return true; }
            role = BeltConveyorRole.Straight;
            return false;
        }

        // ロールに対応するブロックを引く。ファミリーがそのロールを持たなければfalse
        // Resolve the block for a role; false when the family lacks that role
        public bool TryGetBlockIdOfRole(BeltConveyorRole role, out BlockId blockId)
        {
            BlockId? candidate = role switch
            {
                BeltConveyorRole.Straight => StraightBlockId,
                BeltConveyorRole.Up => UpBlockId,
                BeltConveyorRole.Down => DownBlockId,
                BeltConveyorRole.Splitter => SplitterBlockId,
                _ => null,
            };
            blockId = candidate ?? StraightBlockId;
            return candidate.HasValue;
        }

        // 坂ブロックなら上下どちらの坂かを返す
        // Returns which way the slope goes when the block is a slope
        public bool TryGetSlopeDirection(BlockId blockId, out BlockVerticalDirection verticalDirection)
        {
            if (TryGetRole(blockId, out var role) && role == BeltConveyorRole.Up)
            {
                verticalDirection = BlockVerticalDirection.Up;
                return true;
            }

            if (TryGetRole(blockId, out role) && role == BeltConveyorRole.Down)
            {
                verticalDirection = BlockVerticalDirection.Down;
                return true;
            }

            verticalDirection = BlockVerticalDirection.Horizontal;
            return false;
        }

        private static bool Matches(BlockId? member, BlockId blockId)
        {
            return member.HasValue && member.Value == blockId;
        }
    }
}
```

- [ ] **Step 4: BeltConveyorPlaceFamilyUtil に分岐器と坂代表解決を足す**

Replace `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorPlaceFamilyUtil.cs` 全文:

```csharp
using System;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// beltConveyorFamilies定義からファミリーを解決するドメイン層util
    /// Domain-layer util resolving belt families from beltConveyorFamilies
    /// </summary>
    public static class BeltConveyorPlaceFamilyUtil
    {
        public static bool TryGetFamily(BlockId blockId, out BeltConveyorFamily family)
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            return TryGetFamilyByGuid(blockGuid, out family);
        }

        public static bool TryGetFamilyByGuid(Guid blockGuid, out BeltConveyorFamily family)
        {
            // 全ファミリーエントリを走査しメンバー照合。エントリ数は少数のためキャッシュ不要
            // Scan all family entries for membership; few entries so no cache is needed
            foreach (var element in MasterHolder.BlockMaster.Blocks.BeltConveyorFamilies)
            {
                if (!IsMember(element, blockGuid)) continue;
                family = BuildFamily(element);
                return true;
            }

            family = null;
            return false;
        }

        // 財布・解放の代表。坂ロールだけ直線へ寄せ、直線・分岐器・ファミリー外は自身
        // Wallet/unlock representative: only slope roles map to the straight; straight, splitter and non-members stay themselves
        public static BlockId ResolveSlopeRepresentativeBlockId(BlockId blockId)
        {
            if (!TryGetFamily(blockId, out var family)) return blockId;
            if (!family.TryGetRole(blockId, out var role)) return blockId;
            return role == BeltConveyorRole.Up || role == BeltConveyorRole.Down ? family.StraightBlockId : blockId;
        }

        public static Guid ResolveSlopeRepresentativeGuid(Guid blockGuid)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(blockGuid);
            if (blockId == null) return blockGuid;
            var representative = ResolveSlopeRepresentativeBlockId(blockId.Value);
            return MasterHolder.BlockMaster.GetBlockMaster(representative).BlockGuid;
        }

        private static bool IsMember(BeltConveyorFamiliesElement element, Guid blockGuid)
        {
            return element.StraightBlockGuid == blockGuid ||
                   element.UpBlockGuid == blockGuid ||
                   element.DownBlockGuid == blockGuid ||
                   element.SplitterBlockGuid == blockGuid;
        }

        // ファミリーのGUIDを実行時IDへ解決する
        // Resolve the family's GUIDs to runtime IDs
        private static BeltConveyorFamily BuildFamily(BeltConveyorFamiliesElement element)
        {
            var straightBlockId = MasterHolder.BlockMaster.GetBlockId(element.StraightBlockGuid);
            return new BeltConveyorFamily(straightBlockId, ResolveOptional(element.UpBlockGuid), ResolveOptional(element.DownBlockGuid), ResolveOptional(element.SplitterBlockGuid));
        }

        private static BlockId? ResolveOptional(Guid? blockGuid)
        {
            if (blockGuid == null) return null;
            return MasterHolder.BlockMaster.GetBlockId(blockGuid.Value);
        }
    }
}
```

`moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorPlacementUnlockSourceMap.cs` の `ResolveUnlockSourceId` 本体を次に置き換える:

```csharp
        public Guid ResolveUnlockSourceId(Guid targetId)
        {
            // 坂は直線の解放状態に従い、直線・分岐器・ファミリー外は自身に従う
            // Slopes follow the straight block's unlock state; straight, splitter and non-members follow their own
            return BeltConveyorPlaceFamilyUtil.ResolveSlopeRepresentativeGuid(targetId);
        }
```

クラスの summary コメントを「坂ベルトの解放元をファミリーの直線ブロックへ寄せる（分岐器は自身）／Normalizes a belt slope's unlock source to its family's straight block (splitters keep their own)」に更新する。

`moorestech_server/Assets/Scripts/Game.Construction/Wallet/ConstructionWalletUtil.cs` の `ResolveWalletBlockId` を次に置き換える:

```csharp
        public static BlockId ResolveWalletBlockId(BlockId blockId)
        {
            return BeltConveyorPlaceFamilyUtil.ResolveSlopeRepresentativeBlockId(blockId);
        }
```

summary コメントの「ベルトは直線代表、他は自身」を「坂ベルトは直線代表、分岐器と他は自身」に更新する。

- [ ] **Step 5: バリデータに分岐器メンバー検証を足す**

`moorestech_server/Assets/Scripts/Core.Master/Validator/BeltConveyorFamilyValidator.cs` の `ValidateFamily` ローカル関数を次に置き換える:

```csharp
            string ValidateFamily(BeltConveyorFamiliesElement family)
            {
                var familyLogs = "";

                // 直線は必須、坂・分岐器は任意として同じメンバー規則を検証する
                // Validate the required straight and the optional slopes/splitter with one member rule
                familyLogs += ValidateMember(family.StraightBlockGuid, "straightBlockGuid");
                familyLogs += ValidateOptionalMember(family.UpBlockGuid, "upBlockGuid");
                familyLogs += ValidateOptionalMember(family.DownBlockGuid, "downBlockGuid");
                familyLogs += ValidateOptionalMember(family.SplitterBlockGuid, "splitterBlockGuid");

                // 財布を坂と直線で共有するため、坂だけ直線基準で建設コストと設置数/1セットの一致を要求する。分岐器は自身の財布なので対象外
                // Slopes share the straight block's wallet, so only slopes must match its cost and placementsPerCost; splitters keep their own wallet
                if (!elementByGuid.TryGetValue(family.StraightBlockGuid, out var straight)) return familyLogs;
                familyLogs += ValidateCostMatches(straight, family.UpBlockGuid);
                familyLogs += ValidateCostMatches(straight, family.DownBlockGuid);
                return familyLogs;
            }
```

- [ ] **Step 6: 既存テストの期待値を更新し、ロールのテストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorFamilyTest.cs` の `歯車ベルト系ブロックは単一直線ファミリーとして解決できる` の `Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorDown, family.DownBlockId);` の直後に追加:

```csharp
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyorSplitter, family.SplitterBlockId);
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyorSplitter, out var splitterFamily));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, splitterFamily.StraightBlockId);
```

Create `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorRoleTest.cs`:

```csharp
using System.IO;
using Core.Master;
using Core.Master.Validator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Construction;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    /// <summary>
    /// ファミリーのロール解決と、坂だけを直線代表へ寄せる財布・解放キーを検証する
    /// Verifies family role resolution and that only slopes normalize to the straight representative for wallet/unlock
    /// </summary>
    public class BeltConveyorRoleTest
    {
        private const string NonBeltBlockGuid = "00000000-0000-0000-0000-000000000002";

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 分岐器を含む4ロールを往復解決できる()
        {
            BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor, out var family);

            Assert.IsTrue(family.TryGetRole(ForUnitTestModBlockId.GearBeltConveyorSplitter, out var splitterRole));
            Assert.AreEqual(BeltConveyorRole.Splitter, splitterRole);
            Assert.IsTrue(family.TryGetRole(ForUnitTestModBlockId.TestGearBeltConveyorUp, out var upRole));
            Assert.AreEqual(BeltConveyorRole.Up, upRole);
            Assert.IsTrue(family.TryGetBlockIdOfRole(BeltConveyorRole.Splitter, out var splitterId));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyorSplitter, splitterId);
            Assert.IsTrue(family.TryGetBlockIdOfRole(BeltConveyorRole.Down, out var downId));
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorDown, downId);
            Assert.IsFalse(family.TryGetRole(ForUnitTestModBlockId.MachineId, out _));
        }

        [Test]
        public void 坂を持たないファミリーはロール解決がfalseになる()
        {
            BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.SmallGearBeltConveyor, out var family);

            Assert.IsFalse(family.TryGetBlockIdOfRole(BeltConveyorRole.Up, out _));
            Assert.IsTrue(family.TryGetBlockIdOfRole(BeltConveyorRole.Splitter, out var splitterId));
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyorSplitter, splitterId);
        }

        [Test]
        public void 財布キーは坂だけ直線へ寄り分岐器は自身のまま()
        {
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.TestGearBeltConveyorUp));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyorSplitter, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.GearBeltConveyorSplitter));
            Assert.AreEqual(ForUnitTestModBlockId.MachineId, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.MachineId));
        }

        [Test]
        public void 解放元は坂だけ直線へ寄り分岐器は自身のまま()
        {
            var map = new BeltConveyorPlacementUnlockSourceMap();
            var upGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearBeltConveyorUp).BlockGuid;
            var straightGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockGuid;
            var splitterGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyorSplitter).BlockGuid;

            Assert.AreEqual(straightGuid, map.ResolveUnlockSourceId(upGuid));
            Assert.AreEqual(splitterGuid, map.ResolveUnlockSourceId(splitterGuid));
        }

        [Test]
        public void 非ベルト型は分岐器ロールにできない()
        {
            var blocksJToken = LoadBlocksJson();
            blocksJToken["beltConveyorFamilies"][2]["splitterBlockGuid"] = NonBeltBlockGuid;

            var logs = BeltConveyorFamilyValidator.Validate(new BlockMaster(blocksJToken).Blocks);

            StringAssert.Contains("is not a belt block", logs);
        }

        [Test]
        public void 分岐器は直線とコストが違っても検証エラーにならない()
        {
            var blocksJToken = LoadBlocksJson();
            var splitterGuid = blocksJToken["beltConveyorFamilies"][2]["splitterBlockGuid"].Value<string>();
            FindBlock(blocksJToken, splitterGuid)["placementsPerCost"] = 7;

            var logs = BeltConveyorFamilyValidator.Validate(new BlockMaster(blocksJToken).Blocks);

            StringAssert.DoesNotContain("placementsPerCost must match", logs);
        }

        private static JToken LoadBlocksJson()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            return JToken.Parse(File.ReadAllText(path));
        }

        private static JToken FindBlock(JToken blocksJToken, string blockGuid)
        {
            foreach (var block in blocksJToken["data"])
            {
                if (block["blockGuid"].Value<string>() == blockGuid) return block;
            }

            Assert.Fail($"Block not found: {blockGuid}");
            return null;
        }
    }
}
```

- [ ] **Step 7: コンパイルしてテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（`SplitterBlockGuid` が生成されている。`BeltConveyorStraightCellBlockResolverTest` 等クライアント側は `BeltConveyorFamily` の4引数化でコンパイルエラーになるので、`new BeltConveyorFamily(StraightBlock, UpBlock, DownBlock, null)` / `new BeltConveyorFamily(StraightBlock, null, null, null)` へ追従させる。Task 4 でさらに署名を変える）。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorFamilyTest|BeltConveyorRoleTest|ConstructionPayerWalletTest|PlaceBlockRemainingPlacementTest|RemoveBlockRemainingPlacementTest|PlacementTargetCatalog"`
Expected: 全PASS

- [ ] **Step 8: コミットする**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master moorestech_server/Assets/Scripts/Game.Block.Interface moorestech_server/Assets/Scripts/Game.Construction moorestech_server/Assets/Scripts/Tests.Module moorestech_server/Assets/Scripts/Tests/UnitTest/Game moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor
git commit -m "feat(master): beltConveyorFamiliesに分岐器ロールを追加しファミリーをティア化（ADR-0055）"
```

---

### Task 2: RemoveReason.Replace・DTOのIsReplace・搬送品の退避と復元

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/BlockRemoveReason.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlacePacketDto.cs:14-36, 55-67`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/VanillaBeltConveyorComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/BeltTransitItem.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/BeltConveyorTransitCarryOver.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/BeltConveyorTransitCarryOverTest.cs`

**Interfaces:**
- Consumes: `VanillaBeltConveyorComponent.BeltConveyorItems`（`IReadOnlyList<IOnBeltConveyorItem>`: `RemainingTicks`/`TotalTicks`/`ItemId`/`ItemInstanceId`）、`IBeltConveyorBlockInventoryInserter.GetNextGoalConnector(List<IItemStack>)`
- Produces:
  - `BlockRemoveReason.Replace`
  - `PlaceInfo.IsReplace : bool`（get/set）、`PlaceInfoMessagePack.IsReplace`（`[Key(5)]`）
  - `readonly struct BeltTransitItem(ItemId ItemId, ItemInstanceId ItemInstanceId, double RemainingRate)`
  - `uint VanillaBeltConveyorComponent.TicksOfItemEnterToExit { get; }`
  - `bool VanillaBeltConveyorComponent.TryRestoreItem(ItemId itemId, ItemInstanceId itemInstanceId, double remainingRate)`
  - `static List<BeltTransitItem> BeltConveyorTransitCarryOver.Collect(IItemCollectableBeltConveyor belt)`
  - `static List<BeltTransitItem> BeltConveyorTransitCarryOver.Restore(VanillaBeltConveyorComponent belt, IReadOnlyList<BeltTransitItem> items)`（戻り値は収まらなかった残り）

- [ ] **Step 1: BlockRemoveReason と DTO を拡張する**

`moorestech_server/Assets/Scripts/Game.Block.Interface/BlockRemoveReason.cs` の `ManualRemove` の後に追加:

```csharp
        // 張替え設置による撤去。直後に同セルへ新ブロックが設置される
        // Removal by replace placement; a new block is placed on the same cell right after
        Replace,
```

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlacePacketDto.cs`:
- `PlaceInfoMessagePack` の `[Key(4)] public int BlockIdInt { get; set; }` の直後に追加:

```csharp
        // 既設ベルトを同セルで差し替える張替えセルか
        // Whether this cell replaces an existing belt on the same cell
        [Key(5)] public bool IsReplace { get; set; }
```

- コンストラクタ `PlaceInfoMessagePack(PlaceInfo placeInfo)` の末尾に `IsReplace = placeInfo.IsReplace;` を追加。
- `PlaceInfo` クラスの `public bool Placeable { get; set; }` の直後に追加:

```csharp
        public bool IsReplace { get; set; }
```

- [ ] **Step 2: 失敗するテストを書く（搬送品の退避・復元）**

Create `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/BeltConveyorTransitCarryOverTest.cs`:

```csharp
using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    /// ベルト搬送品を進行率維持で別ベルトへ退避・復元できることを検証する
    /// Verifies belt transit items can be collected and restored into another belt with their progress preserved
    /// </summary>
    public class BeltConveyorTransitCarryOverTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 進行率を保ったまま別ベルトへ復元できる()
        {
            var source = CreateBelt(new Vector3Int(0, 0, 0));
            source.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);

            // 数tick進めて途中の進行率を作る
            // Advance a few ticks to create mid-way progress
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            var collected = BeltConveyorTransitCarryOver.Collect(source);
            Assert.AreEqual(1, collected.Count);
            Assert.Less(collected[0].RemainingRate, 1.0);
            Assert.Greater(collected[0].RemainingRate, 0.0);

            var target = CreateBelt(new Vector3Int(0, 0, 5));
            var overflow = BeltConveyorTransitCarryOver.Restore(target, collected);

            Assert.AreEqual(0, overflow.Count);
            var restored = FindItem(target, ForUnitTestItemId.ItemId1);
            Assert.IsNotNull(restored);
            Assert.AreEqual(collected[0].ItemInstanceId, restored.ItemInstanceId);
            var restoredRate = restored.RemainingTicks / (double)restored.TotalTicks;
            Assert.AreEqual(collected[0].RemainingRate, restoredRate, 1.0 / target.TicksOfItemEnterToExit + 1e-9);
        }

        [Test]
        public void 収まらない分は残りとして返る()
        {
            var target = CreateBelt(new Vector3Int(0, 0, 10));
            var slotCount = target.GetSlotSize();
            var items = new System.Collections.Generic.List<BeltTransitItem>();
            for (var i = 0; i < slotCount + 2; i++)
            {
                items.Add(new BeltTransitItem(ForUnitTestItemId.ItemId1, ItemInstanceId.Create(), 1.0));
            }

            var overflow = BeltConveyorTransitCarryOver.Restore(target, items);

            Assert.AreEqual(2, overflow.Count);
            Assert.AreEqual(slotCount, CountItems(target));
        }

        private static VanillaBeltConveyorComponent CreateBelt(Vector3Int position)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            return block.GetComponent<VanillaBeltConveyorComponent>();
        }

        private static IOnBeltConveyorItem FindItem(VanillaBeltConveyorComponent belt, ItemId itemId)
        {
            foreach (var item in belt.BeltConveyorItems)
            {
                if (item != null && item.ItemId == itemId) return item;
            }
            return null;
        }

        private static int CountItems(VanillaBeltConveyorComponent belt)
        {
            var count = 0;
            foreach (var item in belt.BeltConveyorItems)
            {
                if (item != null) count++;
            }
            return count;
        }
    }
}
```

（`GameUpdater.UpdateOneTick()` は `Tests/CombinedTest/Core/BeltConveyorTest.cs:224` が使っている進行手段。）

- [ ] **Step 3: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BeltConveyorTransitCarryOver` / `BeltTransitItem` 未定義のコンパイルエラー

- [ ] **Step 4: BeltTransitItem と TryRestoreItem と CarryOver を実装する**

Create `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/BeltTransitItem.cs`:

```csharp
using Core.Item.Interface;
using Core.Master;

namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    /// 張替えで退避した搬送品。進行率は「残りtick / 総tick」で0〜1
    /// A transit item held aside during replace; RemainingRate is remainingTicks / totalTicks in 0..1
    /// </summary>
    public readonly struct BeltTransitItem
    {
        public readonly ItemId ItemId;
        public readonly ItemInstanceId ItemInstanceId;
        public readonly double RemainingRate;

        public BeltTransitItem(ItemId itemId, ItemInstanceId itemInstanceId, double remainingRate)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            RemainingRate = remainingRate;
        }
    }
}
```

`moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/VanillaBeltConveyorComponent.cs`:
- `public IObservable<Unit> OnItemsChanged => _onItemsChanged;` の直後に追加:

```csharp
        public uint TicksOfItemEnterToExit => _ticksOfItemEnterToExit;
```

- `public void SetItem(int slot, IItemStack itemStack)` メソッドの直後に追加:

```csharp
        // 進行率を保ってアイテムを置く。理想スロットが埋まっていれば入口側→出口側の順で空きを探す
        // Place an item keeping its progress; when the ideal slot is taken, search entry-side first, then exit-side
        public bool TryRestoreItem(ItemId itemId, ItemInstanceId itemInstanceId, double remainingRate)
        {
            BlockException.CheckDestroy(this);

            var remainingTicks = (uint)Math.Min(_ticksOfItemEnterToExit, Math.Ceiling(remainingRate * _ticksOfItemEnterToExit));
            var slot = ResolveSlot(remainingTicks);
            var insertIndex = FindEmptySlotFrom(slot);
            if (insertIndex < 0) return false;

            var checkItems = new List<IItemStack> { ServerContext.ItemStackFactory.Create(itemId, 1, itemInstanceId) };
            var goalConnector = _blockInventoryInserter.GetNextGoalConnector(checkItems);
            _inventoryItems[insertIndex] = new VanillaBeltConveyorInventoryItem(itemId, itemInstanceId, null, goalConnector, _ticksOfItemEnterToExit)
            {
                RemainingTicks = remainingTicks,
            };
            NotifyItemsChanged();
            return true;

            #region Internal

            int ResolveSlot(uint ticks)
            {
                // Updateの送り条件（RemainingTicks <= i*ticksPerSlot で i-1 へ）に合わせ、残りtickから所属スロットを逆算する
                // Invert Update's advance rule (move to i-1 when RemainingTicks <= i*ticksPerSlot) to find the slot for the remaining ticks
                var ticksPerSlot = _ticksOfItemEnterToExit / (uint)_inventoryItemNum;
                if (ticksPerSlot == 0) return _inventoryItemNum - 1;
                var ideal = (int)((ticks + ticksPerSlot - 1) / ticksPerSlot);
                return Math.Min(_inventoryItemNum - 1, ideal);
            }

            int FindEmptySlotFrom(int start)
            {
                for (var i = start; i < _inventoryItemNum; i++)
                {
                    if (_inventoryItems[i] == null) return i;
                }
                for (var i = start - 1; 0 <= i; i--)
                {
                    if (_inventoryItems[i] == null) return i;
                }
                return -1;
            }

            #endregion
        }
```

（`using System;` が無ければ先頭に追加する。ファイルが200行を超えている既存違反はこのタスクでは是正しない。）

Create `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/BeltConveyorTransitCarryOver.cs`:

```csharp
using System.Collections.Generic;

namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    /// 張替え時にベルト搬送品を退避し、新ベルトへ進行率維持で復元する
    /// Collects belt transit items for replace placement and restores them into the new belt keeping progress
    /// </summary>
    public static class BeltConveyorTransitCarryOver
    {
        public static List<BeltTransitItem> Collect(IItemCollectableBeltConveyor belt)
        {
            var result = new List<BeltTransitItem>();
            foreach (var item in belt.BeltConveyorItems)
            {
                if (item == null) continue;
                result.Add(new BeltTransitItem(item.ItemId, item.ItemInstanceId, ResolveRemainingRate(item)));
            }
            return result;
        }

        // 復元できなかった分を返す。呼び出し側がプレイヤーへ返す
        // Returns the items that did not fit; the caller hands them to the player
        public static List<BeltTransitItem> Restore(VanillaBeltConveyorComponent belt, IReadOnlyList<BeltTransitItem> items)
        {
            var overflow = new List<BeltTransitItem>();
            foreach (var item in items)
            {
                if (belt.TryRestoreItem(item.ItemId, item.ItemInstanceId, item.RemainingRate)) continue;
                overflow.Add(item);
            }
            return overflow;
        }

        private static double ResolveRemainingRate(IOnBeltConveyorItem item)
        {
            // 停止中（総tickが0またはMaxValue）は入口として扱う
            // A stopped belt (total ticks 0 or MaxValue) treats the item as at the entry
            if (item.TotalTicks == 0 || item.TotalTicks == uint.MaxValue) return 1.0;
            return item.RemainingTicks / (double)item.TotalTicks;
        }
    }
}
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "BeltConveyorTransitCarryOverTest"`
Expected: 2件PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/BlockRemoveReason.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlacePacketDto.cs moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/BeltConveyorTransitCarryOverTest.cs*
git commit -m "feat(server): 張替えの基盤（RemoveReason.Replace・PlaceInfo.IsReplace・搬送品の退避復元）"
```

---

### Task 3: BeltReplacePlacementService と PlaceBlockProtocol の張替え分岐

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/BeltReplacePlacementService.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs:29-42, 76-80`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTestSupport.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltReplacePlaceTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltReplacePlaceEdgeTest.cs`

**Interfaces:**
- Consumes: `ConstructionWalletService.PlanRemoval(BlockMasterElement, BlockInstanceId, int)` / `PlanPlacement(BlockMasterElement, int)` / `CommitRemoval(IConstructionRemovalPlan)` / `CommitPlacement(IConstructionPlacementPlan, IOpenableInventory, BlockInstanceId)`、`IConstructionRemovalPlan.ItemsToRefund : IReadOnlyList<IItemStack>`、`IConstructionPlacementPlan.ItemsToConsume : IReadOnlyList<(ItemId, int)>`、`ConstructionCostService.HasRequiredItems(...)`、`PlacementTargetCatalog.IsBlockUnlocked(Guid, IGameUnlockStateData, bool)`、`IOpenableInventory.InsertionCheck(List<IItemStack>)` / `InsertItem(List<IItemStack>)`、Task 2 の `BeltConveyorTransitCarryOver`
- Produces: `BeltReplacePlacementService(ConstructionWalletService wallet, PlacementTargetCatalog catalog, IGameUnlockStateDataController unlockState)`、`void Replace(PlaceInfoMessagePack placeInfo, IOpenableInventory inventory, int playerId, bool isFreePlacement)`

- [ ] **Step 1: テスト支援ヘルパを足す**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTestSupport.cs` の `CreatePlacePayload` の直後に追加:

```csharp
        public static byte[] CreateReplacePayload(BlockId blockId, Vector3Int position, BlockDirection direction)
        {
            // 張替えフラグ付きの単一セルペイロードを生成する
            // Build a single-cell payload flagged for replace placement
            var placeInfos = new List<PlaceInfo>
            {
                new()
                {
                    Position = position,
                    Direction = direction,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = blockId,
                    IsReplace = true,
                },
            };
            return CreatePlacePayload(placeInfos);
        }

        public static void AssertRequiredItemsCount(ServiceProvider serviceProvider, BlockId blockId, int costSets)
        {
            // ブロックの必要素材が指定セット分だけインベントリに存在することを検証する
            // Assert the block's required items are present for the given number of cost sets
            var inventory = GetInventory(serviceProvider);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            foreach (var requiredItem in blockMaster.RequiredItems)
            {
                Assert.AreEqual(requiredItem.Count * costSets, GetItemCount(inventory, requiredItem.ItemGuid));
            }
        }

        public static void OccupyAllInventorySlots(ServiceProvider serviceProvider, ItemId fillerItemId)
        {
            // 全スロットをコスト外アイテムで埋め、返却挿入の空きを無くす
            // Occupy every slot with a non-cost item so no room remains for refund insertion
            var inventory = GetInventory(serviceProvider);
            for (var i = 0; i < inventory.GetSlotSize(); i++)
            {
                inventory.SetItem(i, ServerContext.ItemStackFactory.Create(fillerItemId, 1));
            }
        }
```

（`using UnityEngine;`・`using Game.Block.Interface;` が無ければ追加。）

- [ ] **Step 2: 失敗するテストを書く（正常系）**

Create `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltReplacePlaceTest.cs`:

```csharp
using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using Server.Protocol;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// 張替え設置の正常系（ティア差し替え・向き維持・搬送品進行率維持・コスト精算・分岐器ロール）を検証する
    /// Verifies replace placement happy paths (tier swap, direction kept, transit progress kept, cost settlement, splitter role)
    /// </summary>
    public class BeltReplacePlaceTest
    {
        [Test]
        public void 別ファミリーへ向きと搬送品を保って差し替わる()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(50, 0, 50);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyor);

            // 旧は無動力で進む TestBeltConveyor（歯車ベルトは停止中で進行率が常に1.0になり検証にならない）
            // The old block is the unpowered TestBeltConveyor (a stopped gear belt would always report rate 1.0)
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, pos, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var oldBlock);
            var oldBelt = oldBlock.GetComponent<VanillaBeltConveyorComponent>();
            oldBelt.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1), InsertItemContext.Empty);
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
            var beforeRate = BeltConveyorTransitCarryOver.Collect(oldBelt)[0].RemainingRate;
            Assert.Less(beforeRate, 1.0);

            // 手持ちの向きは無視され既設の向きが維持される
            // The held direction is ignored; the existing direction is kept
            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyor, block.BlockId);
            Assert.AreEqual(BlockDirection.East, block.BlockPositionInfo.BlockDirection);
            var newBelt = block.GetComponent<VanillaBeltConveyorComponent>();
            var after = BeltConveyorTransitCarryOver.Collect(newBelt);
            Assert.AreEqual(1, after.Count);
            Assert.AreEqual(ForUnitTestItemId.ItemId2, after[0].ItemId);
            Assert.AreEqual(beforeRate, after[0].RemainingRate, 1.0 / newBelt.TicksOfItemEnterToExit + 1e-9);
        }

        [Test]
        public void 無料ファミリーから有料ファミリーへの差し替えで新コストが消費される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(52, 0, 52);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
            AssertInventoryEmptyOfRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
        }

        [Test]
        public void 分岐器は手持ちファミリーの分岐器へ差し替わる()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(54, 0, 54);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyorSplitter);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyorSplitter, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 手持ちは直線でもロールは既設（分岐器）に従う
            // Even if the held block is a straight, the role follows the existing splitter
            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyorSplitter, pos, BlockDirection.North), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyorSplitter, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
        }

        [Test]
        public void 同ファミリー同ロールはno_opで素材もブロックも変わらない()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(56, 0, 56);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);

            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oldBlock);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.East), new PacketResponseContext(null));

            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.AreEqual(oldBlock.BlockInstanceId, block.BlockInstanceId);
            AssertRequiredItemsCount(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);
        }
    }
}
```

- [ ] **Step 3: 失敗するテストを書く（異常系）**

Create `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltReplacePlaceEdgeTest.cs`:

```csharp
using System;
using System.Collections.Generic;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// 張替え設置の拒否系（ファミリー外・ロール不一致・未解放・コスト不足・満杯・通常設置不変）を検証する
    /// Verifies replace placement rejections (non-family, role mismatch, locked, cost shortage, full inventory, normal placement unchanged)
    /// </summary>
    public class BeltReplacePlaceEdgeTest
    {
        [Test]
        public void ファミリー外の既設ブロックは張り替えられない()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(60, 0, 60);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyor);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.MachineId, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
        }

        [Test]
        public void ロールが一致しない手持ちは拒否される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(62, 0, 62);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyorSplitter);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 既設は直線、手持ちは分岐器（改造クライアント想定）
            // Existing is a straight, held is a splitter (modded client)
            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyorSplitter, pos, BlockDirection.North), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
        }

        [Test]
        public void 未解放の手持ちは拒否される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(64, 0, 64);
            LockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyor);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
        }

        [Test]
        public void 新コスト不足のセルは失敗し既設が残る()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(66, 0, 66);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
        }

        [Test]
        public void インベントリ満杯時は旧ブロックと搬送品が保持される()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(68, 0, 68);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.BeltConveyorId);

            // 有料ファミリー（財布残0）を撤去すると財布+1のみで返却品は無いが、搬送品の最悪ケース返却先が無いので失敗する
            // Removing the paid family at wallet 0 refunds no items, but the worst-case transit return has no room, so the cell fails
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oldBlock);
            var oldBelt = oldBlock.GetComponent<VanillaBeltConveyorComponent>();
            oldBelt.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1), InsertItemContext.Empty);
            OccupyAllInventorySlots(serviceProvider, ForUnitTestItemId.ItemId1);

            packet.GetPacketResponse(CreateReplacePayload(ForUnitTestModBlockId.BeltConveyorId, pos, BlockDirection.North), new PacketResponseContext(null));

            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            Assert.AreEqual(oldBlock.BlockInstanceId, block.BlockInstanceId);
            Assert.AreEqual(1, BeltConveyorTransitCarryOver.Collect(oldBelt).Count);
        }

        [Test]
        public void IsReplace無しの通常設置は既設をスキップして挙動が変わらない()
        {
            var (packet, serviceProvider) = CreateServer();
            var pos = new Vector3Int(70, 0, 70);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var normalPlace = new List<PlaceInfo>
            {
                new()
                {
                    Position = pos,
                    Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = ForUnitTestModBlockId.GearBeltConveyor,
                    IsReplace = false,
                },
            };
            packet.GetPacketResponse(CreatePlacePayload(normalPlace), new PacketResponseContext(null));

            Assert.AreEqual(ForUnitTestModBlockId.BeltConveyorId, ServerContext.WorldBlockDatastore.GetBlock(pos).BlockId);
            AssertRequiredItemsCount(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 1);
        }
    }
}
```

- [ ] **Step 4: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: テストはコンパイルが通り（新規型は使っていない）、実行すると `別ファミリーへ向きと搬送品を保って差し替わる` 等が FAIL（サーバーが `Exists` でスキップするため既設のまま）。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltReplacePlace"`
Expected: 正常系4件FAIL、異常系はPASS（現状は何も起きないため）

- [ ] **Step 5: BeltReplacePlacementService を実装する**

Create `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/BeltReplacePlacementService.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlacementTarget;
using Game.UnlockState;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 1セルの張替え。既設と手持ちが同ロールのベルトファミリーであることを検証し、旧返却→撤去→設置→新消費→搬送品復元を一体で行う
    /// One-cell replace: verifies both blocks are same-role belt family members, then refunds old, removes, places, consumes new and restores transit items as one unit
    /// </summary>
    public class BeltReplacePlacementService
    {
        private readonly ConstructionWalletService _constructionWallet;
        private readonly PlacementTargetCatalog _placementTargetCatalog;
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;

        public BeltReplacePlacementService(ConstructionWalletService constructionWallet, PlacementTargetCatalog placementTargetCatalog, IGameUnlockStateDataController gameUnlockStateDataController)
        {
            _constructionWallet = constructionWallet;
            _placementTargetCatalog = placementTargetCatalog;
            _gameUnlockStateDataController = gameUnlockStateDataController;
        }

        public void Replace(PlaceInfoMessagePack placeInfo, IOpenableInventory inventory, int playerId, bool isFreePlacement)
        {
            var pos = placeInfo.Position;
            var newBlockId = placeInfo.BlockId;

            // 既設・手持ちがともにファミリー所属で同ロールであること。違えば理由をログして何もしない
            // Both existing and held must be family members of the same role; otherwise log the reason and do nothing
            var oldBlock = ServerContext.WorldBlockDatastore.GetBlock(pos);
            if (oldBlock == null) { Reject("no block at cell"); return; }
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(oldBlock.BlockId, out var oldFamily) || !oldFamily.TryGetRole(oldBlock.BlockId, out var oldRole)) { Reject("existing block is not a belt family member"); return; }
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(newBlockId, out var newFamily) || !newFamily.TryGetRole(newBlockId, out var newRole)) { Reject("held block is not a belt family member"); return; }
            if (oldRole != newRole) { Reject($"role mismatch existing:{oldRole} held:{newRole}"); return; }
            if (oldBlock.BlockId == newBlockId) return;

            var newBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(newBlockId);
            if (!_placementTargetCatalog.IsBlockUnlocked(newBlockMaster.BlockGuid, _gameUnlockStateDataController, false)) { Reject("held block is locked"); return; }

            // 撤去返却と設置消費の指示を財布から受け取り、返却先の空きと新コストの充足を事前検証する
            // Obtain refund and consume instructions from the wallet, then pre-validate refund room and new-cost coverage
            var oldBelt = oldBlock.GetComponent<VanillaBeltConveyorComponent>();
            var transit = BeltConveyorTransitCarryOver.Collect(oldBelt);
            var removalPlan = _constructionWallet.PlanRemoval(MasterHolder.BlockMaster.GetBlockMaster(oldBlock.BlockId), oldBlock.BlockInstanceId, playerId);
            var placementPlan = _constructionWallet.PlanPlacement(newBlockMaster, playerId);
            if (!isFreePlacement && !CanRefund(removalPlan.ItemsToRefund, transit)) { Reject("no room for refund or transit items"); return; }
            if (!isFreePlacement && !CanPayNewCost(placementPlan.ItemsToConsume, removalPlan.ItemsToRefund)) { Reject("new construction cost is short"); return; }

            // 旧ブロックを撤去し返却、同セルへ新ブロックを設置して消費
            // Remove and refund the old block, then place the new block on the same cell and consume
            var direction = oldBlock.BlockPositionInfo.BlockDirection;
            var createParams = placeInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();
            ServerContext.WorldBlockDatastore.RemoveBlock(pos, BlockRemoveReason.Replace);
            if (!isFreePlacement)
            {
                _constructionWallet.CommitRemoval(removalPlan);
                if (removalPlan.ItemsToRefund.Count != 0) inventory.InsertItem(removalPlan.ItemsToRefund.ToList());
            }

            if (!ServerContext.WorldBlockDatastore.TryAddBlock(newBlockId, pos, direction, createParams, out var newBlock))
            {
                Reject("TryAddBlock failed after removal; transit items returned to the player");
                inventory.InsertItem(ToItemStacks(transit));
                return;
            }
            if (!isFreePlacement) _constructionWallet.CommitPlacement(placementPlan, inventory, newBlock.BlockInstanceId);

            // 搬送品を進行率維持で復元し、入らない分はプレイヤーへ
            // Restore transit items keeping progress; whatever does not fit goes to the player
            var overflow = BeltConveyorTransitCarryOver.Restore(newBlock.GetComponent<VanillaBeltConveyorComponent>(), transit);
            if (overflow.Count != 0) inventory.InsertItem(ToItemStacks(overflow));

            #region Internal

            void Reject(string reason)
            {
                Debug.Log($"[BeltReplace] rejected at {pos} held:{newBlockId} reason:{reason}");
            }

            bool CanRefund(IReadOnlyList<IItemStack> refund, List<BeltTransitItem> transitItems)
            {
                // 最悪ケース（返却品＋搬送品全部がプレイヤー行き）で溢れないこと
                // The worst case (refund plus every transit item going to the player) must not overflow
                var worstCase = new List<IItemStack>(refund);
                worstCase.AddRange(ToItemStacks(transitItems));
                return worstCase.Count == 0 || inventory.InsertionCheck(worstCase);
            }

            bool CanPayNewCost(IReadOnlyList<(ItemId itemId, int count)> cost, IReadOnlyList<IItemStack> refund)
            {
                // 新コストは「現在のインベントリ＋旧返却品」で賄える必要がある
                // The new cost must be covered by the current inventory plus the old refund
                var available = new List<IItemStack>(inventory.InventoryItems);
                available.AddRange(refund);
                return ConstructionCostService.HasRequiredItems(cost, available);
            }

            List<IItemStack> ToItemStacks(List<BeltTransitItem> transitItems)
            {
                return transitItems.Select(t => ServerContext.ItemStackFactory.Create(t.ItemId, 1, t.ItemInstanceId)).ToList();
            }

            #endregion
        }
    }
}
```

（`GetComponent<T>()` は `IBlock` の既存拡張。`ServerContext.ItemStackFactory.Create(ItemId, int, ItemInstanceId)` オーバーロードは `VanillaBeltConveyorComponent.Update` が使用済み。`inventory.InsertItem(List<IItemStack>)` は `RemoveBlockProtocol.InsertItemsToPlayerInventory` が使う形。名前が違えばそちらへ合わせる。）

- [ ] **Step 6: PlaceBlockProtocol に分岐を入れる**

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`:
- フィールドに `private readonly BeltReplacePlacementService _beltReplacePlacementService;` を追加し、コンストラクタ末尾に `_beltReplacePlacementService = new BeltReplacePlacementService(_constructionWallet, _placementTargetCatalog, _gameUnlockStateDataController);` を追加。
- ローカル関数 `PlaceBlock(PlaceInfoMessagePack placeInfo)` の先頭（`Exists` 判定の前）に追加:

```csharp
                // 張替えセルは張替えサービスへ委譲する。通常セルは既存ブロックがあれば何もしない
                // Delegate replace cells to the replace service; normal cells do nothing when a block already exists
                if (placeInfo.IsReplace)
                {
                    _beltReplacePlacementService.Replace(placeInfo, inventoryData.MainOpenableInventory, data.PlayerId, isFreePlacement);
                    return;
                }
```

（`inventoryData` / `isFreePlacement` / `data` は同メソッド内で既に定義済みの変数名。行番号 L76-80 付近を確認して合わせる。）

- [ ] **Step 7: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltReplacePlace|PlaceBlockProtocol|RemoveBlockRemainingPlacementTest|ConstructionPayerWalletTest"`
Expected: 全PASS

- [ ] **Step 8: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest
git commit -m "feat(server): PlaceBlockProtocolに張替えセルを追加しBeltReplacePlacementServiceで一体処理"
```

---

### Task 4: クライアント基盤（HoldingBlockのロール・セル解決の署名・不可理由・ローカライズ）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorHoldingBlock.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorStraightCellBlockResolver.cs:18-39`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlaceRunBuilder.cs:45`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlacementBlockReason.cs`
- Modify: `Localization/localization.csv:227`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorStraightCellBlockResolverTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorHoldingBlockTest.cs`

**Interfaces:**
- Consumes: Task 1 の `BeltConveyorRole` / `BeltConveyorFamily.TryGetRole`
- Produces:
  - `BeltConveyorHoldingBlock.Role : BeltConveyorRole`、`RunUpBlockId : BlockId?`、`RunDownBlockId : BlockId?`（分岐器手持ちでは両方 null）。`BlockId` は 直線ロールなら `family.StraightBlockId`、それ以外は選択ブロック自身
  - `static List<PlaceInfo> BeltConveyorStraightCellBlockResolver.ResolveStraightRun(IReadOnlyList<PlaceInfo> cells, BlockId horizontalBlockId, BlockId? upBlockId, BlockId? downBlockId, IList<BeltConveyorPlacementBlockReason> beltReasons)`
  - `BeltConveyorPlacementBlockReason.ReplaceRoleMissing`、`LocalizationKeys.Ui.Tooltip.PlaceBeltReplaceRoleMissing`

- [ ] **Step 1: 失敗するテストを書く（HoldingBlock）**

Create `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorHoldingBlockTest.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    public class BeltConveyorHoldingBlockTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 分岐器を選ぶと手持ちは分岐器のままで坂の自動挿入は無効()
        {
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyorSplitter);

            Assert.AreEqual(BeltConveyorRole.Splitter, holding.Role);
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyorSplitter, holding.BlockId);
            Assert.IsNull(holding.SlopeGrade);
            Assert.IsNull(holding.RunUpBlockId);
            Assert.IsNull(holding.RunDownBlockId);
        }

        [Test]
        public void 直線を選ぶと坂の自動挿入が有効()
        {
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyor);

            Assert.AreEqual(BeltConveyorRole.Straight, holding.Role);
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp, holding.RunUpBlockId);
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorDown, holding.RunDownBlockId);
        }

        [Test]
        public void 坂を選ぶと勾配が立ち手持ちは坂自身()
        {
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.TestGearBeltConveyorUp);

            Assert.AreEqual(BeltSlopeGrade.Up, holding.SlopeGrade);
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp, holding.BlockId);
        }
    }
}
```

- [ ] **Step 2: BeltConveyorHoldingBlock を書き換える**

Replace `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorHoldingBlock.cs` 全文:

```csharp
using System;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 選択ブロックからファミリー・ロール・手持ちブロック・坂勾配を解決する
    /// Resolves the family, role, held block and slope grade from the selected block
    /// </summary>
    public class BeltConveyorHoldingBlock
    {
        public readonly BeltConveyorFamily Family;
        public readonly BeltConveyorRole Role;
        public readonly BlockId BlockId;
        public readonly BlockMasterElement BlockMaster;

        // null は直線選択（高低差からの自動坂判定）。分岐器選択も null だが RunUp/RunDown が null なので坂は入らない
        // Null means a straight selection (auto slopes from height); splitter selection is also null but RunUp/RunDown are null so no slope is inserted
        public readonly BeltSlopeGrade? SlopeGrade;

        // 直線ドラッグで坂セルに割り当てるブロック。分岐器手持ちでは坂を持たない
        // Blocks assigned to slope cells in a straight drag; a held splitter has no slopes
        public readonly BlockId? RunUpBlockId;
        public readonly BlockId? RunDownBlockId;

        private BeltConveyorHoldingBlock(BeltConveyorFamily family, BeltConveyorRole role, BlockId blockId, BlockMasterElement blockMaster, BeltSlopeGrade? slopeGrade, BlockId? runUpBlockId, BlockId? runDownBlockId)
        {
            Family = family;
            Role = role;
            BlockId = blockId;
            BlockMaster = blockMaster;
            SlopeGrade = slopeGrade;
            RunUpBlockId = runUpBlockId;
            RunDownBlockId = runDownBlockId;
        }

        public static BeltConveyorHoldingBlock Resolve(BlockId selectedBlockId)
        {
            // ベルト設置系はファミリー所属が前提。無所属はマスタ不整合なので例外で止める
            // Belt placement assumes family membership; a non-member is a master inconsistency, so stop with an exception
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(selectedBlockId, out var family) || !family.TryGetRole(selectedBlockId, out var role))
                throw new InvalidOperationException($"BeltConveyorHoldingBlock: block belongs to no beltConveyorFamily. BlockId:{selectedBlockId}");

            var holdingBlockId = role == BeltConveyorRole.Straight ? family.StraightBlockId : selectedBlockId;
            var slopeGrade = ResolveSlopeGrade();
            var isSplitter = role == BeltConveyorRole.Splitter;
            return new BeltConveyorHoldingBlock(family, role, holdingBlockId, MasterHolder.BlockMaster.GetBlockMaster(holdingBlockId), slopeGrade, isSplitter ? null : family.UpBlockId, isSplitter ? null : family.DownBlockId);

            #region Internal

            BeltSlopeGrade? ResolveSlopeGrade()
            {
                if (role == BeltConveyorRole.Up) return BeltSlopeGrade.Up;
                if (role == BeltConveyorRole.Down) return BeltSlopeGrade.Down;
                return null;
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: セル解決の署名を BlockId 直指定へ変え、呼び出しとテストを追従させる**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorStraightCellBlockResolver.cs`:
- 署名を `public static List<PlaceInfo> ResolveStraightRun(IReadOnlyList<PlaceInfo> cells, BlockId horizontalBlockId, BlockId? upBlockId, BlockId? downBlockId, IList<BeltConveyorPlacementBlockReason> beltReasons)` に変更
- `ResolveCell` 内の `var blockId = family.StraightBlockId;` を `var blockId = horizontalBlockId;` に、`family.UpBlockId` を `upBlockId`、`family.DownBlockId` を `downBlockId` に変更
- `using Game.Block.Interface.Extension;` を削除（未使用になる）

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlaceRunBuilder.cs` L45 を次に変更:

```csharp
            return BeltConveyorStraightCellBlockResolver.ResolveStraightRun(cellInfos, holdingBlock.BlockId, holdingBlock.RunUpBlockId, holdingBlock.RunDownBlockId, beltReasons);
```

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorStraightCellBlockResolverTest.cs`:
- `Family` / `SlopelessFamily` の static フィールド2行を削除
- `ResolveStraightRun(cells, Family, ...)` の呼び出しを全て `ResolveStraightRun(cells, StraightBlock, UpBlock, DownBlock, ...)` に、`ResolveStraightRun(cells, SlopelessFamily, ...)` を `ResolveStraightRun(cells, StraightBlock, null, null, ...)` に置換
- `using Game.Block.Interface.Extension;` を削除

- [ ] **Step 4: 不可理由とローカライズを足す**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlacementBlockReason.cs`:
- enum に `SlopeBlockMissing,` の後ろへ追加:

```csharp
        // 張替え: 手持ちファミリーに既設と同じロール（分岐器等）が無い
        // Replace: the held family lacks the existing block's role (splitter etc.)
        ReplaceRoleMissing,
```

- `ToKey` の switch に追加:

```csharp
                BeltConveyorPlacementBlockReason.ReplaceRoleMissing => LocalizationKeys.Ui.Tooltip.PlaceBeltReplaceRoleMissing,
```

`Localization/localization.csv` の L227（`ui.tooltip.placeBeltNoSlopeBlock,...`）の直後に1行追加:

```
ui.tooltip.placeBeltReplaceRoleMissing,No matching conveyor in the held family,No matching conveyor in the held family,手持ちのファミリーに対応するコンベアがありません,Kein passendes Förderband in der gewählten Baureihe
```

（`LocalizationKeys` は mooresmaster SourceGenerator が CSV から生成する。生成が走らない場合は `_CompileRequester.cs` の `dummyText` を `"belt-family-splitter-role-2"` に変えて再コンパイル。）

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorPlacementBlockReasonTooltipKeyTest.cs` に既存の理由→キーのテストがあれば、同じ形で `ReplaceRoleMissing → PlaceBeltReplaceRoleMissing` の1ケースを追加する（ファイルを開き、`SlopeBlockMissing` のケースを複製して書く）。

- [ ] **Step 5: コンパイルしてテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorHoldingBlockTest|BeltConveyorStraightCellBlockResolverTest|BeltConveyorPlacementBlockReasonTooltipKeyTest|BeltConveyorSlopePlacementTest|BeltConveyorPlaceRunAxisTest|ConveyorOverpassConveyanceTest"`
Expected: 全PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor Localization/localization.csv
git commit -m "feat(client): 手持ちベルトのロール解決と張替え不可理由を追加"
```

---

### Task 5: BeltReplaceRunBuilder（既設ライン追従の張替え経路）と RunBuilder の分岐

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Replace/BeltReplaceRunBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlaceRunBuilder.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltReplaceRunBuilderTest.cs`

**Interfaces:**
- Consumes: `BlockGameObjectDataStore.TryGetBlockGameObject(Vector3Int, out BlockGameObject)`、`BlockGameObject.BlockId` / `BlockPosInfo.OriginalPos` / `BlockPosInfo.BlockDirection`、`BeltConveyorPositionListBuilder.BuildHorizontalPositions(Vector3Int, Vector3Int, bool)`、Task 4 の `BeltConveyorHoldingBlock.Family`
- Produces:
  - `static bool BeltReplaceRunBuilder.TryResolveOrigin(BlockGameObjectDataStore store, Vector3Int cell, out Vector3Int originCell)`
  - `BeltReplaceRunBuilder(BlockGameObjectDataStore store)`、`List<PlaceInfo> Build(Vector3Int originCell, Vector3Int cursorCell, bool isStartDirectionZ, BeltConveyorHoldingBlock holdingBlock, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons)`
  - `BeltConveyorPlaceRunBuilder.Build` は起点が張替え起点なら張替え経路を返す（署名不変）

- [ ] **Step 1: 失敗するテストを書く**

Create `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltReplaceRunBuilderTest.cs`:

```csharp
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// 既設ライン追従の張替え経路（ロール対応・高さ追従・空セル飛ばし・no-op除外・起点解決）を検証する
    /// Verifies the existing-line-following replace run (role mapping, height follow, skipping empty cells, no-op exclusion, origin resolution)
    /// </summary>
    public class BeltReplaceRunBuilderTest
    {
        private GameObject _dataStoreObject;
        private BlockGameObjectDataStore _dataStore;
        private readonly List<GameObject> _blockObjects = new();

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _dataStoreObject = new GameObject("BlockGameObjectDataStore");
            _dataStore = _dataStoreObject.AddComponent<BlockGameObjectDataStore>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var blockObject in _blockObjects) Object.DestroyImmediate(blockObject);
            _blockObjects.Clear();
            Object.DestroyImmediate(_dataStoreObject);
        }

        [Test]
        public void 既設のロールと向きを保って手持ちファミリーの同ロールへ写し高さも追従する()
        {
            // 歯車ライン: 直線(y0) → 上り(y0) → 直線(y1) → 分岐器(y1)。手持ちは分岐器を持つが坂を持たない SmallGear
            // Gear line: straight(y0) -> up(y0) -> straight(y1) -> splitter(y1); held SmallGear has a splitter but no slopes
            Register(new Vector3Int(0, 0, 0), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            Register(new Vector3Int(0, 0, 1), ForUnitTestModBlockId.TestGearBeltConveyorUp, BlockDirection.North);
            Register(new Vector3Int(0, 1, 2), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.East);
            Register(new Vector3Int(0, 1, 3), ForUnitTestModBlockId.GearBeltConveyorSplitter, BlockDirection.North);
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.SmallGearBeltConveyor);

            var result = new BeltReplaceRunBuilder(_dataStore).Build(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 3), true, holding, out _, out var beltReasons);

            Assert.AreEqual(4, result.Count);
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyor, result[0].BlockId);
            Assert.IsTrue(result[0].Placeable && result[0].IsReplace);
            Assert.IsFalse(result[1].Placeable);
            Assert.AreEqual(BeltConveyorPlacementBlockReason.ReplaceRoleMissing, beltReasons[1]);
            Assert.AreEqual(new Vector3Int(0, 1, 2), result[2].Position);
            Assert.AreEqual(BlockDirection.East, result[2].Direction);
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyor, result[2].BlockId);
            Assert.AreEqual(ForUnitTestModBlockId.SmallGearBeltConveyorSplitter, result[3].BlockId);
            Assert.AreEqual(new Vector3Int(0, 1, 3), result[3].Position);
        }

        [Test]
        public void 既設の無いセルは飛ばして続行し同ファミリー同ロールは出さない()
        {
            Register(new Vector3Int(0, 0, 0), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            Register(new Vector3Int(0, 0, 2), ForUnitTestModBlockId.SmallGearBeltConveyor, BlockDirection.North);
            Register(new Vector3Int(0, 0, 3), ForUnitTestModBlockId.MachineId, BlockDirection.North);
            Register(new Vector3Int(0, 0, 4), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.South);
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.SmallGearBeltConveyor);

            var result = new BeltReplaceRunBuilder(_dataStore).Build(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 4), true, holding, out _, out _);

            // z=1 は空、z=2 は同ティア no-op、z=3 は機械、z=0/4 だけ張替え
            // z=1 is empty, z=2 is a same-tier no-op, z=3 is a machine; only z=0 and z=4 are replaced
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 0), result[0].Position);
            Assert.AreEqual(new Vector3Int(0, 0, 4), result[1].Position);
            Assert.AreEqual(BlockDirection.South, result[1].Direction);
        }

        [Test]
        public void 天面ヒットで1段浮いた起点は直下の既設ベルトへ解決する()
        {
            Register(new Vector3Int(3, 0, 3), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);

            Assert.IsTrue(BeltReplaceRunBuilder.TryResolveOrigin(_dataStore, new Vector3Int(3, 1, 3), out var origin));
            Assert.AreEqual(new Vector3Int(3, 0, 3), origin);
            Assert.IsFalse(BeltReplaceRunBuilder.TryResolveOrigin(_dataStore, new Vector3Int(3, 0, 4), out _));
        }

        [Test]
        public void 空き地起点の通常経路は既設ベルトを張り替えない()
        {
            Register(new Vector3Int(0, 0, 2), ForUnitTestModBlockId.GearBeltConveyor, BlockDirection.North);
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.SmallGearBeltConveyor);
            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, new Client.Game.InGame.BlockSystem.PlaceSystem.Common.CommonBlockPlaceDragState());

            var result = runBuilder.Build(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 3), BlockDirection.North, holding, out _, out _);

            // 既設セルは立体交差で跨がれるか不可になるかのどちらかで、張替えにはならない
            // The existing cell is either overpassed or blocked, never replaced
            Assert.AreEqual(4, result.Count);
            Assert.IsTrue(result.TrueForAll(info => !info.IsReplace));
            Assert.IsFalse(result.Exists(info => info.Position == new Vector3Int(0, 0, 2)));
        }

        private void Register(Vector3Int position, BlockId blockId, BlockDirection direction)
        {
            var blockObject = new GameObject($"Block_{position}");
            _blockObjects.Add(blockObject);
            var blockGameObject = blockObject.AddComponent<BlockGameObject>();
            SetBackingField(blockGameObject, nameof(BlockGameObject.BlockPosInfo), new BlockPositionInfo(position, direction, Vector3Int.one));
            SetBackingField(blockGameObject, nameof(BlockGameObject.BlockId), blockId);

            var dictionary = (Dictionary<Vector3Int, BlockGameObject>)typeof(BlockGameObjectDataStore)
                .GetField("_blockObjectsDictionary", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_dataStore);
            dictionary.Add(position, blockGameObject);
        }

        private static void SetBackingField(BlockGameObject blockGameObject, string propertyName, object value)
        {
            typeof(BlockGameObject)
                .GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(blockGameObject, value);
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BeltReplaceRunBuilder` 未定義のコンパイルエラー

- [ ] **Step 3: BeltReplaceRunBuilder を実装する**

Create `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Replace/BeltReplaceRunBuilder.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Path;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace
{
    /// <summary>
    /// 既設ラインを追従する張替え経路。XZはドラッグ経路、Yは直前の既設ベルト±1で探し、既設のロール・向きを保ってBlockIdだけ手持ちファミリーの同ロールへ写す
    /// Replace run following the existing line: XZ from the drag path, Y searched at the previous belt's Y±1, keeping role and direction and mapping only the BlockId to the held family's same role
    /// </summary>
    public class BeltReplaceRunBuilder
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        public BeltReplaceRunBuilder(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        // 起点セルに既設ファミリーブロックがあれば張替え起点。天面ヒットで1段浮いた座標は直下も見る
        // The cell is a replace origin when it (or the cell below, for a top-face hit floated one step up) holds a family block
        public static bool TryResolveOrigin(BlockGameObjectDataStore store, Vector3Int cell, out Vector3Int originCell)
        {
            if (TryGetFamilyBlock(store, cell, out _, out _)) { originCell = cell; return true; }
            var below = cell + Vector3Int.down;
            if (TryGetFamilyBlock(store, below, out _, out _)) { originCell = below; return true; }
            originCell = cell;
            return false;
        }

        public List<PlaceInfo> Build(Vector3Int originCell, Vector3Int cursorCell, bool isStartDirectionZ, BeltConveyorHoldingBlock holdingBlock, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons)
        {
            var result = new List<PlaceInfo>();
            blockCauses = new List<PlacementBlockCause>();
            beltReasons = new List<BeltConveyorPlacementBlockReason>();

            // XZ経路はカーソルのYを無視して起点の高さで作り、Yは既設に追従させる
            // Build the XZ path at the origin's height ignoring the cursor's Y, then follow the existing blocks' Y
            var flatCursor = new Vector3Int(cursorCell.x, originCell.y, cursorCell.z);
            var (xzCells, _) = BeltConveyorPositionListBuilder.BuildHorizontalPositions(originCell, flatCursor, isStartDirectionZ);
            var currentY = originCell.y;
            foreach (var xz in xzCells)
            {
                if (!TryFindFamilyBlockNear(xz.x, xz.z, currentY, out var existing, out var existingFamily)) continue;
                currentY = existing.BlockPosInfo.OriginalPos.y;
                AppendCell(existing, existingFamily);
            }

            return result;

            #region Internal

            void AppendCell(BlockGameObject existing, BeltConveyorFamily existingFamily)
            {
                existingFamily.TryGetRole(existing.BlockId, out var role);
                var info = new PlaceInfo
                {
                    Position = existing.BlockPosInfo.OriginalPos,
                    Direction = existing.BlockPosInfo.BlockDirection,
                    VerticalDirection = ToVerticalDirection(role),
                    IsReplace = true,
                };

                // 手持ちファミリーに同ロールが無ければ不可色で止め、同ブロックならno-opとして出さない
                // Stop with the unplaceable color when the held family lacks the role; omit same-block cells as no-ops
                if (!holdingBlock.Family.TryGetBlockIdOfRole(role, out var targetBlockId))
                {
                    info.BlockId = existing.BlockId;
                    info.Placeable = false;
                    result.Add(info);
                    blockCauses.Add(PlacementBlockCause.None);
                    beltReasons.Add(BeltConveyorPlacementBlockReason.ReplaceRoleMissing);
                    return;
                }
                if (targetBlockId == existing.BlockId) return;

                info.BlockId = targetBlockId;
                info.Placeable = true;
                result.Add(info);
                blockCauses.Add(PlacementBlockCause.None);
                beltReasons.Add(BeltConveyorPlacementBlockReason.None);
            }

            bool TryFindFamilyBlockNear(int x, int z, int y, out BlockGameObject existing, out BeltConveyorFamily family)
            {
                // ベルトの坂は毎セル±1なので、直前の高さ・1つ上・1つ下の順に探す
                // Belt slopes change one step per cell, so search the previous height, one above, then one below
                if (TryGetFamilyBlock(_blockGameObjectDataStore, new Vector3Int(x, y, z), out existing, out family)) return true;
                if (TryGetFamilyBlock(_blockGameObjectDataStore, new Vector3Int(x, y + 1, z), out existing, out family)) return true;
                return TryGetFamilyBlock(_blockGameObjectDataStore, new Vector3Int(x, y - 1, z), out existing, out family);
            }

            static BlockVerticalDirection ToVerticalDirection(BeltConveyorRole role)
            {
                return role switch
                {
                    BeltConveyorRole.Up => BlockVerticalDirection.Up,
                    BeltConveyorRole.Down => BlockVerticalDirection.Down,
                    _ => BlockVerticalDirection.Horizontal,
                };
            }

            #endregion
        }

        private static bool TryGetFamilyBlock(BlockGameObjectDataStore store, Vector3Int cell, out BlockGameObject existing, out BeltConveyorFamily family)
        {
            family = null;
            if (!store.TryGetBlockGameObject(cell, out existing)) return false;
            return BeltConveyorPlaceFamilyUtil.TryGetFamily(existing.BlockId, out family);
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlaceRunBuilder.cs`:
- `using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace;` を追加
- フィールド `private readonly BlockGameObjectDataStore _blockGameObjectDataStore;` と `private readonly BeltReplaceRunBuilder _replaceRunBuilder;` を追加し、コンストラクタで `_blockGameObjectDataStore = blockGameObjectDataStore; _replaceRunBuilder = new BeltReplaceRunBuilder(blockGameObjectDataStore);` を代入
- `Build` の `var isStartDirectionZ = ...;` の直後に追加:

```csharp
            // 起点に既設ファミリーブロックがあれば張替え経路。空き地起点は従来の新規設置経路
            // A family block at the origin selects the replace run; an empty origin keeps the normal placement run
            if (BeltReplaceRunBuilder.TryResolveOrigin(_blockGameObjectDataStore, dragStartPoint, out var replaceOrigin))
            {
                return _replaceRunBuilder.Build(replaceOrigin, placePoint, isStartDirectionZ, holdingBlock, out blockCauses, out beltReasons);
            }
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltReplaceRunBuilderTest|BeltConveyorSlopePlacementTest|BeltConveyorPlaceRunAxisTest|ConveyorOverpassConveyanceTest|BeltConveyorPlacePointCalculatorTest"`
Expected: 全PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor
git commit -m "feat(client): 既設ライン追従の張替え経路BeltReplaceRunBuilderを追加"
```

---

### Task 6: プレビュー色・張替え送信・Undo（逆張替え）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Common/MaterialConst.cs:29-30`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs:75-79`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PlacementPreviewBlockGameObjectController.cs:68-74`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/ReplaceOperationRecord.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceBlockProtocolSender.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs:31-49, 165-180`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Undo/ReplaceOperationRecordTest.cs`

**Interfaces:**
- Consumes: `ClientContext.VanillaApi.SendOnly.PlaceBlock(List<PlaceInfo>)`、`ClientDIContext.BuildOperationHistory.Push(IBuildOperationRecord)`、`IBuildOperationRecord.UndoAsync(BlockGameObjectDataStore)`
- Produces:
  - `MaterialConst.ReplaceColor : Color`
  - `BlockPreviewObject.SetReplaceColor()`
  - `ReplaceOperationRecord.CreateFrom(List<PlaceInfo> placeInfos, BlockGameObjectDataStore store)`、`HasCells`、`UndoAsync`
  - `static bool PlaceBlockProtocolSender.TrySendReplaceOnClickRelease(List<PlaceInfo> currentPlaceInfos, BlockGameObjectDataStore store)`

- [ ] **Step 1: 張替え色を足す**

`moorestech_client/Assets/Scripts/Client.Common/MaterialConst.cs` の `NotPlaceableColor` の直後に追加:

```csharp
        // 張替えプレビュー色（黄）。設置可の青・不可の赤と区別する
        // Replace preview color (yellow), distinct from placeable blue and unplaceable red
        public static readonly Color ReplaceColor = new(0.95f, 0.78f, 0.2f, 1f);
```

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/BlockPreviewObject.cs` の `SetPlaceableColor` の直後に追加:

```csharp
        public void SetReplaceColor()
        {
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.ReplaceColor);
        }
```

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PlacementPreviewBlockGameObjectController.cs` の `UpdatePlaceableColors` を次に置き換える:

```csharp
        public void UpdatePlaceableColors(List<PlaceInfo> placeInfos)
        {
            for (var i = 0; i < _activePreviewBlocks.Count && i < placeInfos.Count; i++)
            {
                // 設置可の張替えセルだけ張替え色、他は従来の可否2色
                // Placeable replace cells use the replace color; everything else keeps the two placeability colors
                var info = placeInfos[i];
                if (info.Placeable && info.IsReplace) _activePreviewBlocks[i].SetReplaceColor();
                else _activePreviewBlocks[i].SetPlaceableColor(info.Placeable);
            }
        }
```

- [ ] **Step 2: 失敗するテストを書く（ReplaceOperationRecord.CreateFrom）**

Create `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Undo/ReplaceOperationRecordTest.cs`:

```csharp
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Undo
{
    public class ReplaceOperationRecordTest
    {
        private static readonly BlockId OldBlock = new(201);
        private static readonly BlockId NewBlock = new(202);
        private GameObject _dataStoreObject;
        private BlockGameObjectDataStore _dataStore;
        private readonly List<GameObject> _blockObjects = new();

        [SetUp]
        public void SetUp()
        {
            _dataStoreObject = new GameObject("BlockGameObjectDataStore");
            _dataStore = _dataStoreObject.AddComponent<BlockGameObjectDataStore>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var blockObject in _blockObjects) Object.DestroyImmediate(blockObject);
            _blockObjects.Clear();
            Object.DestroyImmediate(_dataStoreObject);
        }

        [Test]
        public void 送信前の既設BlockIdを旧ブロックとして記録し不可セルは含めない()
        {
            Register(new Vector3Int(1, 0, 1), OldBlock, BlockDirection.East);
            Register(new Vector3Int(1, 0, 2), OldBlock, BlockDirection.East);
            var placeInfos = new List<PlaceInfo>
            {
                new() { Position = new Vector3Int(1, 0, 1), Direction = BlockDirection.East, BlockId = NewBlock, IsReplace = true, Placeable = true },
                new() { Position = new Vector3Int(1, 0, 2), Direction = BlockDirection.East, BlockId = OldBlock, IsReplace = true, Placeable = false },
            };

            var record = ReplaceOperationRecord.CreateFrom(placeInfos, _dataStore);

            Assert.IsTrue(record.HasCells);
            var cells = record.BuildUndoPlaceInfos(_dataStore);
            Assert.AreEqual(0, cells.Count);
            Replace(new Vector3Int(1, 0, 1), NewBlock);
            cells = record.BuildUndoPlaceInfos(_dataStore);
            Assert.AreEqual(1, cells.Count);
            Assert.AreEqual(OldBlock, cells[0].BlockId);
            Assert.AreEqual(BlockDirection.East, cells[0].Direction);
            Assert.IsTrue(cells[0].IsReplace);
            Assert.IsTrue(cells[0].Placeable);
        }

        private void Replace(Vector3Int position, BlockId blockId)
        {
            var dictionary = Dictionary();
            SetBackingField(dictionary[position], nameof(BlockGameObject.BlockId), blockId);
        }

        private void Register(Vector3Int position, BlockId blockId, BlockDirection direction)
        {
            var blockObject = new GameObject($"Block_{position}");
            _blockObjects.Add(blockObject);
            var blockGameObject = blockObject.AddComponent<BlockGameObject>();
            SetBackingField(blockGameObject, nameof(BlockGameObject.BlockPosInfo), new BlockPositionInfo(position, direction, Vector3Int.one));
            SetBackingField(blockGameObject, nameof(BlockGameObject.BlockId), blockId);
            Dictionary().Add(position, blockGameObject);
        }

        private Dictionary<Vector3Int, BlockGameObject> Dictionary()
        {
            return (Dictionary<Vector3Int, BlockGameObject>)typeof(BlockGameObjectDataStore)
                .GetField("_blockObjectsDictionary", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_dataStore);
        }

        private static void SetBackingField(BlockGameObject blockGameObject, string propertyName, object value)
        {
            typeof(BlockGameObject)
                .GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(blockGameObject, value);
        }
    }
}
```

- [ ] **Step 3: ReplaceOperationRecord と送信経路を実装する**

Create `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/ReplaceOperationRecord.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    /// 張替え1バッチの履歴レコード。Undoは同セルを旧BlockIdへ張り戻す逆張替え
    /// History record of one replace batch; undo replaces the same cells back to the old BlockId
    /// </summary>
    public class ReplaceOperationRecord : IBuildOperationRecord
    {
        private readonly List<ReplacedCell> _cells;

        private ReplaceOperationRecord(List<ReplacedCell> cells)
        {
            _cells = cells;
        }

        public bool HasCells => 0 < _cells.Count;

        // 送信前に既設のBlockIdを旧ブロックとして控える（送信後は既に差し替わっている）
        // Capture the existing BlockId as the old block before sending (it is already replaced afterwards)
        public static ReplaceOperationRecord CreateFrom(List<PlaceInfo> placeInfos, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var cells = new List<ReplacedCell>(placeInfos.Count);
            foreach (var info in placeInfos)
            {
                if (!info.Placeable || !info.IsReplace) continue;
                if (!blockGameObjectDataStore.TryGetBlockGameObject(info.Position, out var existing)) continue;
                cells.Add(new ReplacedCell(info.Position, info.Direction, existing.BlockId, info.BlockId));
            }
            return new ReplaceOperationRecord(cells);
        }

        public UniTask UndoAsync(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var placeInfos = BuildUndoPlaceInfos(blockGameObjectDataStore);
            if (placeInfos.Count != 0) ClientContext.VanillaApi.SendOnly.PlaceBlock(placeInfos);
            return UniTask.CompletedTask;
        }

        // 現在も新BlockIdのセルだけを旧BlockIdへ戻す（他者変更セルの誤爆防止）
        // Only cells still holding the new BlockId go back to the old one (avoids clobbering cells changed by others)
        public List<PlaceInfo> BuildUndoPlaceInfos(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var placeInfos = new List<PlaceInfo>();
            foreach (var cell in _cells)
            {
                if (!blockGameObjectDataStore.TryGetBlockGameObject(cell.Position, out var current)) continue;
                if (!current.BlockId.Equals(cell.NewBlockId)) continue;
                placeInfos.Add(new PlaceInfo
                {
                    Position = cell.Position,
                    Direction = cell.Direction,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = cell.OldBlockId,
                    IsReplace = true,
                    Placeable = true,
                });
            }
            return placeInfos;
        }

        private readonly struct ReplacedCell
        {
            public readonly Vector3Int Position;
            public readonly BlockDirection Direction;
            public readonly BlockId OldBlockId;
            public readonly BlockId NewBlockId;

            public ReplacedCell(Vector3Int position, BlockDirection direction, BlockId oldBlockId, BlockId newBlockId)
            {
                Position = position;
                Direction = direction;
                OldBlockId = oldBlockId;
                NewBlockId = newBlockId;
            }
        }
    }
}
```

（`ClientContext` の namespace は `PlaceBlockProtocolSender.cs` / `RemoveOperationRecord.cs` の using と同じものを使う。）

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceBlockProtocolSender.cs` の `TrySendOnClickRelease` の直後に追加:

```csharp
        // 張替えは送信前に既設IDを控えた逆張替えレコードを積む
        // Replace pushes a reverse-replace record captured from the existing ids before sending
        public static bool TrySendReplaceOnClickRelease(List<PlaceInfo> currentPlaceInfos, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            if (UiPointerHitTest.IsPointerOverAnyUi()) return false;

            var placeableInfos = currentPlaceInfos.Where(info => info.Placeable).ToList();
            if (placeableInfos.Count == 0) return false;

            var record = ReplaceOperationRecord.CreateFrom(placeableInfos, blockGameObjectDataStore);
            ClientContext.VanillaApi.SendOnly.PlaceBlock(placeableInfos);
            if (record.HasCells) ClientDIContext.BuildOperationHistory.Push(record);
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
            return true;
        }
```

（`using Client.Game.InGame.Block;` と `using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;` が無ければ追加。）

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`:
- フィールド `private readonly BlockGameObjectDataStore _blockGameObjectDataStore;` を追加し、コンストラクタで `_blockGameObjectDataStore = blockGameObjectDataStore;` を代入
- `GroundClickControl` 内の `var cursorIndex = PlacementCellReasonReporter.ApplyGroundOverlapsAndReport(...);` の直後に追加（張替えセルはカーソルの1段下にあり完全一致しないため）:

```csharp
            // 張替え経路のセルは天面ヒットのカーソルより1段下にあるため、XZ一致でカーソルセルを引き直す
            // Replace-run cells sit one step below the top-face cursor, so re-resolve the cursor cell by XZ match
            if (_currentPlaceInfos.Exists(info => info.IsReplace))
                cursorIndex = _currentPlaceInfos.FindIndex(info => info.Position.x == placePoint.x && info.Position.z == placePoint.z);
```

- ローカル関数 `PlaceBlock()` の `TrySendOnClickRelease(_currentPlaceInfos, true);` を次に置き換える:

```csharp
                // 張替え経路は張替え用の送信（逆張替えレコード付き）、通常経路は従来の送信
                // The replace run uses the replace sender (with a reverse-replace record); the normal run keeps the usual sender
                if (_currentPlaceInfos.Exists(info => info.IsReplace)) TrySendReplaceOnClickRelease(_currentPlaceInfos, _blockGameObjectDataStore);
                else TrySendOnClickRelease(_currentPlaceInfos, true);
```

- [ ] **Step 4: コンパイルしてテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ReplaceOperationRecordTest|BeltReplaceRunBuilderTest|PlaceSystemStateController"`
Expected: 全PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Common/MaterialConst.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Undo
git commit -m "feat(client): 張替えプレビュー色・張替え送信・逆張替えUndoを追加"
```

---

### Task 7: moorestech_master の JSON 更新・PR・ピン更新

**Files:**
- Modify（別repo）: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（worktree経由）
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Produces: 実マスタの `beltConveyorFamilies` が4行（各行に `splitterBlockGuid`）、ピンが新コミットを指す

- [ ] **Step 1: master 用 worktree を作り JSON を書き換える**

```bash
git -C ../moorestech_master worktree add ../moorestech_master-belt-replace -b feature/belt-family-splitter-role 61cd90f6c33cf3ea1f8ed692523421113434bbc3
```

`../moorestech_master-belt-replace/server_v8/mods/moorestechAlphaMod_8/master/blocks.json` の `beltConveyorFamilies` を、次のスクリプトで「直線のみの行（分岐器）を、名前が対応する直線行の `splitterBlockGuid` へ吸収」して4行にする（worktree ルートで実行）:

```bash
python3 - <<'EOF'
import json
p='../moorestech_master-belt-replace/server_v8/mods/moorestechAlphaMod_8/master/blocks.json'
d=json.load(open(p, encoding='utf-8'))
names={b['blockGuid']:b['name'] for b in d['data']}
# 直線名 → 分岐器名 の対応（実データの名前で固定）
pairs={'直線歯車ベルトコンベア':'歯車コンベア分岐機','鉄の歯車ベルトコンベア':'鉄の歯車ベルトコンベア分岐機','ベルトコンベア':'ベルトコンベア分岐器','高速ベルトコンベア':'高速ベルトコンベア分岐器'}
guidOf={v:k for k,v in names.items()}
fams=[f for f in d['beltConveyorFamilies'] if names[f['straightBlockGuid']] in pairs]
assert len(fams)==4, fams
for f in fams:
    f['splitterBlockGuid']=guidOf[pairs[names[f['straightBlockGuid']]]]
d['beltConveyorFamilies']=fams
json.dump(d, open(p,'w',encoding='utf-8'), ensure_ascii=False, indent=4)
EOF
git -C ../moorestech_master-belt-replace diff --stat
```

Expected: `blocks.json` のみ変更。`git -C ../moorestech_master-belt-replace diff` で `beltConveyorFamilies` が4行になり各行に `splitterBlockGuid` があることを目視確認する。既存ファイルのインデント（4スペース or 2スペース）と差分が大きい場合は `indent` を既存に合わせ、差分が `beltConveyorFamilies` 周辺だけになるよう調整する。

- [ ] **Step 2: コミット・push・PR**

```bash
git -C ../moorestech_master-belt-replace add server_v8/mods/moorestechAlphaMod_8/master/blocks.json
git -C ../moorestech_master-belt-replace commit -m "feat: beltConveyorFamiliesに分岐器ロール(splitterBlockGuid)を追加し分岐器行を直線行へ吸収（moorestech ADR-0055）"
git -C ../moorestech_master-belt-replace push -u origin feature/belt-family-splitter-role
gh pr create --repo moorestech/moorestech_master --head feature/belt-family-splitter-role --title "beltConveyorFamiliesに分岐器ロールを追加（ADR-0055）" --body "moorestech側 ADR-0055（ベルト張替え設置）に対応。分岐器の単独ファミリー4行を直線行の splitterBlockGuid へ吸収し 8行→4行。"
git -C ../moorestech_master-belt-replace rev-parse HEAD
```

- [ ] **Step 3: ピンを更新してマスタロードを確認する**

`.moorestech-external-revisions.json` の `moorestech_master.commitHash` を Step 2 の `rev-parse` の値へ書き換える。

Run: `uloop compile --project-path ./moorestech_client` → エラー0。続けてマスタの実ロード検証として `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MasterLoad|AlphaMod|VanillaMod"`（実マスタをロードするテストが存在する場合。無ければ Task 8 のプレイテスト起動がロード検証を兼ねる）。

- [ ] **Step 4: コミットする**

```bash
git add .moorestech-external-revisions.json
git commit -m "chore: masterピンを分岐器ロール追加コミットへ更新"
```

---

### Task 8: unityプレイ録画テスト（ティア張替え・直線/上り/分岐器）

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-replace-tier-via-ui.cs`

**Interfaces:**
- Consumes: プレイテストDSL（`PlaytestRunner.Run`、`p.SkipOpeningSkit` / `p.SetupFlatGround` / `p.WarpPlayer` / `p.PrepareBlockForUiPlacement` / `p.PlaceBlockDirect` / `p.WaitBlockGameObject` / `p.GetBlock` / `p.OpenBuildMenuAndSelectBlock` / `PlaytestUiOps.PlaceAimPoint` / `PlaytestUiOps.DragPlace` / `p.ExitToGameScreen` / `p.Until` / `p.Assert` / `p.Screenshot` / `PlaytestBlockOps.ResolveBlockId` / `PlaytestItemOps.ResolveItemId`。全て `moorestech_client/Assets/Scripts/Client.Playtest/` に実在を確認済み）。Driver API の正は `.agents/skills/unity-playmode-recorded-playtest/references/write-scenario.md`。

- [ ] **Step 1: シナリオを書く**

Create `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-replace-tier-via-ui.cs`:

```csharp
// ベルト張替え設置検証（ティア差し替え）: 木の歯車ラインを直設置（直線3・上り1・直線1・分岐器1）し搬送品を載せ、
// ビルドメニューで鉄の歯車ベルトを選んで既設起点からドラッグ→全セルが鉄の同ロールへ差し替わり搬送品が残ることを検証する
// Belt replace (tier swap) scenario: direct-place a wood gear line (3 straight, 1 up, 1 straight, 1 splitter) with transit items,
// select the iron gear belt in the build menu, drag from the existing origin, and verify every cell becomes the iron same-role block with items kept
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-replace-tier-via-ui", options, async p =>
{
    await p.SkipOpeningSkit();
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(2.5f, 33.5f, -1f));

    // 鉄ティアを解放しコストを付与（張替えは新コスト消費のため）
    // Unlock the iron tier and grant its cost (replace consumes the new cost)
    await p.PrepareBlockForUiPlacement("鉄の歯車ベルトコンベア", 12);
    await p.PrepareBlockForUiPlacement("鉄の歯車ベルトコンベア分岐機", 3);

    p.Note("木の歯車ライン（直線3・上り・直線・分岐器）をサーバー直設置");
    p.PlaceBlockDirect("直線歯車ベルトコンベア", new Vector3Int(2, 32, 2), BlockDirection.North);
    p.PlaceBlockDirect("直線歯車ベルトコンベア", new Vector3Int(2, 32, 3), BlockDirection.North);
    p.PlaceBlockDirect("直線歯車ベルトコンベア", new Vector3Int(2, 32, 4), BlockDirection.North);
    p.PlaceBlockDirect("上り歯車ベルトコンベア", new Vector3Int(2, 32, 5), BlockDirection.North);
    p.PlaceBlockDirect("直線歯車ベルトコンベア", new Vector3Int(2, 33, 6), BlockDirection.North);
    p.PlaceBlockDirect("歯車コンベア分岐機", new Vector3Int(2, 33, 7), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(2, 33, 7));
    await p.Screenshot("01-wood-line");

    p.Note("搬送品2個を先頭ベルトへ投入");
    var itemId = PlaytestItemOps.ResolveItemId("鉄インゴット");
    var headBelt = p.GetBlock(new Vector3Int(2, 32, 2)).GetComponent<VanillaBeltConveyorComponent>();
    for (var i = 0; i < 2; i++)
    {
        headBelt.InsertItem(ServerContext.ItemStackFactory.Create(itemId, 1), InsertItemContext.Empty);
        await p.WaitSeconds(0.5f);
    }

    var cells = new[] { new Vector3Int(2, 32, 2), new Vector3Int(2, 32, 3), new Vector3Int(2, 32, 4), new Vector3Int(2, 32, 5), new Vector3Int(2, 33, 6), new Vector3Int(2, 33, 7) };
    System.Func<int> countOnLine = () =>
    {
        var total = 0;
        foreach (var cell in cells)
        {
            var belt = p.GetBlock(cell);
            if (belt == null) continue;
            foreach (var item in belt.GetComponent<VanillaBeltConveyorComponent>().BeltConveyorItems)
            {
                if (item != null) total++;
            }
        }
        return total;
    };
    await p.Until(() => countOnLine() == 2, 30f, "搬送品2個がライン上にある");

    p.Note("ビルドメニューで鉄の歯車ベルトを選び、既設起点から終点まで張替えドラッグ");
    await p.OpenBuildMenuAndSelectBlock("鉄の歯車ベルトコンベア");
    // 既設ベルトの天面（y=33/34平面）を照準する。起点は1段浮いた座標として解決される
    // Aim the belt top faces (y=33/34 planes); the origin resolves from the floated cell
    var fromAim = PlaytestUiOps.PlaceAimPoint("鉄の歯車ベルトコンベア", new Vector3Int(2, 33, 2), BlockDirection.North);
    var toAim = PlaytestUiOps.PlaceAimPoint("鉄の歯車ベルトコンベア", new Vector3Int(2, 34, 7), BlockDirection.North);
    await PlaytestUiOps.DragPlace(fromAim, toAim);
    await p.ExitToGameScreen();

    var expected = new[] { "鉄の歯車ベルトコンベア", "鉄の歯車ベルトコンベア", "鉄の歯車ベルトコンベア", "鉄の上り歯車ベルトコンベア", "鉄の歯車ベルトコンベア", "鉄の歯車ベルトコンベア分岐機" };
    await p.Until(() =>
    {
        for (var i = 0; i < cells.Length; i++)
        {
            var block = p.GetBlock(cells[i]);
            if (block == null) return false;
            var expectedId = PlaytestBlockOps.ResolveBlockId(expected[i]);
            if (block.BlockId != expectedId) return false;
            if (block.BlockPositionInfo.BlockDirection != BlockDirection.North) return false;
        }
        return true;
    }, 15f, "張替え: 6セル全てが鉄ティアの同ロール・北向き");
    p.Assert(countOnLine() == 2, "張替え後も搬送品2個が保持されている");
    await p.WaitBlockGameObject(new Vector3Int(2, 33, 7));
    await p.Screenshot("02-iron-line");
});
```

（`p.DragPlaceViaUi` は起点・終点セルにブロックが現れるのを待つため、1段浮いた照準を使う張替えには使えない。`OpenBuildMenuAndSelectBlock` → `PlaytestUiOps.PlaceAimPoint` → `PlaytestUiOps.DragPlace` の分解形を使う。Driver API の実体: `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs`、`Operations/PlaytestBlockOps.cs`、`Operations/Ui/PlaytestUiOps.cs`。）

- [ ] **Step 2: 実行する**

```bash
pwd   # /Users/katsumi/moorestech-belt-replace
git -C ../moorestech_master worktree list   # Task 7 の worktree が新コミットを指していることを確認
SKILL=.claude/skills/unity-playmode-recorded-playtest
uloop control-play-mode --project-path ./moorestech_client --action stop
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/building/belt-replace-tier-via-ui.cs"
```

Expected: result.json の Asserts 全PASS、動画が0byteでない、スクショ `02-iron-line` に鉄ティアのラインが写る。失敗したら `references/troubleshooting.md` に従う。

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-replace-tier-via-ui.cs
git checkout -- moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs   # Unity自動書き換え分のみ戻す（Task 1で意図した値がHEADなら不要）
git commit -m "test(playtest): ベルト張替え（ティア差し替え）のunityプレイ録画テストを追加"
```

---

### Task 9: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

必ず最後にコードレビュースキル（moores-code-review）で全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー記録は `../moorestech_logs/harness/` へ書き、feature ブランチにはコミットしない。

- [ ] **Step 2: 指摘を反映する**

指摘の反映が判定経路・条件式・その評価時点（`BeltReplaceRunBuilder` の探索順、`BeltReplacePlacementService` の検証順、`TryRestoreItem` のスロット計算）に触れた場合は、Task 8 のプレイテストを反映後のバイナリで再実行してから完了とする。テスト通過・ログ無音は代替にならない。

- [ ] **Step 3: 全テストとコミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Belt|PlaceBlock|RemoveBlock|Construction|PlacementTarget"`
Expected: 全PASS

```bash
git status   # 未コミットが無いこと
```

その後 pr-create スキルで PR を作る（本文に moorestech_master 側 PR の URL を併記）。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 | 前例・判定 |
|---|---|---|---|---|
| 1 | `BeltConveyorRole` / `BeltConveyorFamily` 拡張 / `ResolveSlopeRepresentative*` | Game.Block.Interface/Extension | マスタ生成物を読むだけの static util | 既存 `BeltConveyorPlaceFamilyUtil`（同ファイル群）。層マップ「マスタ値のドメイン解釈は Game.Xxx.Interface の static util」に合致。ok |
| 2 | `BeltConveyorFamilyValidator` 分岐器検証 | Core.Master/Validator | 汎用 Validate | validate-schema スキルの規定どおり。ok |
| 3 | `ConstructionWalletUtil.ResolveWalletBlockId` / `BeltConveyorPlacementUnlockSourceMap` | 既存クラスの実装差し替え | util 委譲 | 判断（坂のみ直線）を1箇所（#1）へ集約し、財布・解放は問い合わせるだけ。ok |
| 4 | `BlockRemoveReason.Replace` | Game.Block.Interface | enum 追加 | `switch` で網羅している購読者は無し（grep済）。ok |
| 5 | `PlaceInfo.IsReplace` / `PlaceInfoMessagePack [Key(5)]` | Server.Protocol/PacketResponse/PlacePacketDto.cs | 既存プロトコルへのフィールド追加 | 1プロトコル1ドメイン（新プロトコル新設なし）。旧spec と同形。ok |
| 6 | `BeltTransitItem` / `BeltConveyorTransitCarryOver` / `VanillaBeltConveyorComponent.TryRestoreItem` | Game.Block/Blocks/BeltConveyor | コンポーネントのドメインAPI | 進行率の算術は `UpdateTicksForSpeedChange` / `Update` の送り条件と同じ規則。ベルトの内部構造はベルト側が所有。ok |
| 7 | `BeltReplacePlacementService` | Server.Protocol/PacketResponse/Util/Construction | プロトコル配下のオーケストレーション | 同ディレクトリの `ConstructionWalletService` / `ConstructionCostService`、`RemoveBlockProtocol` の撤去手順が前例。財布へは Plan を問い合わせ Commit するだけ（指示を返すサービスの線引き遵守）。ok |
| 8 | `PlaceBlockProtocol` の isReplace 分岐 | 既存プロトコル | Mode 分岐 | 旧 d188b6382 と同形。ok |
| 9 | `BeltConveyorHoldingBlock.Role/RunUp/RunDown` | Client PlaceSystem/BeltConveyor/Parts | 選択→手持ち解決 | 既存クラスの責務内。分岐器を「自ファミリーの直線」として扱っていた暗黙をロールで明示化。ok |
| 10 | `BeltReplaceRunBuilder` | Client PlaceSystem/BeltConveyor/Replace（新ディレクトリ） | 経路生成（純関数＋DataStore読み） | `BeltConveyorSlopePathBuilder`（ADR 0050 の坂選択経路）と同じ「RunBuilder が起点条件で経路生成を分岐」。データフロー: 入力→`BeltConveyorPlaceRunBuilder`→`PlaceInfo`列→プレビュー/送信 の既存一方向連鎖に「書き手」として参加。交差点なし。**新規パターン**: 既設ライン追従（Y±1探索）。ADR 0055 ユーザー裁定 |
| 11 | 張替え色（`MaterialConst.ReplaceColor` / `SetReplaceColor`） | Client.Common / PreviewController | 既存2色に1色追加 | `PlaceableColor`/`NotPlaceableColor` と同じ定義場所。ok |
| 12 | `ReplaceOperationRecord` / `TrySendReplaceOnClickRelease` | Client PlaceSystem/Undo, Util | 既存レコード型と同型 | `PlaceOperationRecord` / `RemoveOperationRecord`。ok |
| 13 | `ReplaceRoleMissing` 理由 | `BeltConveyorPlacementBlockReason` | ベルト固有 enum | `PlacementBlockCause` のコメント「系固有の原因は専用enumへ」に合致。ok |
| 14 | `splitterBlockGuid: optional` | blocks.yml | optional | 同スキーマの `upBlockGuid/downBlockGuid` が optional で「坂を持たないファミリー」を表す前例。分岐器を持たないファミリー（テストmod）も正当。agent前提 |

機構選択（検査4）: 既存の設置パイプライン（`BeltConveyorPlaceSystem` → `RunBuilder` → `PlaceInfo` → `PlaceBlockProtocolSender` → `va:placeBlock`）を抑止・迂回せず、RunBuilder の分岐と DTO の1フラグで相乗りする受動的統合を採用。能動介入案（張替え専用プロトコル・専用 PlaceSystem）は「1プロトコル1ドメイン」「ベルト系は既に BeltConveyorPlaceSystem 一択」により不要。

機能パリティ（死活表）:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 空き地起点のベルトドラッグ（坂自動・立体交差） | 生きる | RunBuilder は起点に既設ファミリーが無ければ従来経路（Task 5 テスト4） |
| 分岐器のドラッグ設置（全セル分岐器） | 生きる | `RunUp/RunDown=null` で坂セルは `SlopeBlockMissing`、水平セルは分岐器（Task 4） |
| 分岐器の財布・解放 | 生きる（自身のまま） | `ResolveSlopeRepresentative*`（Task 1 テスト） |
| 坂の財布・解放（直線代表） | 生きる | 同上 |
| 既設ベルト上でのホバー/クリック（従来は赤の不可プレビュー） | 変わる: 張替えプレビュー（黄）／同ティアなら無表示 | ADR 0055 ユーザー裁定（単クリックは張替え） |
| Ctrl+Z（設置/撤去） | 生きる | レコード型追加のみ |
| 通常撤去の搬送品返却 | 生きる | `RemoveBlockProtocol` 不変。張替えは `WorldBlockDatastore.RemoveBlock` 直呼び |

## 判断記録（ADR）

- 設計ADR: `docs/adr/0055-belt-conveyor-replace-placement-by-family-role.md`（ユーザー裁定9件＋agent前提）。裁定の蒸留: `.decisions/2026-09-10-ベルト張替えはファミリー間無制限で形状維持のティア差し替えとする.md`、`.decisions/2026-09-10-張替えドラッグは既設の無いセルを飛ばして続行する.md`、`.decisions/2026-09-10-ベルト張替えのサーバー処理は2026-07-22の裁定を引き継ぐ.md`、`.decisions/2026-09-10-フィルター分岐器はベルト張替えに含めない.md`、`.decisions/2026-09-10-ベルト張替えの発動条件と高さ追従と失敗セルとUndo.md`
- planning中の判断:
  - サーバーは手持ちの `Direction` を無視し既設の向きで設置する（形状維持の保証をサーバー側でも担う）。出所: agent前提（ADR 0055「形状維持」の含意）
  - サーバーは既設ロールと手持ちロールの一致を検証し、不一致は拒否ログ。出所: agent前提（fail-closed ログ規約・改造クライアント防御）
  - 搬送品の復元は `TryRestoreItem` で「残りtickから所属スロットを逆算し、理想→入口側→出口側の順で空きを探す」。出所: agent前提（`Update` の送り条件の逆算）
  - 同ファミリー同ロール（no-op）セルはプレビューにも出さない（不可色で赤く見せない）。出所: agent前提（ADR「no-op は送信しない」の表示側解釈）
  - `BeltConveyorStraightCellBlockResolver` の引数を `BlockId` 直指定へ変え、分岐器手持ちの「坂なし」を `BeltConveyorHoldingBlock.RunUp/RunDown=null` で表す。出所: agent前提（分岐器を吸収したことでファミリーの Up/Down が分岐器手持ちにも見えるようになったため）
  - `splitterBlockGuid` は `optional: true`。出所: agent前提（同スキーマの up/down が optional で「持たないファミリー」を表す前例）
  - クライアントのコスト先読みは張替えセルを新コスト要求として数える簡略化のまま（返却込みの精密予測はしない）。出所: agent前提（旧spec 2026-07-22 と同じ簡略化）
  - 張替え経路のカーソルセルは XZ 一致で引き直す（`PlacementCursorCellResolver` は完全一致→末尾フォールバックのため、そのままでは役割欠落セルのツールチップが末尾セルに寄る）。出所: agent前提（ツールチップ規約）
  - 高さオフセット（Q/E）中のホバーは起点が1段以上浮くため張替え起点にならない（ドラッグ開始後は起点固定なので影響しない）。出所: agent前提（ADR「張替え中は高さオフセットを使わない」の帰結）
  - テストmodに `SmallGearBeltConveyorSplitter` を追加し「分岐器を持つ2ファミリー間の張替え」を検証可能にする。出所: agent前提（forUnitTest に分岐器が1つしか無いため）
