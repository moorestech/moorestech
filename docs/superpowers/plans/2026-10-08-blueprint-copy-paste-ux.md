# Blueprint Copy/Paste UX Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ブループリント（BP）のコピー範囲指定を「1クリック始点・1クリック終点」にし、貼り付けにQ/E高さとドラッグ列設置を足し、保存済みBPにサムネイル画像を出す。

**Architecture:** コピー始点・終点・貼り付けアンカーのセルは通常1x1設置と同じ `PlaceSystemUtil.CalcPlacePoint`（サイズ1x1x1）＋ `PlacementHeightOffset` で決め、BP専用のスナップ（`SnapHitPointToCell`）を設置系から外す。コピー範囲は2セルのAABB、連続設置は通常設置の列計算を位置計算だけ抽出して流用する。サーバーのアンカーはブロック外形のXZ中心・最下段へ変える（オフセットは相対値のままなのでセーブ形式不変）。サムネイルはクライアントで `BlockIconImagePhotographer` を主シーンへ持ち越して撮影し、既存の `/api/*-icons/{key}.png` 経路で配信する。

**Tech Stack:** Unity / C# / NUnit / UniRx / VContainer / Web UI（React・zod・vitest）/ uloop / unityプレイ録画テスト（`unity-playmode-recorded-playtest`）。

## Requirements

- R1 コピー始点・終点・貼り付けアンカーのセルは通常1x1設置と同じ解決（ヒット面の外側セル＋Q/E高さ）。受入: チェスト東面(3.0,32.55,2.25)ヒットで始点セルが (3,32,2)（現行は (3,33,2)）。地面ヒットのYは `PlacementGroundCellResolver.ResolveCellY` 経由。
- R2 コピー範囲は始点セルと終点セル（各々Q/E込み）を対角とするAABB。ホイール高さ調整・既定上面+4を廃止し、コピー中のホイール占有も無くす。受入: 地面(0,32,0)→地面(4,32,4) で範囲 min(0,32,0) max(4,32,4)、Eを1回押して終点を選ぶと max.y=33。
- R3 操作は1クリック（押下→解放）で始点、もう1クリックで終点。終点確定で名前入力モーダルを開き、確定でCreate送信して選択を畳む。モーダルのキャンセル（Web側ESC/閉じる、ゲーム側ESC/右短押し）は始点を保ったまま終点選択へ戻る。右短押し/ESCは終点選択中なら始点を破棄して未選択へ戻し、未選択なら建築モードを抜ける（既存二段階解除）。受入: 録画テストで ESC 後に始点マーカーが残り終点マーカーが追従する。
- R4 プレビュー: 始点候補/始点は赤い半透明1x1キューブ、終点候補/終点は緑の半透明1x1キューブ、範囲は半透明の直方体。受入: 録画テストで `BlueprintCopyRangeVisualizer` の3オブジェクトの active とサイズを assert。
- R5 終点選択中はカーソルツールチップに「範囲内のブロック: N」を常時出し、N=0 なら終点クリックで名前入力へ進まず「範囲にブロックがありません」を出す。判定はクライアントのブロック情報で、サーバーと同じ交差規則（占有セルが1つでも範囲内・レール系除外）。受入: 範囲外で終点クリックしてもモーダルが開かず、ツールチップ行が2行になる。
- R6 Create の失敗応答（EmptyArea等）はログ（`Debug.LogError`）とローカル通知 `denied.blueprint.CreateFailed`（{p0}=理由）の両方へ出す。受入: `ClientBlueprintLibrary.CreateBlueprint` が理由を返し、失敗時に通知を出すテスト。
- R7 貼り付け中のQ/Eで高さが変わる（`PlacementHeightOffset` を読む・`UsesPlacementHeight=true`）。受入: E押下後にゴーストYが+1、HUDの高さ表示が1。
- R8 貼り付けの左ドラッグで一列に並べて設置する。間隔はBP内ブロック全体の外形（回転後AABB寸法）。解放で置けるセルだけ送信、置けないものは赤ゴースト。右短押し/ESCはドラッグだけを畳む。受入: (6,32,6)→(10,32,6) ドラッグで1x1チェストBPが5個置かれる。
- R9 サーバーのアンカーはコピー範囲の中心ではなく「コピーされたブロック群の外形のXZ中心セル・最下段」。受入: 範囲 (0,0,0)-(9,2,9) にチェスト1個(0,0,0)のときオフセットが (0,0,0)。
- R10 保存済みBPのサムネイル: BP内ブロックのプレファブをオフセット通りに並べ `BlockIconImagePhotographer` で撮影。クライアント要求時生成・メモリキャッシュ・`/api/blueprint-icons/{guid}.png` で配信。ビルドメニューとホットバーの両方で画像が出る。サムネイル未生成の間は IconUrl を出さず名前表示のまま、生成完了で再配信。受入: 録画テストで `BlueprintThumbnailContainer.Contains(guid)` が true になりスクショに画像が映る。
- R11 境界の大型ブロックは一部でも範囲に入れば丸ごとコピー（現行維持）。範囲内ブロック数にも数える。
- R12 貼り付け・列設置で一部が重なる/置けないときは置けるブロックだけ設置（現行の部分設置維持、列でも要素単位）。
- 非目標: セーブ形式の変更、Webモーダルの変更、コピー時の選択範囲寸法の保存、BPの編集機能、ホットバーの画像以外の変更、接続ツールのホイール占有の変更。

## Global Constraints

- 全コード200行未満・1ディレクトリ10ファイルまで・partial禁止・`Func<>`禁止・イベントはUniRx・デフォルト引数禁止・try-catchは外部境界のみ。.cs変更後は `uloop compile --project-path ./moorestech_client`。
- Unity YAML（シーン・Prefab・.meta）の手編集禁止。`.meta` は `git mv` でファイルと一緒に動かす。
- サーバーのゲームロジックに実時間APIを使わない（本planは時間計測なし）。
- fail-closed の拒否・無視経路は必ず開発者向けログを出す（AGENTS.md）。
- 辞書キーは `Localization/localization.csv`（列: key,Source,english,japanese,german,korean）へ追加し、C#は `LocalizationKeys.*` 自動生成、Webは `moorestech_web/webui` で `pnpm gen:i18n` を実行してコミット。
- Web UI の変更後は `moorestech_web/webui` で `pnpm test` と `pnpm build`、`pnpm build` 後は `moorestech_client/Assets/StreamingAssets/WebUi/dist` へ反映（既存ビルド手順 `moorestech_web/README.md`）。
- 作業場所: worktree `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/blueprint-copy-paste-ux`（branch `feat/blueprint-copy-paste-ux`、origin/master `2e69d16ac` マージ済み）。Editor は同 worktree のものだけを使う。プレイテストは `run-scenario.sh` の第3引数に `/Users/sakastudio/hermes-agent/data/repos/moorestech_master/server_v8` を渡す（HEAD 47f79caa が互換ピン）。
- 背景Editorでは InputSystem がデバイスを無効化し注入が落ちる。録画シナリオ冒頭で `InputSystem.settings.backgroundBehavior=IgnoreFocus`・`editorInputBehaviorInPlayMode=AllDeviceInputAlwaysGoesToGameView`・`EnableDevice(Keyboard/Mouse)` を入れる（beads moorestech-xsd18.4 (b)）。
- Beads: 本件 `moorestech-zbwxs`。関連 `moorestech-izz2`（BP貼り付けYのRound/Floor不一致。本planのR1で解消）。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / Create失敗の表現をサーバー `BlueprintFailureReason` 拡張からクライアント側 `BlueprintCreateFailure`（前例 `BlueprintDeleteResult`）へ。配置表・死活表は「配置と前例」節
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強4・弱9・第3バケツ1 / 強4は全て反映または理由付きで据え置き（判断記録参照）、弱は4件反映・5件理由付き据え置き、第3バケツは既存IsOverlapへの委譲で解消

## 実測根拠（2026-10-08）

worktree `blueprint-copy-paste-ux` の `moorestech_client/PlaytestResults/20261008_180049/blueprint-copy-box-probe/`（録画・スクショ・result.json。プローブシナリオは本planのTask 8で録画テストへ置き換える）。

| 症状 | 実測値 | 原因（コード） |
|---|---|---|
| 高さ膨張 | 地面→地面 高さ5、終点チェスト天面 6、石窯天面 7 | `BlueprintCopySystem.CalcBox` が `max(start.y,end.y) + _topYOffset(4)` |
| 1マスずれ | 東面ヒット (3.07,32.55,2.25) → 始点 (3,33,2)、南面 → (2,33,1) | `SnapHitPointToCell` の Y RoundToInt・XZ Floor |
| 空範囲が無通知 | probeD で BP 未登録（EmptyArea）、UI無反応 | `CreateBlueprint(...).Forget()` が結果を捨てる |
| Q/E無効 | E後も height 0、ゴースト (6,32,6) 不変 | `BlueprintPasteSystem` が高さを読まない |
| サムネイル無し | BP枠は名前テキストのみ | `ResolveIconUrl` がBPに null |

## 配置と前例（レイヤリング制約）

| 項目 | 配置 | 前例 |
|---|---|---|
| コピー対象規則・外形計算 | `Game.Blueprint`（サーバー asm、クライアントからも参照済み） | `BlueprintPasteCalculator.cs` |
| 1x1セル解決 | `Client.Game/.../PlaceSystem/Util/PlacementUnitCellResolver.cs`（`PlaceSystemUtil.CalcPlacePoint` を呼ぶだけ） | `PlaceSystemUtil.TryGetRayHitBlockPosition` |
| 列位置計算の抽出 | `Common/Run/PlacementRunPositionCalculator.cs`（純関数）。`CommonBlockPlacePointCalculator.CalculateRun` が委譲 | `PlacementRun`/`PlacementRunAxis` |
| コピー/貼り付けの設置系 | `PlaceSystemBase<T>` 継承・毎フレーム `ManualUpdate` 駆動。入力検知と対象検知は系内部 | `CommonBlockPlaceSystem` |
| 高さ | `PlacementHeightOffset` を読むだけ（`UsesPlacementHeight=true`）。Q/E解釈は `PlacementHeightKeyInput` へ抽出し `CommonBlockPlaceDragState` と共用 | `CommonBlockPlaceDragState.UpdateHeightOffsetByInput` |
| 設置系固有のツールチップ行 | `Blueprint/Copy/BlueprintCopyFeedbackLines.cs`（`PlacementFeedback` へ `Add`） | `ElectricWireFeedbackLines.cs` |
| 失敗通知 | `ClientDIContext.ClientLocalNotificationSource.Notify(NotificationMessagePack.CreateOperationDenied(...))` | `VanillaRemovalRestoreSender.cs:44` |
| サムネイル保持 | `Client.Game/InGame/Context/BlueprintThumbnailContainer.cs`、`ClientContext` の静的プロパティ | `BlockImageContainer` |
| サムネイル生成の駆動 | DI `RegisterEntryPoint<BlueprintThumbnailRenderer>()`（`IInitializable`、`ClientBlueprintLibrary.OnChanged` 購読＝表示専用オブザーバ） | `MainGameModelRegistration.RegisterEntryPoint<HotbarNetworkEventHandler>` |
| アイコン配信 | `Client.WebUiHost/Game/Icons/BlueprintIconSource.cs`、`IconEndpoint._sources` へ登録 | `TrainCarIconSource.cs` |
| 撮影器の寿命 | `InitializeScenePipeline` で `DontDestroyOnLoad` し `ClientContext.BlockIconImagePhotographer` へ | `ServerConnectionInitializer.cs:115` の DontDestroyOnLoad |

機構選択（検査4）: 既存の `PlaceSystemStateController`/`PlaceBlockState` の駆動・二段階解除は無傷のまま、BP系2クラスの内部だけを書き換える（受動的統合）。能動介入（状態機械の抑止・許可リスト）は無し。

死活表（Phase 2.5）: B（建築モード終了）生存・Tab（メニュー再表示）生存・R（回転）生存・Q/E（高さ）コピー/貼り付けで新たに生存・右短押し/ESC（二段階解除）生存・ホイール（コピー中の高さ調整）廃止→装備切替へ戻る（ユーザー裁定 D2）・左クリック貼り付け生存（ドラッグ列の1点ケース）・BP右クリック削除（Web）生存・ホットバー割当生存。

---

### Task 1: サーバー: コピー対象規則の共有化とアンカーのブロック外形化

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintCopyTargetRule.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintCreateService.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintCreateServiceTest.cs`

**Interfaces:**
- Consumes: `BlockPositionInfo.MinPos/MaxPos`（inclusive）、`BlockMasterElement.BlockType`
- Produces: `public static class BlueprintCopyTargetRule { static bool IsCopyTarget(BlockMasterElement master); static bool IntersectsBox(BlockPositionInfo positionInfo, Vector3Int min, Vector3Int max); }`（Task 5 のクライアント計数が同じ規則を使う）

- [ ] **Step 1: 規則クラスを書く**

```csharp
// moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintCopyTargetRule.cs
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Game.Blueprint
{
    /// <summary>
    ///     コピー対象かどうかの規則。サーバーの抽出とクライアントの範囲内計数が同じ答えを出すための唯一の所有者
    ///     The rule deciding what a blueprint copies; the single owner so the server extraction and the client range count agree
    /// </summary>
    public static class BlueprintCopyTargetRule
    {
        // レール系はブロック外ドメイン（RailSegments）を持つためコピー対象外
        // Rail-family blocks are excluded; their graph lives outside block states
        private static readonly HashSet<string> ExcludedBlockTypes = new()
        {
            BlockTypeConst.TrainRail,
            BlockTypeConst.TrainStation,
            BlockTypeConst.TrainItemPlatform,
            BlockTypeConst.TrainFluidPlatform,
        };

        public static bool IsCopyTarget(BlockMasterElement master)
        {
            return !ExcludedBlockTypes.Contains(master.BlockType);
        }

        // 占有セルの一部でも範囲に入れば対象。範囲を1つの占有情報に見立て既存のAABB交差（IsOverlap）へ委ねる
        // Included when any occupied cell intersects the box; the box is treated as one footprint and handed to the existing AABB overlap (IsOverlap)
        public static bool IntersectsBox(BlockPositionInfo positionInfo, Vector3Int min, Vector3Int max)
        {
            var box = new BlockPositionInfo(min, BlockDirection.North, max - min + Vector3Int.one);
            return positionInfo.IsOverlap(box);
        }
    }
}
```

- [ ] **Step 2: BlueprintCreateService を規則とブロック外形アンカーへ書き換える**

```csharp
// moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintCreateService.cs（全文）
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Blueprint
{
    public static class BlueprintCreateService
    {
        public static bool TryCreateFromArea(string name, Vector3Int min, Vector3Int max, out BlueprintJsonObject blueprint)
        {
            var targets = CollectTargets();
            if (targets.Count == 0)
            {
                blueprint = null;
                return false;
            }

            // アンカー = コピーされたブロック群の外形のXZ中心セル・最下段（範囲の余白でゴーストがカーソルからずれない）
            // Anchor: XZ center cell and bottom Y of the copied blocks' extent, so selection margins never shift the ghost off the cursor
            var anchor = CalcAnchor(targets);
            var blocks = new List<BlueprintBlockJsonObject>();
            foreach (var data in targets)
            {
                blocks.Add(CreateBlockJson(data, anchor));
            }

            blueprint = new BlueprintJsonObject(name, blocks, GameRandom.NextGuid());
            return true;

            #region Internal

            List<WorldBlockData> CollectTargets()
            {
                var result = new List<WorldBlockData>();
                foreach (var data in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
                {
                    var master = MasterHolder.BlockMaster.GetBlockMaster(data.Block.BlockId);
                    if (!BlueprintCopyTargetRule.IsCopyTarget(master)) continue;
                    if (!BlueprintCopyTargetRule.IntersectsBox(data.Block.BlockPositionInfo, min, max)) continue;
                    result.Add(data);
                }

                return result;
            }

            // 負座標でも1セルずれないようfloorで丸める
            // Floor so negative coordinates never shift a cell
            Vector3Int CalcAnchor(List<WorldBlockData> copyTargets)
            {
                var extentMin = copyTargets[0].Block.BlockPositionInfo.MinPos;
                var extentMax = copyTargets[0].Block.BlockPositionInfo.MaxPos;
                foreach (var data in copyTargets)
                {
                    extentMin = Vector3Int.Min(extentMin, data.Block.BlockPositionInfo.MinPos);
                    extentMax = Vector3Int.Max(extentMax, data.Block.BlockPositionInfo.MaxPos);
                }

                return new Vector3Int(Mathf.FloorToInt((extentMin.x + extentMax.x) / 2f), extentMin.y, Mathf.FloorToInt((extentMin.z + extentMax.z) / 2f));
            }

            BlueprintBlockJsonObject CreateBlockJson(WorldBlockData data, Vector3Int anchorPos)
            {
                var master = MasterHolder.BlockMaster.GetBlockMaster(data.Block.BlockId);
                var offset = data.Block.BlockPositionInfo.OriginalPos - anchorPos;
                var direction = (int)data.Block.BlockPositionInfo.BlockDirection;

                // 設定持ちコンポーネントからJSON収集
                // Collect settings JSON from settings-providing components
                var settings = new Dictionary<string, string>();
                foreach (var component in data.Block.ComponentManager.GetComponents<IBlockBlueprintSettings>())
                {
                    settings[component.BlueprintSettingsKey] = component.GetBlueprintSettingsJson();
                }

                return new BlueprintBlockJsonObject(offset, master.BlockGuid.ToString(), direction, settings);
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: テストをブロック外形アンカーへ更新し、範囲中心ではないことを固定する**

`BlueprintCreateServiceTest` の3テストを次に置き換える（`using Game.Block.Interface;` 等は既存のまま）:

```csharp
[Test]
public void AreaExtractionTest()
{
    var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
        .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

    // 箱内2/XZ範囲外1/Y範囲外1を設置
    // Two inside the box, one outside XZ, one above the box top
    ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
    ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(3, 0, 4), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _);
    ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(100, 0, 100), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
    ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(2, 5, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

    var created = BlueprintCreateService.TryCreateFromArea("test", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5), out var blueprint);

    Assert.IsTrue(created);
    Assert.AreEqual(2, blueprint.Blocks.Count);

    // アンカーはブロック外形（チェスト(0,0,0)と機械のMaxPos）のXZ中心・最下段
    // The anchor is the XZ center / bottom of the blocks' extent (chest at origin and the machine's MaxPos)
    var machineInfo = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(3, 0, 4)).BlockPositionInfo;
    var expectedAnchor = new Vector3Int(Mathf.FloorToInt(machineInfo.MaxPos.x / 2f), 0, Mathf.FloorToInt(machineInfo.MaxPos.z / 2f));

    var chestBlock = blueprint.Blocks.First(b => b.Direction == (int)BlockDirection.North);
    Assert.AreEqual(new Vector3Int(0, 0, 0) - expectedAnchor, chestBlock.Offset);
    var machineBlock = blueprint.Blocks.First(b => b.Direction == (int)BlockDirection.East);
    Assert.AreEqual(new Vector3Int(3, 0, 4) - expectedAnchor, machineBlock.Offset);
}

[Test]
public void AnchorFollowsBlockExtentNotBoxTest()
{
    var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
        .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

    // 範囲は広いがブロックは1個。範囲中心(4,0,4)基準なら(-4,0,-4)、外形基準なら(0,0,0)
    // A wide box with a single block: box-center anchoring would give (-4,0,-4); extent anchoring gives (0,0,0)
    ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

    var created = BlueprintCreateService.TryCreateFromArea("extent", new Vector3Int(0, 0, 0), new Vector3Int(9, 2, 9), out var blueprint);

    Assert.IsTrue(created);
    Assert.AreEqual(new Vector3Int(0, 0, 0), blueprint.Blocks[0].Offset);
}

[Test]
public void NegativeCoordinateAnchorIsFlooredTest()
{
    var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
        .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

    // 外形(-4..-1)の中心はfloorで-3（ゼロ方向丸めだと-2）
    // The extent (-4..-1) centers at -3 by floor (truncation toward zero would give -2)
    ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(-4, 0, -4), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
    ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

    var created = BlueprintCreateService.TryCreateFromArea("negative", new Vector3Int(-4, 0, -4), new Vector3Int(-1, 2, -1), out var blueprint);

    Assert.IsTrue(created);
    Assert.IsTrue(blueprint.Blocks.Any(b => b.Offset == new Vector3Int(-1, 0, -1)));
    Assert.IsTrue(blueprint.Blocks.Any(b => b.Offset == new Vector3Int(2, 0, 2)));
}
```

`BoxHeightIncludesElevatedBlockTest` は期待オフセット `(0, 5, 0)` を「チェスト(0,0,0)と(2,5,2)の外形中心 (1,0,1)」基準の `(1, 5, 1)` に直し、原点側チェストは `(-1, 0, -1)` で assert する。

- [ ] **Step 4: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Blueprint"`
Expected: ErrorCount 0 / 全PASS（`BlueprintMachineRecipeSelectionTest`・`BlueprintFilterSplitterSettingsTest` も通る）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Blueprint moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintCreateServiceTest.cs
git commit -m "feat(blueprint): コピー対象規則を共有化しアンカーをブロック外形のXZ中心・最下段へ変える"
```

### Task 2: サーバー共有: BP外形寸法の計算（ドラッグ列の間隔）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintFootprintCalculator.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPlacementElementUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPasteCalculator.cs:14-19`（マスタに無いGUIDのスキップへ警告ログ）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintFootprintCalculatorTest.cs`

**Interfaces:**
- Consumes: `BlueprintPasteCalculator.CalculatePlacements(BlueprintJsonObject, Vector3Int, int)`、`BlockPositionInfo(Vector3Int, BlockDirection, Vector3Int)`
- Produces: `public static class BlueprintFootprintCalculator { static Vector3Int CalcSize(BlueprintJsonObject blueprint, int rotationStep); }`（Task 6 の列間隔）、`public static class BlueprintPlacementElementUtil { static BlockPositionInfo ToPositionInfo(BlueprintPlacementElement placement); }`（Task 6 の重なり判定と共用）

- [ ] **Step 1: 実装を書く**

```csharp
// moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPlacementElementUtil.cs
using Core.Master;
using Game.Block.Interface;

namespace Game.Blueprint
{
    /// <summary>
    ///     配置要素をマスタのサイズ込みの占有情報へ変換する唯一の場所（外形計算と重なり判定が共用）
    ///     The single conversion from a placement element to its footprint with the master size (shared by footprint and overlap checks)
    /// </summary>
    public static class BlueprintPlacementElementUtil
    {
        public static BlockPositionInfo ToPositionInfo(BlueprintPlacementElement placement)
        {
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(placement.BlockId).BlockSize;
            return new BlockPositionInfo(placement.Position, placement.Direction, blockSize);
        }
    }
}
```

```csharp
// moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintFootprintCalculator.cs
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Blueprint
{
    /// <summary>
    ///     回転後のBP全ブロックを包む直方体の寸法。ドラッグ列で隣のBPまでの間隔に使う
    ///     Size of the box enclosing every block of a rotated blueprint; used as the stride between copies in a drag run
    /// </summary>
    public static class BlueprintFootprintCalculator
    {
        public static Vector3Int CalcSize(BlueprintJsonObject blueprint, int rotationStep)
        {
            var placements = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, rotationStep);

            // マスタに無いブロックだけのBPは置くものが無い。1セル刻みに畳み理由を残す
            // A blueprint whose blocks all vanished from the master has nothing to place; fold to a one-cell stride and say so
            if (placements.Count == 0)
            {
                Debug.Log($"[BlueprintFootprint] no resolvable blocks in blueprint {blueprint.BlueprintGuid}; stride folded to 1");
                return Vector3Int.one;
            }

            var min = placements[0].Position;
            var max = placements[0].Position;
            foreach (var placement in placements)
            {
                var info = BlueprintPlacementElementUtil.ToPositionInfo(placement);
                min = Vector3Int.Min(min, info.MinPos);
                max = Vector3Int.Max(max, info.MaxPos);
            }

            return max - min + Vector3Int.one;
        }
    }
}
```

`BlueprintPasteCalculator.CalculatePlacements` の `if (blockId == null) continue;` を次にする（無音の縮退禁止。外形計算・範囲計数・サムネイルがこのスキップの上に載る）:

```csharp
if (blockId == null)
{
    Debug.LogWarning($"[BlueprintPaste] block {block.BlockGuidStr} of blueprint {blueprint.BlueprintGuid} is not in the master; skipped");
    continue;
}
```

- [ ] **Step 2: テストを書く**

```csharp
// moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintFootprintCalculatorTest.cs
using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintFootprintCalculatorTest
    {
        [Test]
        public void TwoChestsInLineFootprintTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid.ToString();
            var blueprint = new BlueprintJsonObject("line", new List<BlueprintBlockJsonObject>
            {
                new(new Vector3Int(0, 0, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
                new(new Vector3Int(2, 0, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
            }, Guid.NewGuid());

            // 無回転はX方向3セル、90度回転でZ方向3セル
            // Unrotated spans 3 cells on X; one rotation spans 3 cells on Z
            Assert.AreEqual(new Vector3Int(3, 1, 1), BlueprintFootprintCalculator.CalcSize(blueprint, 0));
            Assert.AreEqual(new Vector3Int(1, 1, 3), BlueprintFootprintCalculator.CalcSize(blueprint, 1));
        }

        [Test]
        public void UnresolvableBlueprintFoldsToOneCellTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blueprint = new BlueprintJsonObject("missing", new List<BlueprintBlockJsonObject>
            {
                new(new Vector3Int(0, 0, 0), Guid.NewGuid().ToString(), (int)BlockDirection.North, new Dictionary<string, string>()),
            }, Guid.NewGuid());

            Assert.AreEqual(Vector3Int.one, BlueprintFootprintCalculator.CalcSize(blueprint, 0));
        }
    }
}
```

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Tests.CombinedTest.Game.BlueprintFootprintCalculatorTest"`
Expected: PASS 2件

- [ ] **Step 4: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintFootprintCalculator.cs moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPlacementElementUtil.cs moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintPasteCalculator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/BlueprintFootprintCalculatorTest.cs
git commit -m "feat(blueprint): 回転後のBP外形寸法を計算する BlueprintFootprintCalculator を追加"
```

### Task 3: クライアント共通: 1x1セル解決・列位置計算・Q/E入力の抽出

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs:56,67,107-160`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlacementUnitCellResolver.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/Run/PlacementRunPositionCalculator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlacePointCalculator.cs:30-110`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PlacementHeightKeyInput.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceDragState.cs:36-42`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs:95-110`（対象変更で高さを地表へ戻す）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlaceSystemStateControllerHeightResetTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementUnitCellResolverTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementRunPositionCalculatorTest.cs`

**Interfaces:**
- Consumes: `PlaceSystemUtil.CalcPlacePoint(BlockMasterElement, Vector3, int, BlockDirection, PreviewSurfaceType?, float)`、`GroundHeightQuantization.StepOf(Collider)`、`PlacementHeightOffset.Adjust(int)`
- Produces:
  - `public static Vector3Int PlaceSystemUtil.CalcPlacePointBySize(Vector3Int rotatedSize, Vector3 hitPoint, int heightOffset, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep)`（既存 `CalcPlacePoint(BlockMasterElement, …)` はこれへ委譲）
  - `public static bool PlaceSystemUtil.TryRaycastPlacementSurface(Camera, out RaycastHit, out BlockPreviewBoundingBoxSurface)`（private→public）
  - `public static class PlacementUnitCellResolver { static bool TryGetCursorCell(Camera mainCamera, int heightOffset, out Vector3Int cell); static Vector3Int ResolveCell(Vector3 hitPoint, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep, int heightOffset); }`
  - `public readonly struct PlacementRunPositions { IReadOnlyList<Vector3Int> Positions; PlacementRunAxis Axis; int CursorIndex; }`、`public static class PlacementRunPositionCalculator { static PlacementRunPositions Calculate(Vector3Int startPoint, Vector3Int endPoint, Vector3Int stepSize); }`
  - `public static class PlacementHeightKeyInput { static void Apply(PlacementHeightOffset heightOffset); }`

- [ ] **Step 1: `PlaceSystemUtil` を分解する（`TryRaycastPlacementSurface` を public、サイズ版の本体を追加）**

`CalcPlacePoint(BlockMasterElement holdingBlock, Vector3 hitPoint, int heightOffset, BlockDirection currentBlockDirection, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep)` の本体を次の2メソッドにする（既存の面別switch・コメントはそのまま `CalcPlacePointBySize` へ移す）:

```csharp
public static Vector3Int CalcPlacePoint(BlockMasterElement holdingBlock, Vector3 hitPoint, int heightOffset, BlockDirection currentBlockDirection, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep)
{
    var rotateAction = currentBlockDirection.GetCoordinateConvertAction();
    var rotatedSize = rotateAction(holdingBlock.BlockSize).Abs();
    return CalcPlacePointBySize(rotatedSize, hitPoint, heightOffset, surfaceType, groundHeightQuantizationStep);
}

// 回転済みサイズで設置セルを決める本体。1x1のBPコピー/貼り付けもここを通り、スナップ規約が1本になる
// The body that resolves the placement cell from an already-rotated size; 1x1 blueprint copy/paste goes through here too, so there is one snapping rule
public static Vector3Int CalcPlacePointBySize(Vector3Int rotatedSize, Vector3 hitPoint, int heightOffset, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep)
{
    // ここに既存の本体（surfaceType==null の地面分岐、SnapParallel*、面別switch、heightOffset加算）をそのまま置く
}
```

`private static bool TryRaycastPlacementSurface(...)` を `public static` にする。`SnapHitPointToCell` のコメントを「列車車両の距離判定専用。設置セル解決には使わない（BPは `PlacementUnitCellResolver`）」へ直す。ファイルが200行に近いため、`TryGetRaySpecifiedComponentHit<T>` と `TryGetRaySpecifiedComponentHitPosition<T>` の2メソッドを `Util/PlaceSystemRaycastUtil.cs`（`public static class PlaceSystemRaycastUtil`）へ移し、呼び出し側（`grep -rn "TryGetRaySpecifiedComponentHit" moorestech_client/Assets/Scripts` の全件）を `PlaceSystemRaycastUtil.` へ置換する。

- [ ] **Step 2: 1x1セル解決を書く**

```csharp
// moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlacementUnitCellResolver.cs
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    ///     カーソルが指す1x1セルを通常設置と同じ規約で決める。BPコピーの始点/終点と貼り付けアンカーの唯一の解決口
    ///     Resolves the 1x1 cell under the cursor with the normal placement rule; the single resolver for blueprint copy endpoints and the paste anchor
    /// </summary>
    public static class PlacementUnitCellResolver
    {
        private static readonly Vector3Int UnitSize = Vector3Int.one;

        public static bool TryGetCursorCell(Camera mainCamera, int heightOffset, out Vector3Int cell)
        {
            cell = Vector3Int.zero;
            if (!PlaceSystemUtil.TryRaycastPlacementSurface(mainCamera, out var hit, out var surface)) return false;

            // 地面ヒットだけ地形の高さ格子1段を渡す（通常設置と同じ）
            // Only a ground hit passes the terrain height lattice step (same as normal placement)
            var groundHeightQuantizationStep = surface == null ? GroundHeightQuantization.StepOf(hit.collider) : 0f;
            var surfaceType = surface == null ? (PreviewSurfaceType?)null : surface.PreviewSurfaceType;
            cell = ResolveCell(hit.point, surfaceType, groundHeightQuantizationStep, heightOffset);
            return true;
        }

        public static Vector3Int ResolveCell(Vector3 hitPoint, PreviewSurfaceType? surfaceType, float groundHeightQuantizationStep, int heightOffset)
        {
            return PlaceSystemUtil.CalcPlacePointBySize(UnitSize, hitPoint, heightOffset, surfaceType, groundHeightQuantizationStep);
        }
    }
}
```

- [ ] **Step 3: 列位置計算を純関数へ抽出する**

```csharp
// moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/Run/PlacementRunPositionCalculator.cs
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run
{
    /// <summary>
    ///     ドラッグ列の位置列。ブロック列もBP列も同じ骨格を使う
    ///     The positions of one drag run; block runs and blueprint runs share this skeleton
    /// </summary>
    public readonly struct PlacementRunPositions
    {
        public readonly IReadOnlyList<Vector3Int> Positions;
        public readonly PlacementRunAxis Axis;
        public readonly int CursorIndex;

        public PlacementRunPositions(IReadOnlyList<Vector3Int> positions, PlacementRunAxis axis, int cursorIndex)
        {
            Positions = positions;
            Axis = axis;
            CursorIndex = cursorIndex;
        }
    }

    public static class PlacementRunPositionCalculator
    {
        // 最も距離が長い軸へ、stepSizeのその軸成分の刻みで伸ばす
        // Extend along the longest axis, stepping by that axis component of stepSize
        public static PlacementRunPositions Calculate(Vector3Int startPoint, Vector3Int endPoint, Vector3Int stepSize)
        {
            var positions = new List<Vector3Int> { startPoint };
            var current = startPoint;
            var deltaX = Mathf.Abs(endPoint.x - startPoint.x);
            var deltaY = Mathf.Abs(endPoint.y - startPoint.y);
            var deltaZ = Mathf.Abs(endPoint.z - startPoint.z);

            PlacementRunAxis axis;
            if (deltaX >= deltaY && deltaX >= deltaZ)
            {
                axis = PlacementRunAxis.X;
                var direction = endPoint.x > startPoint.x ? 1 : -1;
                while (Mathf.Abs(current.x - endPoint.x) >= stepSize.x) { current.x += stepSize.x * direction; positions.Add(current); }
            }
            else if (deltaZ >= deltaX && deltaZ >= deltaY)
            {
                axis = PlacementRunAxis.Z;
                var direction = endPoint.z > startPoint.z ? 1 : -1;
                while (Mathf.Abs(current.z - endPoint.z) >= stepSize.z) { current.z += stepSize.z * direction; positions.Add(current); }
            }
            else
            {
                axis = PlacementRunAxis.Y;
                var direction = endPoint.y > startPoint.y ? 1 : -1;
                while (Mathf.Abs(current.y - endPoint.y) >= stepSize.y) { current.y += stepSize.y * direction; positions.Add(current); }
            }

            return new PlacementRunPositions(positions, axis, ResolveCursorIndex(positions, endPoint));

            #region Internal

            // 終点は刻み幅で割り切れないと列に載らないため、一致が無ければ末尾セルを充てる
            // The end point is not on the run when the step does not divide it, so the last cell stands in
            static int ResolveCursorIndex(List<Vector3Int> runPositions, Vector3Int cursor)
            {
                for (var i = 0; i < runPositions.Count; i++)
                {
                    if (runPositions[i] == cursor) return i;
                }

                return runPositions.Count - 1;
            }

            #endregion
        }
    }
}
```

`CommonBlockPlacePointCalculator.CalculateRun` のローカル関数 `CalcPositions` と `ResolveCursorIndex` を削除し、本体を次にする（`CalcPlaceCells` は残す）:

```csharp
public static PlacementRun CalculateRun(Vector3Int startPoint, Vector3Int endPoint, BlockDirection blockDirection, BlockMasterElement holdingBlockMasterElement)
{
    var runPositions = PlacementRunPositionCalculator.Calculate(startPoint, endPoint, holdingBlockMasterElement.BlockSize);
    List<PlaceInfo> cells = CalcPlaceCells(runPositions.Positions);

    var blockCauses = new List<PlacementBlockCause>(cells.Count);
    for (var i = 0; i < cells.Count; i++) blockCauses.Add(PlacementBlockCause.None);

    return new PlacementRun(cells, blockCauses, runPositions.Axis, runPositions.CursorIndex);

    #region Internal
    // CalcPlaceCells は引数型を IReadOnlyList<Vector3Int> にして既存本体のまま
    #endregion
}
```

- [ ] **Step 4: Q/E入力の解釈を共通化する**

```csharp
// moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PlacementHeightKeyInput.cs
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     Q/Eを設置高さの±1へ解釈する唯一の場所。通常設置・BPコピー・BP貼り付けが共用する
    ///     The single place that reads Q/E as ±1 placement height; shared by normal placement, blueprint copy and paste
    /// </summary>
    public static class PlacementHeightKeyInput
    {
        public static void Apply(PlacementHeightOffset heightOffset)
        {
            if (HybridInput.GetKeyDown(KeyCode.Q)) heightOffset.Adjust(-1); //TODO InputManagerに移す
            else if (HybridInput.GetKeyDown(KeyCode.E)) heightOffset.Adjust(1);
        }
    }
}
```

`CommonBlockPlaceDragState.UpdateHeightOffsetByInput()` の本体を `PlacementHeightKeyInput.Apply(_heightOffset);` に置き換える（`AdjustHeightOffset` はそのまま）。

`PlaceSystemStateController.ManualUpdate` の `var isSelectionChanged = ...; _lastTarget = CurrentTarget;` の直後に次を足す（高さを0へ戻す書き手を設置系へ散らさず、ここ1本にする。既存の「高さを持たない系へ移ったら戻す」はそのまま）:

```csharp
// 設置対象が変わったら高さは地表基準へ戻す（ブロックの持ち替えもBPの持ち替えも同じ）。設置系側はResetToGroundを呼ばない
// A target change returns the height to ground level (swapping blocks or blueprints alike); place systems never call ResetToGround themselves
if (isSelectionChanged) _placementHeightOffset.ResetToGround();
```

```csharp
// moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlaceSystemStateControllerHeightResetTest.cs
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Game.PlacementTarget;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    // 対象の持ち替えで高さが地表へ戻ること（書き手はコントローラ1本）
    // A target swap returns the height to ground (the controller is the single writer)
    public class PlaceSystemStateControllerHeightResetTest
    {
        [Test]
        public void 対象が変わると高さが0へ戻る()
        {
            var heightOffset = new PlacementHeightOffset();
            var placeSystem = new HeightPlaceSystem();
            var controller = new PlaceSystemStateController(new SingleSelector(placeSystem), new NullPresenter(), heightOffset);

            controller.SetTarget(new BlueprintPlacementTarget(Guid.NewGuid(), "a"), PlacementOrigin.NonHotbar);
            controller.ManualUpdate();
            heightOffset.Adjust(2);
            controller.ManualUpdate();
            Assert.AreEqual(2, heightOffset.Value);

            controller.SetTarget(new BlueprintPlacementTarget(Guid.NewGuid(), "b"), PlacementOrigin.NonHotbar);
            controller.ManualUpdate();
            Assert.AreEqual(0, heightOffset.Value);
        }

        private class HeightPlaceSystem : IPlaceSystem
        {
            public bool OwnsWheelInput => false;
            public bool UsesPlacementHeight => true;
            public void Enable() { }
            public void ManualUpdate(PlaceSystemUpdateContext context) { }
            public void Disable() { }
            public bool TryCancelInProgressOperation() => false;
        }

        private class SingleSelector : IPlaceSystemSelector
        {
            private readonly IPlaceSystem _placeSystem;
            public SingleSelector(IPlaceSystem placeSystem) { _placeSystem = placeSystem; }
            public IPlaceSystem EmptyPlaceSystem { get; } = new Client.Game.InGame.BlockSystem.PlaceSystem.Empty.EmptyPlaceSystem();
            public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context) => _placeSystem;
        }

        private class NullPresenter : IPlacementFeedbackPresenter
        {
            public void Present(PlacementFeedback feedback) { }
            public void Hide() { }
        }
    }
}
```

（`PlacementOrigin.NonHotbar` と `BlueprintPlacementTarget(Guid, string)` は既存。`SetTarget` は public）

- [ ] **Step 5: テストを書く**

```csharp
// moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementUnitCellResolverTest.cs
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    // BPコピー/貼り付けのセルが通常1x1設置と同じ規約で決まること（実測: 側面ヒットのYが1段浮いていた）
    // Blueprint copy/paste cells follow the normal 1x1 rule (measured: a side-face hit used to float one cell up)
    public class PlacementUnitCellResolverTest
    {
        [Test]
        public void 東面ヒットは外側セルでYは浮かない()
        {
            // 実測 probeD: チェスト(2,32,2)の東面ヒット (3.07,32.55,2.25)
            var cell = PlacementUnitCellResolver.ResolveCell(new Vector3(3.07f, 32.55f, 2.25f), PreviewSurfaceType.YZ_X, 0f, 0);
            Assert.AreEqual(new Vector3Int(3, 32, 2), cell);
        }

        [Test]
        public void 天面ヒットは上のセル()
        {
            var cell = PlacementUnitCellResolver.ResolveCell(new Vector3(2.63f, 33.07f, 2.09f), PreviewSurfaceType.XZ_Y, 0f, 0);
            Assert.AreEqual(new Vector3Int(2, 33, 2), cell);
        }

        [Test]
        public void 地面ヒットに高さオフセットが乗る()
        {
            var cell = PlacementUnitCellResolver.ResolveCell(new Vector3(0.5f, 32f, 0.5f), null, 0f, 2);
            Assert.AreEqual(new Vector3Int(0, 34, 0), cell);
        }
    }
}
```

```csharp
// moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementRunPositionCalculatorTest.cs
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class PlacementRunPositionCalculatorTest
    {
        [Test]
        public void X軸へ刻み幅で伸びる()
        {
            var run = PlacementRunPositionCalculator.Calculate(new Vector3Int(6, 32, 6), new Vector3Int(10, 32, 6), new Vector3Int(1, 1, 1));
            Assert.AreEqual(5, run.Positions.Count);
            Assert.AreEqual(PlacementRunAxis.X, run.Axis);
            Assert.AreEqual(4, run.CursorIndex);
        }

        [Test]
        public void 刻み幅3では割り切れない終点を末尾で代替する()
        {
            var run = PlacementRunPositionCalculator.Calculate(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 7), new Vector3Int(3, 1, 3));
            Assert.AreEqual(3, run.Positions.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 6), run.Positions[2]);
            Assert.AreEqual(2, run.CursorIndex);
        }

        [Test]
        public void 始点と終点が同じなら1点()
        {
            var run = PlacementRunPositionCalculator.Calculate(new Vector3Int(1, 1, 1), new Vector3Int(1, 1, 1), new Vector3Int(2, 2, 2));
            Assert.AreEqual(1, run.Positions.Count);
            Assert.AreEqual(0, run.CursorIndex);
        }
    }
}
```

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementUnitCellResolverTest|PlacementRunPositionCalculatorTest|PlaceSystemUtilCalcPlacePointTest|CommonBlockPlace|PlaceSystemStateController"`
Expected: ErrorCount 0 / 全PASS（既存の `PlaceSystemUtilCalcPlacePointTest` が委譲後も通る）

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem
git commit -m "refactor(place): 1x1セル解決・列位置計算・Q/E入力を共通化し、持ち替え時の高さ復帰をコントローラ1本にする"
```

### Task 4: 辞書・ツールチップ行・Create失敗の可視化

**Files:**
- Modify: `Localization/localization.csv`（末尾へ3行）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopyFeedbackLines.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/ClientBlueprintLibrary.cs:63-74`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/BlueprintCreateResult.cs`
- Modify: `moorestech_web/webui/src/features/notification/notificationMessages.ts`（1行追加）
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`pnpm gen:i18n` の生成結果）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintCopyFeedbackLinesTest.cs`

**Interfaces:**
- Produces:
  - 辞書キー `LocalizationKeys.Ui.Tooltip.BlueprintCopyBlocksInRange`（{p0}）、`LocalizationKeys.Ui.Tooltip.BlueprintCopyEmptyRange`、`LocalizationKeys.Ui.Notification.BlueprintCreateFailed`（{p0}）
  - `public static class BlueprintCopyFeedbackLines { static void ReportBlocksInRange(int count, PlacementFeedback feedback); }`
  - `public enum BlueprintCreateFailure { None, RequestFailed, NotUnlocked, InvalidName, EmptyArea, InvalidRequest, Unknown }`、`public readonly struct BlueprintCreateResult { BlueprintCreateFailure Failure; Guid BlueprintGuid; bool Success => Failure == None; }`（判別子は `Failure` 1本。`Success` は派生）
  - `bool ClientBlueprintLibrary.TryGetBlueprint(Guid blueprintGuid, out BlueprintJsonObject blueprint)`（Task 6/7 が共用。見つからなければ理由をログ）、`UniTask<BlueprintCreateResult> ClientBlueprintLibrary.CreateBlueprint(string name, Vector3Int min, Vector3Int max, CancellationToken ct)`（前例: 同ファイルの `BlueprintDeleteResult`）

- [ ] **Step 1: 辞書行を足す（`Localization/localization.csv` 末尾）**

```csv
ui.tooltip.blueprintCopyBlocksInRange,Blocks in range: {p0},Blocks in range: {p0},範囲内のブロック: {p0},Blöcke im Bereich: {p0},범위 내 블록: {p0}
ui.tooltip.blueprintCopyEmptyRange,No blocks in range,No blocks in range,範囲にブロックがありません,Keine Blöcke im Bereich,범위에 블록이 없습니다
ui.notification.blueprintCreateFailed,Blueprint was not created: {p0},Blueprint was not created: {p0},ブループリントを作成できませんでした: {p0},Blaupause wurde nicht erstellt: {p0},블루프린트를 만들지 못했습니다: {p0}
```

- [ ] **Step 2: ツールチップ行を書く**

```csharp
// moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopyFeedbackLines.cs
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     BPコピー固有のツールチップ行。範囲内ブロック数は常時、0個のときだけ空範囲の理由を足す
    ///     Blueprint-copy-specific tooltip lines: the in-range count always, plus the empty-range reason only when it is zero
    /// </summary>
    public static class BlueprintCopyFeedbackLines
    {
        public static void ReportBlocksInRange(int count, PlacementFeedback feedback)
        {
            feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.BlueprintCopyBlocksInRange, new[] { count.ToString() }));
            if (count == 0) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.BlueprintCopyEmptyRange));
        }
    }
}
```

- [ ] **Step 3: Create の結果型と失敗の可視化**

```csharp
// moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/BlueprintCreateResult.cs
using System;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    // 作成結果でサーバー拒否の理由と通信失敗（null応答）を区別する（BlueprintDeleteResultと同型）
    // Distinguishes each server rejection from a request failure (null reply), same shape as BlueprintDeleteResult
    public enum BlueprintCreateFailure
    {
        None,
        RequestFailed,
        NotUnlocked,
        InvalidName,
        EmptyArea,
        InvalidRequest,
        Unknown,
    }

    public readonly struct BlueprintCreateResult
    {
        // 判別子はFailure1本。成功はFailure==Noneの派生で、BlueprintGuidは成功時だけ意味を持つ
        // Failure is the single discriminator; Success derives from it and BlueprintGuid only means something on success
        public readonly BlueprintCreateFailure Failure;
        public readonly Guid BlueprintGuid;
        public bool Success => Failure == BlueprintCreateFailure.None;

        private BlueprintCreateResult(BlueprintCreateFailure failure, Guid blueprintGuid)
        {
            Failure = failure;
            BlueprintGuid = blueprintGuid;
        }

        public static BlueprintCreateResult Succeeded(Guid blueprintGuid) => new(BlueprintCreateFailure.None, blueprintGuid);
        public static BlueprintCreateResult RequestFailed() => new(BlueprintCreateFailure.RequestFailed, Guid.Empty);

        public static BlueprintCreateResult Rejected(BlueprintFailureReason reason)
        {
            var failure = reason switch
            {
                BlueprintFailureReason.NotUnlocked => BlueprintCreateFailure.NotUnlocked,
                BlueprintFailureReason.InvalidName => BlueprintCreateFailure.InvalidName,
                BlueprintFailureReason.EmptyArea => BlueprintCreateFailure.EmptyArea,
                BlueprintFailureReason.InvalidRequest => BlueprintCreateFailure.InvalidRequest,
                _ => BlueprintCreateFailure.Unknown,
            };
            return new BlueprintCreateResult(failure, Guid.Empty);
        }
    }
}
```

`ClientBlueprintLibrary.CreateBlueprint` を次にする:

```csharp
public async UniTask<BlueprintCreateResult> CreateBlueprint(string name, Vector3Int min, Vector3Int max, CancellationToken ct)
{
    var request = BlueprintRequest.CreateCreateRequest(name, min, max);
    var response = await ClientContext.VanillaApi.Response.Block.SendBlueprintRequest(request, ct);

    // タイムアウト等のnull応答は失敗扱い
    // Treat a null response (timeout etc.) as failure
    if (response == null) return BlueprintCreateResult.RequestFailed();

    ApplyResponse(response);
    return response.Success
        ? BlueprintCreateResult.Succeeded(Guid.Parse(response.RegisteredGuidStr))
        : BlueprintCreateResult.Rejected(response.FailureReason);
}
```

`ClientBlueprintLibrary` に次を足す（Task 6 の解決と Task 7 の撮影が共用。見つからない＝削除済み/未同期をログに残す）:

```csharp
public bool TryGetBlueprint(Guid blueprintGuid, out BlueprintJsonObject blueprint)
{
    foreach (var pack in _blueprints)
    {
        if (pack.BlueprintGuid != blueprintGuid) continue;
        blueprint = pack.ToJsonObject();
        return true;
    }

    Debug.Log($"[ClientBlueprintLibrary] blueprint {blueprintGuid} is not in the cache (deleted or not yet synced)");
    blueprint = null;
    return false;
}
```

`notificationMessages.ts` の `denied.*` 表へ `["denied.blueprint.CreateFailed", L.ui.notification.blueprintCreateFailed],` を足す（通知の発火は Task 5 のコピー系が `NotificationMessagePack.CreateOperationDenied("denied.blueprint.CreateFailed", new[] { reason.ToString() })` で行う）。

- [ ] **Step 4: テストを書く**

```csharp
// moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintCopyFeedbackLinesTest.cs
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintCopyFeedbackLinesTest
    {
        [Test]
        public void 範囲内が1以上なら件数行だけ()
        {
            var feedback = new PlacementFeedback();
            BlueprintCopyFeedbackLines.ReportBlocksInRange(3, feedback);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.BlueprintCopyBlocksInRange.Key, feedback.Lines[0].Key.Key);
            Assert.AreEqual("3", feedback.Lines[0].TextParams[0]);
        }

        [Test]
        public void 範囲内が0なら空範囲の理由が増える()
        {
            var feedback = new PlacementFeedback();
            BlueprintCopyFeedbackLines.ReportBlocksInRange(0, feedback);
            Assert.AreEqual(2, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.BlueprintCopyEmptyRange.Key, feedback.Lines[1].Key.Key);
        }
    }
}
```

- [ ] **Step 5: 生成・コンパイル・テスト**

Run: `cd moorestech_web/webui && pnpm gen:i18n && pnpm test` → `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.PlaceSystem.BlueprintCopyFeedbackLinesTest"`
Expected: vitest 全PASS（`notificationServerIdCoverage.test.ts` は Task 5 で発火文字列を書くまで `denied.blueprint.CreateFailed` 未使用を指摘し得る。その場合は Task 5 完了後に再実行して PASS を確認）/ ErrorCount 0 / PASS 2件

- [ ] **Step 6: コミットする**

```bash
git add Localization/localization.csv moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint moorestech_web/webui/src/features/notification/notificationMessages.ts moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintCopyFeedbackLinesTest.cs
git commit -m "feat(blueprint): 範囲内ブロック数のツールチップ行とCreate失敗理由の型・辞書を追加"
```

### Task 5: コピー: 1クリック始点/終点・Q/E高さ・マーカー表示・範囲内計数

**Files:**
- Move: `Blueprint/BlueprintCopySystem.cs` → `Blueprint/Copy/BlueprintCopySystem.cs`（`git mv` で .cs と .meta）、`Blueprint/BlueprintAreaVisualizer.cs` → `Blueprint/Copy/BlueprintCopyRangeVisualizer.cs`（書き換え）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopySelection.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopyRangeCounter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopyClickInput.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemSelector.cs`（using を `.Blueprint.Copy`/`.Blueprint.Paste` に）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs:106-107`（using 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs` 内の `BlueprintCopySystem` 登録はそのまま（ctor 依存は VContainer が解決）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintCopySelectionTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintCopyRangeCounterTest.cs`

（名前空間は `Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy`。`BlueprintAreaVisualizer` を参照していた旧録画シナリオは Task 8 で置き換える）

**Interfaces:**
- Consumes: Task 1 `BlueprintCopyTargetRule`、Task 3 `PlacementUnitCellResolver.TryGetCursorCell`・`PlacementHeightKeyInput.Apply`、Task 4 `BlueprintCopyFeedbackLines`・`BlueprintCreateResult`、`BlueprintNameInputState.Open/Close/OnConfirm/OnCancel`、`PlacementHeightOffset.Value/ResetToGround`、`BlockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary`
- Produces:
  - `public enum BlueprintCopyPhase { SelectingStart, SelectingEnd, AwaitingName, Creating }`
  - `public class BlueprintCopySelection { BlueprintCopyPhase Phase; Vector3Int StartCell; Vector3Int EndCell; void SelectStart(Vector3Int); void SelectEnd(Vector3Int); void ReturnToEndSelection(); void BeginCreate(); void Clear(); static (Vector3Int min, Vector3Int max) CalcBox(Vector3Int a, Vector3Int b); }`（`Creating` は送信中。次の始点を受け付けない）
  - `public static class BlueprintCopyRangeCounter { static int Count(IEnumerable<(BlockMasterElement master, BlockPositionInfo position)> blocks, Vector3Int min, Vector3Int max); }`
  - `public class BlueprintCopyClickInput { bool TryConsumeClick(); }`（押下を登録してから解放で1回だけ true。ビルドメニュー選択クリックの解放漏れを無視する）
  - `public class BlueprintCopyRangeVisualizer { void ShowSelectingStart(Vector3Int hoverCell); void ShowSelectingEnd(Vector3Int startCell, Vector3Int hoverCell, Vector3Int min, Vector3Int max); void ShowAwaitingName(Vector3Int startCell, Vector3Int endCell, Vector3Int min, Vector3Int max); void ShowStartOnly(Vector3Int startCell); void HideAll(); }`（GameObject名: `BlueprintCopyStartMarker` / `BlueprintCopyEndMarker` / `BlueprintCopyRangeBox`）

- [ ] **Step 1: 選択状態（純ロジック）を書く**

```csharp
// Blueprint/Copy/BlueprintCopySelection.cs
using System;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    public enum BlueprintCopyPhase
    {
        SelectingStart,
        SelectingEnd,
        AwaitingName,
        Creating,
    }

    /// <summary>
    ///     コピー範囲の選択状態。局面と2セルを1値に畳み、片方だけ残る状態を作れなくする
    ///     The copy-range selection; phase and the two cells live in one value so a half-cleared state is unrepresentable
    /// </summary>
    public class BlueprintCopySelection
    {
        public BlueprintCopyPhase Phase { get; private set; } = BlueprintCopyPhase.SelectingStart;
        public Vector3Int StartCell { get; private set; }
        public Vector3Int EndCell { get; private set; }

        public void SelectStart(Vector3Int cell)
        {
            if (Phase != BlueprintCopyPhase.SelectingStart) throw new InvalidOperationException($"SelectStart in {Phase}");
            StartCell = cell;
            Phase = BlueprintCopyPhase.SelectingEnd;
        }

        public void SelectEnd(Vector3Int cell)
        {
            if (Phase != BlueprintCopyPhase.SelectingEnd) throw new InvalidOperationException($"SelectEnd in {Phase}");
            EndCell = cell;
            Phase = BlueprintCopyPhase.AwaitingName;
        }

        // 名前入力のキャンセルは始点を保ったまま終点選択へ戻る（ユーザー裁定）
        // Cancelling the name input keeps the start and returns to end selection (user ruling)
        public void ReturnToEndSelection()
        {
            if (Phase != BlueprintCopyPhase.AwaitingName) throw new InvalidOperationException($"ReturnToEndSelection in {Phase}");
            Phase = BlueprintCopyPhase.SelectingEnd;
        }

        // 名前確定で送信中へ。応答が返るまで次の始点を受け付けない（ADR 0076）
        // Confirming the name enters Creating; no new start is accepted until the reply arrives (ADR 0076)
        public void BeginCreate()
        {
            if (Phase != BlueprintCopyPhase.AwaitingName) throw new InvalidOperationException($"BeginCreate in {Phase}");
            Phase = BlueprintCopyPhase.Creating;
        }

        public void Clear()
        {
            Phase = BlueprintCopyPhase.SelectingStart;
        }

        public static (Vector3Int min, Vector3Int max) CalcBox(Vector3Int a, Vector3Int b)
        {
            return (Vector3Int.Min(a, b), Vector3Int.Max(a, b));
        }
    }
}
```

- [ ] **Step 2: 範囲内計数とクリック入力を書く**

```csharp
// Blueprint/Copy/BlueprintCopyRangeCounter.cs
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Blueprint;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     範囲内のコピー対象ブロック数。サーバーの抽出規則（BlueprintCopyTargetRule）と同じ答えを出す
    ///     The number of copy targets inside the range, using the server's own rule (BlueprintCopyTargetRule)
    /// </summary>
    public static class BlueprintCopyRangeCounter
    {
        public static int Count(IEnumerable<(BlockMasterElement master, BlockPositionInfo position)> blocks, Vector3Int min, Vector3Int max)
        {
            var count = 0;
            foreach (var (master, position) in blocks)
            {
                if (!BlueprintCopyTargetRule.IsCopyTarget(master)) continue;
                if (!BlueprintCopyTargetRule.IntersectsBox(position, min, max)) continue;
                count++;
            }

            return count;
        }
    }
}
```

```csharp
// Blueprint/Copy/BlueprintCopyClickInput.cs
using Client.Game.InGame.Control;
using Client.Input;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     UI外で押下を登録し、解放で1クリックとして消費する。押下未登録の解放（メニュー選択クリックの漏れ）は無視する
    ///     Registers a press outside UI and consumes the release as one click; a release without a registered press (a leaked menu click) is ignored
    /// </summary>
    public class BlueprintCopyClickInput
    {
        private bool _isPressRegistered;

        public bool TryConsumeClick()
        {
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _isPressRegistered = true;
            if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return false;

            var isClick = _isPressRegistered && !UiPointerHitTest.IsPointerOverAnyUi();
            _isPressRegistered = false;
            return isClick;
        }

        public void Reset()
        {
            _isPressRegistered = false;
        }
    }
}
```

- [ ] **Step 3: マーカーと範囲の表示を書く（`BlueprintAreaVisualizer.cs` を `git mv` して全文置換）**

```csharp
// Blueprint/Copy/BlueprintCopyRangeVisualizer.cs
using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     始点（赤）・終点（緑）の1x1半透明キューブと、範囲の半透明直方体を表示する
    ///     Shows the red start / green end 1x1 translucent cubes and the translucent range box
    /// </summary>
    public class BlueprintCopyRangeVisualizer
    {
        private static readonly Color EndMarkerColor = new(0.3f, 0.85f, 0.35f, 1f);

        private readonly GameObject _startMarker;
        private readonly GameObject _endMarker;
        private readonly GameObject _rangeBox;

        public BlueprintCopyRangeVisualizer()
        {
            _startMarker = CreateCube("BlueprintCopyStartMarker", MaterialConst.NotPlaceableColor);
            _endMarker = CreateCube("BlueprintCopyEndMarker", EndMarkerColor);
            _rangeBox = CreateCube("BlueprintCopyRangeBox", MaterialConst.PlaceableColor);
        }

        // 始点未確定: 赤マーカーがカーソルセルに追従する
        // Before the start is set, the red marker follows the cursor cell
        public void ShowSelectingStart(Vector3Int hoverCell)
        {
            PlaceCell(_startMarker, hoverCell);
            _endMarker.SetActive(false);
            _rangeBox.SetActive(false);
        }

        public void ShowSelectingEnd(Vector3Int startCell, Vector3Int hoverCell, Vector3Int min, Vector3Int max)
        {
            PlaceCell(_startMarker, startCell);
            PlaceCell(_endMarker, hoverCell);
            PlaceBox(min, max);
        }

        public void ShowAwaitingName(Vector3Int startCell, Vector3Int endCell, Vector3Int min, Vector3Int max)
        {
            ShowSelectingEnd(startCell, endCell, min, max);
        }

        // カーソルが何にも当たらないフレーム: 確定済みの始点だけ残す
        // On a frame with no cursor hit, keep only the confirmed start
        public void ShowStartOnly(Vector3Int startCell)
        {
            PlaceCell(_startMarker, startCell);
            _endMarker.SetActive(false);
            _rangeBox.SetActive(false);
        }

        public void HideAll()
        {
            _startMarker.SetActive(false);
            _endMarker.SetActive(false);
            _rangeBox.SetActive(false);
        }

        private static void PlaceCell(GameObject cube, Vector3Int cell)
        {
            cube.transform.position = cell + new Vector3(0.5f, 0.5f, 0.5f);
            cube.transform.localScale = Vector3.one;
            cube.SetActive(true);
        }

        private void PlaceBox(Vector3Int min, Vector3Int max)
        {
            // セル境界から中心とサイズを算出（各セル1x1x1前提）
            // Center and scale from cell bounds; each cell is 1x1x1
            var size = new Vector3(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1);
            _rangeBox.transform.position = new Vector3(min.x, min.y, min.z) + size * 0.5f;
            _rangeBox.transform.localScale = size;
            _rangeBox.SetActive(true);
        }

        private static GameObject CreateCube(string name, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Object.Destroy(cube.GetComponent<Collider>());

            // 設置プレビュー材質を複製して色を適用
            // Clone the shared placement preview material and tint it
            var material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial());
            material.SetColor(MaterialConst.PreviewColorPropertyName, color);
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            cube.SetActive(false);
            return cube;
        }
    }
}
```

- [ ] **Step 4: コピー系本体を書き換える（`git mv` 後に全文置換）**

```csharp
// Blueprint/Copy/BlueprintCopySystem.cs
using System.Linq;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Blueprint;
using Cysharp.Threading.Tasks;
using Server.Event.Notification;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     ・1クリックで始点、もう1クリックで終点（セルは通常1x1設置と同じ解決＋Q/E）
    ///     ・名前入力後にCreate送信。キャンセルは終点選択へ戻る
    ///     Picks the copy box with one click for the start and one for the end (cells resolved like a 1x1 block plus Q/E), then sends Create after naming; cancel returns to end selection
    /// </summary>
    public class BlueprintCopySystem : PlaceSystemBase<BlueprintCopyPlacementTarget>
    {
        private readonly ClientBlueprintLibrary _library;
        private readonly BlueprintNameInputState _nameInputState;
        private readonly PlacementHeightOffset _heightOffset;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly Camera _mainCamera;
        private readonly BlueprintCopySelection _selection = new();
        private readonly BlueprintCopyClickInput _clickInput = new();
        private readonly CompositeDisposable _subscriptions = new();
        private BlueprintCopyRangeVisualizer _visualizer;

        // Q/Eで動かす設置高さを読む系
        // A system that reads the placement height moved by Q/E
        public override bool UsesPlacementHeight => true;

        public BlueprintCopySystem(Camera mainCamera, ClientBlueprintLibrary library, BlueprintNameInputState nameInputState, PlacementHeightOffset heightOffset, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _mainCamera = mainCamera;
            _library = library;
            _nameInputState = nameInputState;
            _heightOffset = heightOffset;
            _blockGameObjectDataStore = blockGameObjectDataStore;

            // Enable毎の重複購読を避けるため購読はコンストラクタで1回だけ行う
            // Subscribe once in the constructor to avoid duplicate subscriptions on repeated Enable
            _nameInputState.OnConfirm.Subscribe(name => CreateAndReset(name).Forget()).AddTo(_subscriptions);
            _nameInputState.OnCancel.Subscribe(_ => { if (_selection.Phase == BlueprintCopyPhase.AwaitingName) _selection.ReturnToEndSelection(); }).AddTo(_subscriptions);
        }

        public override void Enable()
        {
            _visualizer ??= new BlueprintCopyRangeVisualizer();
            _selection.Clear();
            _clickInput.Reset();
        }

        protected override void ManualUpdate(BlueprintCopyPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            // 送信中は入力も表示も止める（応答で選択が畳まれる）
            // While sending, take no input and show nothing (the reply folds the selection)
            if (_selection.Phase == BlueprintCopyPhase.Creating)
            {
                _visualizer.HideAll();
                return;
            }

            // 名前入力中は選択を凍結し表示だけ保つ
            // While the name dialog is open, freeze the selection and keep the visuals
            if (_selection.Phase == BlueprintCopyPhase.AwaitingName)
            {
                var (nameMin, nameMax) = BlueprintCopySelection.CalcBox(_selection.StartCell, _selection.EndCell);
                _visualizer.ShowAwaitingName(_selection.StartCell, _selection.EndCell, nameMin, nameMax);
                return;
            }

            PlacementHeightKeyInput.Apply(_heightOffset);
            var isClicked = _clickInput.TryConsumeClick();

            if (!PlacementUnitCellResolver.TryGetCursorCell(_mainCamera, _heightOffset.Value, out var cursorCell))
            {
                if (_selection.Phase == BlueprintCopyPhase.SelectingEnd) _visualizer.ShowStartOnly(_selection.StartCell);
                else _visualizer.HideAll();
                return;
            }

            if (_selection.Phase == BlueprintCopyPhase.SelectingStart)
            {
                _visualizer.ShowSelectingStart(cursorCell);
                if (isClicked) _selection.SelectStart(cursorCell);
                return;
            }

            var (min, max) = BlueprintCopySelection.CalcBox(_selection.StartCell, cursorCell);
            var count = BlueprintCopyRangeCounter.Count(EnumerateBlocks(), min, max);
            BlueprintCopyFeedbackLines.ReportBlocksInRange(count, feedback);
            _visualizer.ShowSelectingEnd(_selection.StartCell, cursorCell, min, max);

            if (!isClicked) return;
            if (count == 0)
            {
                // 空範囲は確定させない。理由はツールチップに出ているがログにも残す（無音の縮退禁止）
                // An empty range never confirms; the tooltip already says why, and the log keeps a trace too (no silent fold)
                Debug.Log($"[BlueprintCopy] end click refused: no copy targets in {min}-{max}");
                return;
            }

            _selection.SelectEnd(cursorCell);
            _nameInputState.Open();

            #region Internal

            System.Collections.Generic.IEnumerable<(Mooresmaster.Model.BlocksModule.BlockMasterElement, Game.Block.Interface.BlockPositionInfo)> EnumerateBlocks()
            {
                return _blockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary.Values.Select(block => (block.BlockMasterElement, block.BlockPosInfo));
            }

            #endregion
        }

        public override void Disable()
        {
            _selection.Clear();
            _clickInput.Reset();
            _visualizer?.HideAll();
            _nameInputState.Close();
        }

        // 右短押し/Esc: 終点選択中は始点を捨て、名前入力中はモーダルを畳んで終点選択へ戻る。未選択なら建築モード離脱へ譲る
        // Right short press / Esc: drop the start while selecting the end, fold the modal back to end selection while naming, otherwise yield to leaving build mode
        public override bool TryCancelInProgressOperation()
        {
            switch (_selection.Phase)
            {
                case BlueprintCopyPhase.SelectingEnd:
                    _selection.Clear();
                    _visualizer?.HideAll();
                    return true;
                case BlueprintCopyPhase.AwaitingName:
                    _nameInputState.Close();
                    _selection.ReturnToEndSelection();
                    return true;
                default:
                    return false;
            }
        }

        private async UniTaskVoid CreateAndReset(string name)
        {
            var (min, max) = BlueprintCopySelection.CalcBox(_selection.StartCell, _selection.EndCell);
            _selection.BeginCreate();

            var result = await _library.CreateBlueprint(name, min, max, CancellationToken.None);

            // 応答後に畳む。Disableで既にClear済みなら二重に畳まない
            // Fold after the reply; if Disable already cleared it, do not fold twice
            if (_selection.Phase == BlueprintCopyPhase.Creating) _selection.Clear();
            if (result.Success) return;

            // サーバー拒否・通信失敗はログと通知の両方へ（最終防衛。通常は範囲内0をクライアントで弾く）
            // A server rejection or request failure goes to both the log and a notification (last line of defense; an empty range is normally refused client-side)
            Debug.LogError($"[BlueprintCopy] create rejected: {result.Failure} box={min}-{max} name={name}");
            ClientDIContext.ClientLocalNotificationSource.Notify(NotificationMessagePack.CreateOperationDenied("denied.blueprint.CreateFailed", new[] { result.Failure.ToString() }));
        }
    }
}
```

- [ ] **Step 5: テストを書く**

```csharp
// moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintCopySelectionTest.cs
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintCopySelectionTest
    {
        [Test]
        public void 始点_終点_名前入力_キャンセルで終点選択へ戻る()
        {
            var selection = new BlueprintCopySelection();
            Assert.AreEqual(BlueprintCopyPhase.SelectingStart, selection.Phase);

            selection.SelectStart(new Vector3Int(4, 32, 4));
            Assert.AreEqual(BlueprintCopyPhase.SelectingEnd, selection.Phase);

            selection.SelectEnd(new Vector3Int(0, 33, 0));
            Assert.AreEqual(BlueprintCopyPhase.AwaitingName, selection.Phase);

            selection.ReturnToEndSelection();
            Assert.AreEqual(BlueprintCopyPhase.SelectingEnd, selection.Phase);
            Assert.AreEqual(new Vector3Int(4, 32, 4), selection.StartCell);

            selection.SelectEnd(new Vector3Int(0, 33, 0));
            selection.BeginCreate();
            Assert.AreEqual(BlueprintCopyPhase.Creating, selection.Phase);
            selection.Clear();
            Assert.AreEqual(BlueprintCopyPhase.SelectingStart, selection.Phase);
        }

        [Test]
        public void 範囲は2セルの成分ごとのmin_max()
        {
            var (min, max) = BlueprintCopySelection.CalcBox(new Vector3Int(4, 32, 0), new Vector3Int(0, 33, 4));
            Assert.AreEqual(new Vector3Int(0, 32, 0), min);
            Assert.AreEqual(new Vector3Int(4, 33, 4), max);
        }

        [Test]
        public void 局面外の遷移は例外()
        {
            var selection = new BlueprintCopySelection();
            Assert.Throws<InvalidOperationException>(() => selection.SelectEnd(Vector3Int.zero));
            Assert.Throws<InvalidOperationException>(() => selection.ReturnToEndSelection());
            Assert.Throws<InvalidOperationException>(() => selection.BeginCreate());
        }
    }
}
```

```csharp
// moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintCopyRangeCounterTest.cs
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintCopyRangeCounterTest
    {
        private static BlockMasterElement MakeBlock(string blockType, Vector3Int size)
        {
            return new BlockMasterElement(0, Guid.Empty, "TestBlock", blockType, null, 1, null, "テスト", "テスト", 0, false, size, null, false, null);
        }

        [Test]
        public void 一部でも交差すれば数え_レール系は数えない()
        {
            var chest = MakeBlock("Chest", Vector3Int.one);
            var kiln = MakeBlock("ElectricMachine", new Vector3Int(3, 2, 3));
            var rail = MakeBlock(BlockMasterElement.BlockTypeConst.TrainRail, Vector3Int.one);
            var blocks = new List<(BlockMasterElement, BlockPositionInfo)>
            {
                (chest, new BlockPositionInfo(new Vector3Int(2, 32, 2), BlockDirection.North, Vector3Int.one)),
                (kiln, new BlockPositionInfo(new Vector3Int(10, 32, 2), BlockDirection.North, new Vector3Int(3, 2, 3))),
                (chest, new BlockPositionInfo(new Vector3Int(100, 32, 100), BlockDirection.North, Vector3Int.one)),
                (rail, new BlockPositionInfo(new Vector3Int(3, 32, 3), BlockDirection.North, Vector3Int.one)),
            };

            // 範囲(0..11, 32, 0..4): チェストと石窯の角だけ入る。レールは除外
            // Box (0..11, 32, 0..4): the chest and one corner of the kiln; the rail is excluded
            Assert.AreEqual(2, BlueprintCopyRangeCounter.Count(blocks, new Vector3Int(0, 32, 0), new Vector3Int(11, 32, 4)));

            // 1段上の範囲: 石窯（高さ2）だけ
            // One cell up: only the kiln (height 2)
            Assert.AreEqual(1, BlueprintCopyRangeCounter.Count(blocks, new Vector3Int(0, 33, 0), new Vector3Int(11, 33, 4)));
            Assert.AreEqual(0, BlueprintCopyRangeCounter.Count(blocks, new Vector3Int(3, 32, 2), new Vector3Int(4, 32, 4)));
        }
    }
}
```

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintCopy|PlaceSystemStateController|RightShortPress"`
Expected: ErrorCount 0 / 全PASS。`cd moorestech_web/webui && pnpm test` で `notificationServerIdCoverage.test.ts` を含め全PASS

- [ ] **Step 7: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemSelector.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem
git commit -m "feat(blueprint): コピー範囲を1クリック始点/終点とQ/Eで決め、赤緑マーカーと範囲内ブロック数を出す"
```

### Task 6: 貼り付け: Q/E高さ・1x1アンカー・ドラッグ列設置

**Files:**
- Move: `Blueprint/BlueprintPasteSystem.cs`・`Blueprint/BlueprintPastePreviewController.cs`・`Blueprint/BlueprintPasteOverlapReasonReporter.cs` → `Blueprint/Paste/`（`git mv`、名前空間 `...Blueprint.Paste`）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteDragState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Paste/BlueprintPasteRunBuilder.cs`
- Modify: `Blueprint/Paste/BlueprintPasteSystem.cs`（全文）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintPasteRunBuilderTest.cs`

**Interfaces:**
- Consumes: Task 2 `BlueprintFootprintCalculator.CalcSize`、Task 3 `PlacementUnitCellResolver.TryGetCursorCell`・`PlacementRunPositionCalculator.Calculate`・`PlacementHeightKeyInput.Apply`、`PlacementDragSession`、`PlacementHeightOffset.Restore`
- Produces:
  - `public class BlueprintPasteDragState { bool IsDragging; void BeginDrag(Vector3Int startAnchor, int startHeightOffset); Vector3Int ResolveStartAnchor(Vector3Int cursorAnchor); bool EndDrag(); void ClearDrag(); }`
  - `public static class BlueprintPasteRunBuilder { static List<BlueprintPlacementElement> Build(BlueprintJsonObject blueprint, Vector3Int startAnchor, Vector3Int cursorAnchor, Vector3Int footprintSize, int rotationStep); }`

- [ ] **Step 1: ドラッグ状態を書く**

```csharp
// Blueprint/Paste/BlueprintPasteDragState.cs
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     貼り付けのドラッグセッション。終了時に高さを開始値へ戻す（通常設置と同じ規則）
    ///     The paste drag session; ending it restores the starting height (same rule as normal placement)
    /// </summary>
    public class BlueprintPasteDragState
    {
        public bool IsDragging => _session != null;

        private readonly PlacementHeightOffset _heightOffset;
        private PlacementDragSession _session;

        public BlueprintPasteDragState(PlacementHeightOffset heightOffset)
        {
            _heightOffset = heightOffset;
        }

        public void BeginDrag(Vector3Int startAnchor, int startHeightOffset)
        {
            _session = new PlacementDragSession(startAnchor, PlacementHitSurfaceKind.Ground, startHeightOffset);
        }

        public Vector3Int ResolveStartAnchor(Vector3Int cursorAnchor)
        {
            return _session == null ? cursorAnchor : _session.StartCell;
        }

        // 押下未登録の解放は無視する（ビルドメニュー選択クリックの解放漏れ対策）。戻り値は押下が登録されていたか
        // A release without a registered press is ignored (a leaked build-menu click); returns whether a press was registered
        public bool EndDrag()
        {
            if (_session == null) return false;

            ClearDrag();
            return true;
        }

        public void ClearDrag()
        {
            if (_session == null) return;

            _heightOffset.Restore(_session.StartHeightOffset);
            _session = null;
        }
    }
}
```

- [ ] **Step 2: 列の組み立てを書く**

```csharp
// Blueprint/Paste/BlueprintPasteRunBuilder.cs
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Game.Blueprint;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     ドラッグ列の各アンカーへBPを展開し、全要素を1列に並べる。間隔はBP外形
    ///     Expands the blueprint at every anchor of a drag run into one flat element list; the stride is the blueprint footprint
    /// </summary>
    public static class BlueprintPasteRunBuilder
    {
        public static List<BlueprintPlacementElement> Build(BlueprintJsonObject blueprint, Vector3Int startAnchor, Vector3Int cursorAnchor, Vector3Int footprintSize, int rotationStep)
        {
            var run = PlacementRunPositionCalculator.Calculate(startAnchor, cursorAnchor, footprintSize);
            var elements = new List<BlueprintPlacementElement>();
            foreach (var anchor in run.Positions)
            {
                elements.AddRange(BlueprintPasteCalculator.CalculatePlacements(blueprint, anchor, rotationStep));
            }

            return elements;
        }
    }
}
```

- [ ] **Step 3: 貼り付け系本体を書き換える（全文）**

```csharp
// Blueprint/Paste/BlueprintPasteSystem.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Input;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     BPを回転・Q/E高さ・ドラッグ列で貼り付ける設置系。アンカーは通常1x1設置と同じセル解決
    ///     Pastes a blueprint with rotation, Q/E height and a drag run; the anchor uses the normal 1x1 cell rule
    /// </summary>
    public class BlueprintPasteSystem : PlaceSystemBase<BlueprintPlacementTarget>
    {
        private readonly ClientBlueprintLibrary _library;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly PlacementHeightOffset _heightOffset;
        private readonly Camera _mainCamera;
        private readonly BlueprintPasteDragState _dragState;
        private BlueprintPastePreviewController _previewController;

        private BlueprintJsonObject _currentBlueprint;
        private int _rotationStep;
        private Vector3Int _footprintSize = Vector3Int.one;

        // 手編集セーブ等でSettingsがnullのブロックを空設定として扱うための共有インスタンス
        // Shared instance so blocks whose Settings is null (e.g. hand-edited saves) paste as settings-less
        private static readonly Dictionary<string, string> EmptySettings = new();

        public override bool UsesPlacementHeight => true;

        public BlueprintPasteSystem(Camera mainCamera, ClientBlueprintLibrary library, BlockGameObjectDataStore blockGameObjectDataStore, PlacementHeightOffset heightOffset)
        {
            _mainCamera = mainCamera;
            _library = library;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _heightOffset = heightOffset;
            _dragState = new BlueprintPasteDragState(heightOffset);
        }

        public override void Enable()
        {
            _rotationStep = 0;
            _previewController ??= new BlueprintPastePreviewController(new GameObject("BlueprintPastePreview").transform);
        }

        protected override void ManualUpdate(BlueprintPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            if (isSelectionChanged) { ResolveBlueprint(target.BlueprintGuid); _dragState.ClearDrag(); }
            if (_currentBlueprint == null)
            {
                // 未解決BP（キャッシュに無い等）は前回のゴーストを残さない
                // Hide stale ghosts when the blueprint could not be resolved (e.g. not cached)
                _previewController.Hide();
                return;
            }

            PlacementHeightKeyInput.Apply(_heightOffset);
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown) Rotate();

            if (!PlacementUnitCellResolver.TryGetCursorCell(_mainCamera, _heightOffset.Value, out var cursorAnchor))
            {
                _previewController.Hide();
                return;
            }

            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi()) _dragState.BeginDrag(cursorAnchor, _heightOffset.Value);

            // 距離外なら理由のみ出しゴースト無し。解放は畳む
            // Beyond range, show only the reason and no ghost; a release still folds the drag
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(cursorAnchor))
            {
                _previewController.Hide();
                feedback.AddTooFar();
                if (InputManager.Playable.ScreenLeftClick.GetKeyUp) _dragState.EndDrag();
                return;
            }

            var placements = BlueprintPasteRunBuilder.Build(_currentBlueprint, _dragState.ResolveStartAnchor(cursorAnchor), cursorAnchor, _footprintSize, _rotationStep);
            var placeableFlags = placements.Select(IsPlaceable).ToList();
            _previewController.UpdatePreview(placements, placeableFlags);
            BlueprintPasteOverlapReasonReporter.Report(placeableFlags, feedback);

            // 解放で設置可能セルのみ送信（サーバーは部分成功を許す）
            // Release sends placeable cells only; the server allows partial success
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp && _dragState.EndDrag() && !UiPointerHitTest.IsPointerOverAnyUi()) SendPlace(placements, placeableFlags);

            #region Internal

            void ResolveBlueprint(Guid blueprintGuid)
            {
                // 見つからない理由（削除済み/未同期）はライブラリ側がログに残す
                // The library logs why a lookup misses (deleted or not yet synced)
                _currentBlueprint = _library.TryGetBlueprint(blueprintGuid, out var blueprint) ? blueprint : null;
                if (_currentBlueprint != null) _footprintSize = BlueprintFootprintCalculator.CalcSize(_currentBlueprint, _rotationStep);
            }

            void Rotate()
            {
                _rotationStep = (_rotationStep + 1) % 4;
                _footprintSize = BlueprintFootprintCalculator.CalcSize(_currentBlueprint, _rotationStep);
            }

            bool IsPlaceable(BlueprintPlacementElement placement)
            {
                return !_blockGameObjectDataStore.IsOverlapPositionInfo(BlueprintPlacementElementUtil.ToPositionInfo(placement));
            }

            void SendPlace(List<BlueprintPlacementElement> allPlacements, List<bool> flags)
            {
                var placeInfos = new List<PlaceInfo>();
                for (var i = 0; i < allPlacements.Count; i++)
                {
                    if (flags[i]) placeInfos.Add(ToPlaceInfo(allPlacements[i]));
                }

                PlaceBlockProtocolSender.SendPlaceBlockProtocol(placeInfos);
            }

            PlaceInfo ToPlaceInfo(BlueprintPlacementElement placement)
            {
                var createParams = (placement.Settings ?? EmptySettings)
                    .Select(kvp => new BlockCreateParam(kvp.Key, Encoding.UTF8.GetBytes(kvp.Value)))
                    .ToArray();

                return new PlaceInfo
                {
                    Position = placement.Position,
                    Direction = placement.Direction,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = placement.BlockId,
                    Placeable = true,
                    CreateParams = createParams,
                };
            }

            #endregion
        }

        // 右短押し/Escはドラッグだけを畳み建築モードに留まる
        // A right short press or Esc folds only the drag and stays in build mode
        public override bool TryCancelInProgressOperation()
        {
            if (!_dragState.IsDragging) return false;

            _dragState.ClearDrag();
            return true;
        }

        public override void Disable()
        {
            _dragState.ClearDrag();
            _previewController?.Hide();
            _currentBlueprint = null;
        }
    }
}
```

- [ ] **Step 4: テストを書く**

```csharp
// moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintPasteRunBuilderTest.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintPasteRunBuilderTest
    {
        [Test]
        public void 外形幅の刻みでアンカーが並ぶ()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid.ToString();
            var blueprint = new BlueprintJsonObject("pair", new List<BlueprintBlockJsonObject>
            {
                new(new Vector3Int(0, 0, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
                new(new Vector3Int(1, 0, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
            }, Guid.NewGuid());

            // 外形(2,1,1)で(0,0,0)→(5,0,0): アンカー0,2,4 → 6要素
            // Footprint (2,1,1) from (0,0,0) to (5,0,0): anchors 0,2,4 → 6 elements
            var elements = BlueprintPasteRunBuilder.Build(blueprint, new Vector3Int(0, 0, 0), new Vector3Int(5, 0, 0), new Vector3Int(2, 1, 1), 0);
            Assert.AreEqual(6, elements.Count);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5 }, elements.Select(e => e.Position.x).ToArray());
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Tests/Tests.asmdef` の `references` に `Game.Blueprint` を追加する（`Server.Boot`・`Server.Tests.Module` は参照済み）。

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintPaste|PlaceSystemStateController"`
Expected: ErrorCount 0 / 全PASS

- [ ] **Step 6: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(blueprint): 貼り付けにQ/E高さとドラッグ列設置を足し、アンカーを1x1設置と同じセル解決にする"
```

### Task 7: サムネイル: 撮影・保持・配信・DTO・Web契約

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientContext.cs`（`BlockIconImagePhotographer` プロパティ・ctor引数追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs`（`IBlueprintThumbnailLookup BlueprintThumbnailLookup` を解決して静的公開）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:154`（DontDestroyOnLoad・隔離位置への退避・ClientContext 引数）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockIconImagePhotographer.cs:57-61`（複製の配置を `localPosition` に。撮影器を隔離位置へ置けるようにする）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Thumbnail/IBlueprintThumbnailLookup.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Thumbnail/BlueprintThumbnailContainer.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Thumbnail/BlueprintThumbnailSyncPlanner.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Thumbnail/BlueprintThumbnailSubjectBuilder.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Thumbnail/BlueprintThumbnailRenderer.cs`（`Photograph` は撮れないとき null を返し `Sync` が登録を飛ばす。null は「撮れなかった」の1意味だけで、ログは `TryBuild` が出す）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs`（`builder.Register<BlueprintThumbnailContainer>(Lifetime.Singleton).AsSelf().As<IBlueprintThumbnailLookup>();` と `builder.RegisterEntryPoint<BlueprintThumbnailRenderer>();` を `RegisterPlacement` 末尾へ）
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Icons/BlueprintIconSource.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Icons/IconEndpoint.cs:19-26`（`_sources` へ追加）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs:143-156`（`ResolveIconUrl`）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs:48`付近（購読追加）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Hotbar/HotbarTopic.cs:57`付近（購読追加）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/hotbar.ts:21-27`（blueprint枠 `iconUrl: z.string().optional()`）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.test.ts`（BPエントリに iconUrl がある fixture を受理するケース追加）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintThumbnailSyncPlannerTest.cs`

**Interfaces:**
- Consumes: `BlockIconImagePhotographer.TakeIconImages(List<(GameObject prefab, string debugName)>)`、`ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(BlockId, Vector3, Quaternion)`、`SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition`、`BlueprintPasteCalculator.CalculatePlacements`、`ClientBlueprintLibrary.Blueprints/OnChanged`
- Produces:
  - `public interface IBlueprintThumbnailLookup { IObservable<Guid> OnThumbnailChanged; bool Contains(Guid); bool TryGet(Guid, out Texture2D); IReadOnlyCollection<Guid> Guids; }`（読み取り面。配信・DTO・トピックはこれだけを見る）
  - `public class BlueprintThumbnailContainer : IBlueprintThumbnailLookup { void Add(Guid, Texture2D); void Remove(Guid); }`（書き手は `BlueprintThumbnailRenderer` だけ。DI注入のみで static 公開しない）
  - `ClientDIContext.BlueprintThumbnailLookup`（static get、`IBlueprintThumbnailLookup`）、`ClientContext.BlockIconImagePhotographer`（static get）
  - `public readonly struct BlueprintThumbnailSyncPlan { IReadOnlyList<Guid> ToRender; IReadOnlyList<Guid> ToRemove; }`、`public static class BlueprintThumbnailSyncPlanner { static BlueprintThumbnailSyncPlan Plan(IReadOnlyList<Guid> libraryGuids, IReadOnlyCollection<Guid> cachedGuids); }`
  - `public static class BlueprintThumbnailSubjectBuilder { static bool TryBuild(BlueprintJsonObject blueprint, Transform parent, out GameObject subject); }`（解決できた配置が0件なら false＋`Debug.LogError`。被写体は撮影器の直下にローカル原点で組む）
  - `public class BlueprintThumbnailRenderer : IInitializable, IDisposable`
  - `public class BlueprintIconSource : IIconTextureSource { const string PathPrefixConst = "/api/blueprint-icons/"; }`

- [ ] **Step 1: 保持コンテナと ClientContext**

```csharp
// Blueprint/Thumbnail/IBlueprintThumbnailLookup.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail
{
    /// <summary>
    ///     サムネイルの読み取り面。配信・DTO・トピックはここだけを見る（書き手は撮影側のみ）
    ///     The read side of thumbnails; delivery, DTOs and topics see only this (the photographer side is the sole writer)
    /// </summary>
    public interface IBlueprintThumbnailLookup
    {
        IObservable<Guid> OnThumbnailChanged { get; }
        IReadOnlyCollection<Guid> Guids { get; }
        bool Contains(Guid blueprintGuid);
        bool TryGet(Guid blueprintGuid, out Texture2D thumbnail);
    }
}
```

```csharp
// Blueprint/Thumbnail/BlueprintThumbnailContainer.cs
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail
{
    /// <summary>
    ///     保存済みBPのサムネイルをGuidキーで保持する。未撮影のGuidは持たない
    ///     Holds saved-blueprint thumbnails by GUID; GUIDs not yet photographed are absent
    /// </summary>
    public class BlueprintThumbnailContainer : IBlueprintThumbnailLookup
    {
        private readonly Dictionary<Guid, Texture2D> _thumbnails = new();
        private readonly Subject<Guid> _onThumbnailChanged = new();

        public IObservable<Guid> OnThumbnailChanged => _onThumbnailChanged;
        public IReadOnlyCollection<Guid> Guids => _thumbnails.Keys;

        public bool Contains(Guid blueprintGuid) => _thumbnails.ContainsKey(blueprintGuid);
        public bool TryGet(Guid blueprintGuid, out Texture2D thumbnail) => _thumbnails.TryGetValue(blueprintGuid, out thumbnail);

        public void Add(Guid blueprintGuid, Texture2D thumbnail)
        {
            _thumbnails[blueprintGuid] = thumbnail;
            _onThumbnailChanged.OnNext(blueprintGuid);
        }

        public void Remove(Guid blueprintGuid)
        {
            if (!_thumbnails.Remove(blueprintGuid)) return;
            _onThumbnailChanged.OnNext(blueprintGuid);
        }
    }
}
```

`BlockIconImagePhotographer.GetIcon` の `captureTarget.transform.position = Vector3.zero;` を `captureTarget.transform.localPosition = Vector3.zero;` に変える（初期化シーンでは撮影器が原点にあるため結果は同じ。主シーンでは隔離位置に従う。カメラ位置は複製のレンダラー境界から求めるため変更不要）。

`ClientContext` に `public static BlockIconImagePhotographer BlockIconImagePhotographer { get; private set; }` を追加し、ctor 末尾に `BlockIconImagePhotographer blockIconImagePhotographer` を足して代入する（`using Client.Game.InGame.Block;`）。`ClientDIContext` に `public static IBlueprintThumbnailLookup BlueprintThumbnailLookup { get; private set; }` を追加し、ctor で `diContainer.DIContainerResolver.Resolve<IBlueprintThumbnailLookup>()` を代入する（前例: 同ctorの `BuildOperationHistory`）。

`InitializeScenePipeline` の `new ClientContext(...)` 直前に次を置く:

```csharp
// 撮影器を主シーンへ持ち越し、地形や既設ブロックが写り込まない隔離位置へ退避する（被写体と複製は撮影器直下のローカル原点に置かれる）
// Carry the photographer into the main scene and park it where terrain and placed blocks cannot appear (subjects and clones sit at its local origin)
DontDestroyOnLoad(blockIconImagePhotographer.gameObject);
blockIconImagePhotographer.transform.position = new Vector3(0f, -5000f, 0f);
new ClientContext(assetResult.BlockGameObjectPrefabContainer, assetResult.ItemImageContainer, assetResult.BlockImageContainer, assetResult.TrainCarImageContainer, assetResult.ConnectToolImageContainer, assetResult.FluidImageContainer, serverResult.PlayerConnectionSetting, serverResult.VanillaApi, blockIconImagePhotographer);
```

- [ ] **Step 2: 同期計画（純ロジック）と撮影対象の組み立て**

```csharp
// Blueprint/Thumbnail/BlueprintThumbnailSyncPlanner.cs
using System;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail
{
    public readonly struct BlueprintThumbnailSyncPlan
    {
        public readonly IReadOnlyList<Guid> ToRender;
        public readonly IReadOnlyList<Guid> ToRemove;

        public BlueprintThumbnailSyncPlan(IReadOnlyList<Guid> toRender, IReadOnlyList<Guid> toRemove)
        {
            ToRender = toRender;
            ToRemove = toRemove;
        }
    }

    /// <summary>
    ///     ライブラリとキャッシュの差分から「撮るGuid」「捨てるGuid」を決める
    ///     Decides which GUIDs to photograph and which to drop from the library/cache difference
    /// </summary>
    public static class BlueprintThumbnailSyncPlanner
    {
        public static BlueprintThumbnailSyncPlan Plan(IReadOnlyList<Guid> libraryGuids, IReadOnlyCollection<Guid> cachedGuids)
        {
            var library = new HashSet<Guid>(libraryGuids);
            var toRender = new List<Guid>();
            foreach (var guid in libraryGuids)
            {
                if (!cachedGuids.Contains(guid)) toRender.Add(guid);
            }

            var toRemove = new List<Guid>();
            foreach (var guid in cachedGuids)
            {
                if (!library.Contains(guid)) toRemove.Add(guid);
            }

            return new BlueprintThumbnailSyncPlan(toRender, toRemove);
        }
    }
}
```

```csharp
// Blueprint/Thumbnail/BlueprintThumbnailSubjectBuilder.cs
using Client.Game.InGame.Context;
using Game.Block.Interface;
using Game.Blueprint;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail
{
    /// <summary>
    ///     BPのブロックプレファブをオフセット通りに1つの親へ並べ、撮影器へ渡す被写体を作る（呼び出し側が撮影後に破棄する）
    ///     Lays the blueprint's block prefabs out under one parent as the photographer's subject (the caller destroys it after the shot)
    /// </summary>
    public static class BlueprintThumbnailSubjectBuilder
    {
        public static bool TryBuild(BlueprintJsonObject blueprint, Transform parent, out GameObject subject)
        {
            var placements = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, 0);

            // 全ブロックがマスタから消えたBPは被写体が空で撮影器が例外を投げるため、撮らずに理由を残す（以後のBPを止めない）
            // A blueprint whose blocks all vanished from the master has an empty subject and the photographer throws, so skip it with a reason (never stall later blueprints)
            if (placements.Count == 0)
            {
                Debug.LogError($"[BlueprintThumbnail] blueprint {blueprint.BlueprintGuid} ({blueprint.Name}) has no resolvable blocks; thumbnail skipped");
                subject = null;
                return false;
            }

            subject = new GameObject($"BlueprintThumbnailSubject:{blueprint.Name}");
            subject.transform.SetParent(parent, false);

            // 実設置と同じ座標変換（グリッド原点→モデル原点）で、撮影器直下のローカル座標に並べる
            // Lay out with the same grid-to-model-origin conversion as real placement, in the photographer's local space
            foreach (var placement in placements)
            {
                // プレファブ欠損は被写体から外して続ける（例外で撮影が止まると以降のBPが全て未撮影のまま固まる）
                // A missing prefab is left out and the shot continues (an exception here would stall every later thumbnail)
                if (!ClientContext.BlockGameObjectPrefabContainer.BlockPrefabInfos.ContainsKey(placement.BlockId))
                {
                    Debug.LogError($"[BlueprintThumbnail] prefab missing for block {placement.BlockId.AsPrimitive()} in blueprint {blueprint.BlueprintGuid}; skipped");
                    continue;
                }

                var position = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placement.Position, placement.Direction, placement.BlockId);
                var block = ClientContext.BlockGameObjectPrefabContainer.CreateBlockGameObject(placement.BlockId, Vector3.zero, placement.Direction.GetRotation());
                block.transform.SetParent(subject.transform, false);
                block.transform.localPosition = position;
                block.SetActive(true);
            }

            return true;
        }
    }
}
```

- [ ] **Step 3: 撮影の駆動**

```csharp
// Blueprint/Thumbnail/BlueprintThumbnailRenderer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail
{
    /// <summary>
    ///     ライブラリ更新を購読し、未撮影のBPを順に撮影してコンテナへ入れる（表示専用オブザーバ）
    ///     Subscribes to library updates and photographs blueprints that have no thumbnail yet, in order (display-only observer)
    /// </summary>
    public class BlueprintThumbnailRenderer : IInitializable, IDisposable
    {
        private readonly ClientBlueprintLibrary _library;
        private readonly BlueprintThumbnailContainer _container;
        private readonly CompositeDisposable _subscriptions = new();
        private bool _isRendering;
        private bool _isRerunRequested;

        public BlueprintThumbnailRenderer(ClientBlueprintLibrary library, BlueprintThumbnailContainer container)
        {
            _library = library;
            _container = container;
        }

        public void Initialize()
        {
            _library.OnChanged.Subscribe(_ => Sync().Forget()).AddTo(_subscriptions);
            Sync().Forget();
        }

        public void Dispose() => _subscriptions.Dispose();

        private async UniTaskVoid Sync()
        {
            // 撮影中に来た更新は1回にまとめて撮り直す
            // Updates arriving mid-shoot fold into one rerun afterwards
            if (_isRendering) { _isRerunRequested = true; return; }
            _isRendering = true;

            var plan = BlueprintThumbnailSyncPlanner.Plan(_library.Blueprints.Select(b => b.BlueprintGuid).ToList(), _container.Guids.ToList());
            foreach (var guid in plan.ToRemove) _container.Remove(guid);
            foreach (var guid in plan.ToRender)
            {
                if (!_library.TryGetBlueprint(guid, out var blueprint)) continue;
                var thumbnail = await Photograph(blueprint);

                // 撮れなかったBPは未登録のまま（名前表示に留まる）。理由はTryBuildがログ済み
                // A blueprint that could not be shot stays unregistered (name display); TryBuild already logged why
                if (thumbnail == null) continue;
                _container.Add(guid, thumbnail);
            }

            _isRendering = false;
            if (!_isRerunRequested) return;
            _isRerunRequested = false;
            Sync().Forget();
        }

        private static async UniTask<Texture2D> Photograph(Game.Blueprint.BlueprintJsonObject blueprint)
        {
            // バッチ実行は描画せず代替画像（ModAssetIconLoaderと同じ）
            // Batch runs do not render and use the placeholder (same as ModAssetIconLoader)
            if (Application.isBatchMode) return Texture2D.whiteTexture;

            var photographer = ClientContext.BlockIconImagePhotographer;
            if (!BlueprintThumbnailSubjectBuilder.TryBuild(blueprint, photographer.transform, out var subject)) return null;
            var textures = await photographer.TakeIconImages(new List<(GameObject prefab, string debugName)> { (subject, blueprint.Name) });
            UnityEngine.Object.Destroy(subject);
            return textures[0];
        }
    }
}
```

`MainGameInteractionRegistration.RegisterPlacement` 末尾に `builder.Register<BlueprintThumbnailContainer>(Lifetime.Singleton).AsSelf().As<IBlueprintThumbnailLookup>();` と `builder.RegisterEntryPoint<BlueprintThumbnailRenderer>();`（`using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;`）。

- [ ] **Step 4: 配信ソース・DTO・トピックの再配信**

```csharp
// moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Icons/BlueprintIconSource.cs
using System;
using Client.Game.InGame.Context;
using UnityEngine;

namespace Client.WebUiHost.Game.Icons
{
    /// <summary>
    /// GET /api/blueprint-icons/{guid}.png で保存済みBPのサムネイルを解決する
    /// Resolves saved-blueprint thumbnails served at GET /api/blueprint-icons/{guid}.png
    /// </summary>
    public class BlueprintIconSource : IIconTextureSource
    {
        public const string PathPrefixConst = "/api/blueprint-icons/";

        public string PathPrefix => PathPrefixConst;

        // 主ゲームのDIが組み上がるまで（ClientDIContext未設定）は503で待たせる
        // Until the main game's DI is built (ClientDIContext unset) answer 503
        public bool IsReady => ClientDIContext.BlueprintThumbnailLookup != null;

        public bool IsValidKey(string keyText)
        {
            return Guid.TryParse(keyText, out _);
        }

        public Texture2D ResolveOrNull(string keyText)
        {
            if (!Guid.TryParse(keyText, out var blueprintGuid)) return null;
            return ClientDIContext.BlueprintThumbnailLookup.TryGet(blueprintGuid, out var thumbnail) ? thumbnail : null;
        }
    }
}
```

`IconEndpoint._sources` に `new BlueprintIconSource()` を追加（クラスのsummaryも「Item/Block/TrainCar/ConnectTool/Fluid/Blueprint」に）。

`BuildMenuEntryDtoFactory.ResolveIconUrl` の BP 分岐を次にする:

```csharp
// サムネイル未撮影の間はURLを出さず名前表示のまま。撮影完了はトピック再配信で追従する
// Until the thumbnail is shot there is no URL and the name shows; completion republishes through the topics
case BlueprintPlacementTarget blueprint:
    return ClientDIContext.BlueprintThumbnailLookup.Contains(blueprint.BlueprintGuid)
        ? $"{BlueprintIconSource.PathPrefixConst}{blueprint.BlueprintGuid:D}{IconEndpoint.PathSuffix}"
        : null;
case BlueprintCopyPlacementTarget:
    return null;
```

`BuildMenuTopic` ctor の購読群に `ClientDIContext.BlueprintThumbnailLookup.OnThumbnailChanged.Subscribe(_ => SchedulePublish())`、`HotbarTopic` ctor にも同じ購読を足し、既存の `_subscriptions`/Dispose 経路へ `.AddTo` する（既存フィールド名に合わせる。無ければ `_thumbnailSubscription` を追加して Dispose で破棄）。

Web: `hotbar.ts` の `HotbarBlueprintSlotSchema` を `iconUrl: z.string().optional()` にする（コメントを「サムネイル撮影後だけURLが載る」に）。`buildMenu.test.ts` に `{ kind: "blueprint", id, label: "x", iconUrl: "/api/blueprint-icons/<guid>.png", ... }` が受理されるケースを1つ足す。`hotbar` の既存テスト（`src/bridge/contract/schemas/hotbar.test.ts` があれば）にも同様に1ケース。

- [ ] **Step 5: テストを書く**

```csharp
// moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintThumbnailSyncPlannerTest.cs
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintThumbnailSyncPlannerTest
    {
        [Test]
        public void 未撮影だけ撮り_消えたBPだけ捨てる()
        {
            var a = Guid.NewGuid(); var b = Guid.NewGuid(); var gone = Guid.NewGuid();
            var plan = BlueprintThumbnailSyncPlanner.Plan(new List<Guid> { a, b }, new List<Guid> { a, gone });
            CollectionAssert.AreEqual(new[] { b }, plan.ToRender);
            CollectionAssert.AreEqual(new[] { gone }, plan.ToRemove);
        }

        [Test]
        public void 空ライブラリは全て捨てる_空キャッシュは全て撮る()
        {
            var a = Guid.NewGuid();
            var emptyLibrary = BlueprintThumbnailSyncPlanner.Plan(new List<Guid>(), new List<Guid> { a });
            Assert.AreEqual(0, emptyLibrary.ToRender.Count);
            Assert.AreEqual(1, emptyLibrary.ToRemove.Count);

            var emptyCache = BlueprintThumbnailSyncPlanner.Plan(new List<Guid> { a }, new List<Guid>());
            Assert.AreEqual(1, emptyCache.ToRender.Count);
            Assert.AreEqual(0, emptyCache.ToRemove.Count);
        }
    }
}
```

- [ ] **Step 6: コンパイル・テスト・Webビルド**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintThumbnail|BuildMenu|Hotbar"` → `cd moorestech_web/webui && pnpm test && pnpm build`（`moorestech_web/README.md` の手順で `StreamingAssets/WebUi/dist` を更新）
Expected: ErrorCount 0 / 全PASS / vitest・build 成功

- [ ] **Step 7: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/Context moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockIconImagePhotographer.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlueprintThumbnailSyncPlannerTest.cs moorestech_web/webui/src
git commit -m "feat(blueprint): BPサムネイルをアイコン撮影器で生成し /api/blueprint-icons で配信する"
```

### Task 8: unityプレイ録画テスト: コピー→ESC→作成→Q/E・ドラッグ貼り付け→サムネイル

**Files:**
- Modify（全文置換）: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-copy-paste-via-ui.cs`（旧uGUI版を置換）
- Delete: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-copy-box-probe.cs`（実測プローブ。役目を終える。結果は plan の実測根拠節に転記済み）

**Interfaces:**
- Consumes: Task 5 の GameObject名 `BlueprintCopyStartMarker`/`BlueprintCopyEndMarker`/`BlueprintCopyRangeBox`、`BlueprintNameInputState.Confirm`、`ClientDIContext.BlueprintThumbnailLookup.Contains`、Web testid `build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d`・`build-menu-category-d1000000-0000-4000-8000-000000000009`（ツール）・`build-menu-category-d1000000-0000-4000-8000-000000000010`（BP）・`modal-input`

- [ ] **Step 1: シナリオを書く**

```csharp
// ⚠ 旧uGUI版（BuildMenuView等）を置き換えたWeb UI版。名前入力の文字打ちだけは状態へ直接書く（Webモーダルの文字入力はDSL未対応）
// Web-UI rewrite replacing the old uGUI scenario; only the name text is written to the state directly (DSL cannot type into the web modal)
// 検証: 1クリック始点/終点・E高さ・ESCで終点選択へ戻る・側面ヒットの始点セル・範囲内0の拒否・貼り付けE・ドラッグ列5個・サムネイル
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Blueprint;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Blueprint;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("blueprint-copy-paste-via-ui", options, async p =>
{
    // 背景EditorではInputSystemがデバイスを無効化し注入が届かないため、焦点無視へ切り替え再有効化する（beads moorestech-xsd18.4 (b)）
    // A background Editor disables keyboard/mouse so injection never lands; switch to ignore-focus and re-enable (beads moorestech-xsd18.4 (b))
    var inputSettings = InputSystem.settings;
    inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();
    p.Hotbar.UnlockBlueprint();

    p.PlaceBlockDirect("木のチェスト", new Vector3Int(2, 32, 2), BlockDirection.North);
    p.PlaceBlockDirect("石窯", new Vector3Int(10, 32, 2), BlockDirection.North);
    await p.WaitBlockGameObject(new Vector3Int(2, 32, 2));
    await p.WaitBlockGameObject(new Vector3Int(10, 32, 2));
    p.WarpPlayer(new Vector3(6f, 33.5f, -5f));

    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var nameState = resolver.Resolve<BlueprintNameInputState>();
    var heightOffset = resolver.Resolve<PlacementHeightOffset>();
    var tooltip = resolver.Resolve<MouseCursorTooltipState>();
    var datastore = p.ServerService<IBlueprintDatastore>();

    const string ToolCategory = "build-menu-category-d1000000-0000-4000-8000-000000000009";
    const string CopyToolEntry = "build-menu-entry-blueprintCopy-88dd687d-aceb-4aeb-94e8-44be1e1f5a0d";
    const string BlueprintCategory = "build-menu-category-d1000000-0000-4000-8000-000000000010";

    p.Note("BPコピーツールを選択");
    await SelectEntry(ToolCategory, CopyToolEntry);

    p.Note("始点: 地面(0.5,32,0.5)を1クリック。赤マーカーがセルに乗る");
    await p.AimAt(new Vector3(0.5f, 32f, 0.5f));
    await p.ClickPlace();
    AssertMarker("BlueprintCopyStartMarker", new Vector3Int(0, 32, 0), "始点マーカー(0,32,0)");

    p.Note("Eで終点を1段上げ、地面(4.5,32,4.5)へホバー。範囲は(0,32,0)-(4,33,4)、範囲内1ブロック");
    await p.PressKey(Key.E);
    await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
    await UniTask.DelayFrame(3);
    AssertMarker("BlueprintCopyEndMarker", new Vector3Int(4, 33, 4), "終点マーカー(4,33,4)");
    AssertRangeBox(new Vector3Int(0, 32, 0), new Vector3Int(4, 33, 4), "範囲ボックス");
    p.Assert(TooltipHasParam("1"), "ツールチップに範囲内1ブロック");
    await p.Screenshot("01-selecting-end");

    p.Note("終点クリックで名前入力が開く。Web側キャンセル（nameState.Cancel＝モーダルの閉じる/ESC相当）で終点選択へ戻る（始点は残る）");
    await p.ClickPlace();
    await p.UntilWebUiElement("modal-input", 10f);
    nameState.Cancel();
    await UniTask.DelayFrame(5);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, $"Webキャンセル後もPlaceBlock 実際:{p.CurrentUiState}");
    AssertMarker("BlueprintCopyStartMarker", new Vector3Int(0, 32, 0), "Webキャンセル後も始点マーカーが残る");

    p.Note("もう一度終点クリックし、今度はゲーム側のEscキーで終点選択へ戻る");
    await p.ClickPlace();
    await p.UntilWebUiElement("modal-input", 10f);
    await p.PressKey(Key.Escape);
    await UniTask.DelayFrame(5);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, $"Esc後もPlaceBlock 実際:{p.CurrentUiState}");
    p.Assert(!nameState.IsOpen, "Escで名前入力が閉じる");
    AssertMarker("BlueprintCopyStartMarker", new Vector3Int(0, 32, 0), "Esc後も始点マーカーが残る");
    await p.Screenshot("02-after-escape");

    p.Note("もう一度終点を確定し名前を入れて作成。チェストだけが写りオフセット(0,0,0)");
    await p.PressKey(Key.Q);
    await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
    await p.ClickPlace();
    await p.UntilWebUiElement("modal-input", 10f);
    nameState.Confirm("chest-bp");
    await p.Until(() => datastore.Blueprints.Any(b => b.Name == "chest-bp"), 15f, "BP『chest-bp』が登録される");
    var chestBp = datastore.Blueprints.First(b => b.Name == "chest-bp");
    p.Assert(chestBp.Blocks.Count == 1 && chestBp.Blocks[0].Offset == Vector3Int.zero, $"BPはチェスト1個・オフセット(0,0,0) 実際:{chestBp.Blocks.Count}/{(chestBp.Blocks.Count > 0 ? chestBp.Blocks[0].Offset.ToString() : "-")}");

    p.Note("側面ヒット: チェスト東面(3.0,32.5,2.5)の始点セルは(3,32,2)（旧実装は(3,33,2)）。範囲内0なら確定を拒む");
    await p.AimAt(new Vector3(3.0f, 32.5f, 2.5f));
    await p.ClickPlace();
    AssertMarker("BlueprintCopyStartMarker", new Vector3Int(3, 32, 2), "東面ヒットの始点セル(3,32,2)");
    await p.AimAt(new Vector3(4.5f, 32f, 4.5f));
    await UniTask.DelayFrame(3);
    p.Assert(TooltipHasParam("0"), "範囲内0ブロックの表示");
    await p.ClickPlace();
    await UniTask.DelayFrame(5);
    p.Assert(!nameState.IsOpen, "範囲内0では名前入力が開かない");
    await p.Screenshot("03-empty-range-refused");
    await p.PressKey(Key.Escape);

    p.Note("貼り付け: Eでゴーストが1段上がる");
    await SelectEntry(BlueprintCategory, $"build-menu-entry-blueprint-{chestBp.BlueprintGuid:D}");
    await p.AimAt(new Vector3(6.5f, 32f, 6.5f));
    await p.PressKey(Key.E);
    await UniTask.DelayFrame(3);
    p.Assert(heightOffset.Value == 1, $"貼り付け中のEで高さ1 実際:{heightOffset.Value}");
    p.Assert(ActiveGhostPositions().Any(pos => pos == new Vector3(6f, 33f, 6f)), $"ゴーストが(6,33,6) 実際:{string.Join(";", ActiveGhostPositions())}");
    await p.Screenshot("04-paste-height");
    await p.PressKey(Key.Q);

    p.Note("ドラッグ列: (6.5,32,6.5)→(10.5,32,6.5)で5個");
    await PlaytestUiOps.DragPlace(new Vector3(6.5f, 32f, 6.5f), new Vector3(10.5f, 32f, 6.5f));
    await p.Until(() => Enumerable.Range(6, 5).All(x => p.GetBlock(new Vector3Int(x, 32, 6)) != null), 20f, "x=6..10 に5個置かれる");
    await p.WaitBlockGameObject(new Vector3Int(10, 32, 6));
    await p.Screenshot("05-drag-run-pasted");

    p.Note("サムネイル: コンテナに撮影済みでビルドメニューに画像が出る");
    await p.Until(() => ClientDIContext.BlueprintThumbnailLookup.Contains(chestBp.BlueprintGuid), 20f, "サムネイル撮影完了");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.BuildMenu, 10f);
    await p.ClickWebUi(BlueprintCategory);
    await p.HoverWebUi($"build-menu-entry-blueprint-{chestBp.BlueprintGuid:D}");
    await p.Screenshot("06-thumbnail-in-build-menu");
    await p.CloseWebUiPanel();
    await p.ExitToGameScreen();

    #region Internal

    async UniTask SelectEntry(string categoryTestid, string entryTestid)
    {
        for (var attempt = 0; attempt < 3 && p.CurrentUiState != UIStateEnum.BuildMenu; attempt++)
        {
            await p.PressKey(p.CurrentUiState == UIStateEnum.PlaceBlock ? Key.Tab : Key.B);
            if (await PlaytestUiOps.PollUiState(UIStateEnum.BuildMenu, 4f)) break;
        }
        p.Assert(p.CurrentUiState == UIStateEnum.BuildMenu, $"ビルドメニューが開く ({entryTestid})");
        await p.UntilWebUiElement("build-menu-panel", 15f);
        await p.ClickWebUi(categoryTestid);
        var deadline = Time.realtimeSinceStartup + 20f;
        while (p.CurrentUiState != UIStateEnum.PlaceBlock && Time.realtimeSinceStartup < deadline)
        {
            await p.ClickWebUi(entryTestid);
            await UniTask.DelayFrame(10);
        }
        p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, $"エントリ選択でPlaceBlockへ遷移: {entryTestid}");
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.6f));
    }

    void AssertMarker(string name, Vector3Int cell, string label)
    {
        var marker = GameObject.Find(name);
        var expected = cell + new Vector3(0.5f, 0.5f, 0.5f);
        var ok = marker != null && marker.activeSelf && (marker.transform.position - expected).sqrMagnitude < 0.01f;
        p.Assert(ok, $"{label} 実際:{(marker == null ? "null" : marker.activeSelf ? marker.transform.position.ToString() : "inactive")}");
    }

    void AssertRangeBox(Vector3Int min, Vector3Int max, string label)
    {
        var box = GameObject.Find("BlueprintCopyRangeBox");
        var size = new Vector3(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1);
        var ok = box != null && box.activeSelf && box.transform.localScale == size;
        p.Assert(ok, $"{label} size={size} 実際:{(box == null ? "null" : box.transform.localScale.ToString())}");
    }

    bool TooltipHasParam(string value)
    {
        return tooltip.GetPresentation().Lines.Any(line => line.TextParams.Count > 0 && line.TextParams[0] == value);
    }

    System.Collections.Generic.List<Vector3> ActiveGhostPositions()
    {
        var root = GameObject.Find("BlueprintPastePreview");
        var result = new System.Collections.Generic.List<Vector3>();
        if (root == null) return result;
        foreach (Transform child in root.transform) if (child.gameObject.activeSelf) result.Add(child.position);
        return result;
    }

    #endregion
});
```

`BlueprintNameInputState` に `public bool IsOpen => _isOpen;` を足す（シナリオの assert 用。既存の `_isOpen` の読み取り公開のみ）。`MouseCursorTooltipState.GetPresentation()` が返す `TooltipPresentation.Lines` を読む（`TooltipTopic.cs:39` と同じ）。

- [ ] **Step 2: 実行する**

Run（worktree直下）:
```bash
uloop control-play-mode --project-path ./moorestech_client --action stop
SKILL=.claude/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/building/blueprint-copy-paste-via-ui.cs" /Users/sakastudio/hermes-agent/data/repos/moorestech_master/server_v8
```
Expected: `Success: true`・Asserts 全PASS・録画mp4が0byteでない・スクショ06にBP枠の画像が映り、その画像に地形や周囲ブロックが写り込んでいない（目視）。失敗したら `result.json` の最後のPASSと `uloop get-logs --log-type Error` で切り分ける

- [ ] **Step 3: コミットする**

```bash
git rm .agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-copy-box-probe.cs
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building/blueprint-copy-paste-via-ui.cs moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Blueprint/BlueprintNameInputState.cs
git commit -m "playtest: BPコピー/貼り付けのWeb UI版録画シナリオへ置き換え（Esc戻り・側面ヒット・空範囲拒否・E高さ・ドラッグ列・サムネイル）"
```

### Task 9: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] **Step 1:** `moores-code-review` スキルを起動し、ブランチ `feat/blueprint-copy-paste-ux` の全差分をレビューする。指摘は反映し、反映が判定経路・条件式・評価時点（セル解決・範囲計数・ドラッグ列・サムネイル同期）に触れたら Task 8 の録画シナリオを反映後のバイナリで再実施する。
- [ ] **Step 2:** レビュー記録は `../moorestech_logs/harness/` へ（コードrepoにコミットしない）。

### Task 10: セッション終了可能状態にすること

- [ ] **Step 1:** `pr-create` スキルでPRを作成する。masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。
- [ ] **Step 2:** 全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。`bd close moorestech-zbwxs --reason "PR作成"`。`moores-wt rm blueprint-copy-paste-ux` はPRマージ後にユーザー側で行う（本planの範囲外）。

---

## Self-Review coverage

| 要件 | タスク |
|---|---|
| R1 セル解決 | Task 3（`PlacementUnitCellResolver`・テスト）、Task 5/6 で使用、Task 8 で側面ヒット assert |
| R2 範囲AABB・ホイール廃止 | Task 5（`BlueprintCopySelection.CalcBox`・`OwnsWheelInput` override削除）、Task 8 |
| R3 1クリック・ESC戻り | Task 5（`BlueprintCopyClickInput`・`OnCancel`・`TryCancelInProgressOperation`）、Task 8 |
| R4 マーカー表示 | Task 5（`BlueprintCopyRangeVisualizer`）、Task 8 |
| R5 範囲内数・0拒否 | Task 1（規則）、Task 4（行）、Task 5（計数・拒否）、Task 8 |
| R6 失敗の可視化 | Task 4（結果型・辞書・Web通知ID）、Task 5（ログ＋通知） |
| R7 貼り付けQ/E | Task 6、Task 8 |
| R8 ドラッグ列 | Task 2（外形）、Task 3（列位置）、Task 6（`BlueprintPasteRunBuilder`）、Task 8 |
| R9 アンカー外形 | Task 1（テスト `AnchorFollowsBlockExtentNotBoxTest`） |
| R10 サムネイル | Task 7（撮影はライブラリ更新を起点に先回り生成。URLは撮影済みだけ）、Task 8 |
| R11 境界ブロック | Task 1（規則維持）、Task 5 テスト |
| R12 部分設置 | Task 6（`IsPlaceable` 要素単位） |

## 判断記録（ADR）

設計裁定: `docs/adr/0076-blueprint-copy-paste-share-placement-cell-height-and-run.md`（ユーザー裁定 D1〜D6 と委任、`.decisions/2026-10-08-*` 6件）。

planning中の判断:
- 貼り付けアンカーをブロック外形中心へ（旧BPは旧アンカー基準のオフセットのまま貼れる。セーブ形式不変）。出所: agent前提（ユーザーによる詳細設計の委任。ADR 0076）
- 部分設置は現行維持・列でも要素単位。出所: agent前提（委任。ADR 0076）
- `Blueprint/` を `Copy/` `Paste/` `Thumbnail/` に分け名前空間も追従（1ディレクトリ10ファイル規約）。出所: agent前提（AGENTS.md）
- `SnapHitPointToCell` は列車車両の距離判定に残す（設置セル解決からは外す）。出所: agent前提（置換対象の役割が違う）
- コピー/貼り付けの `Enable` で高さを地表へ戻す（`PlacementHeightOffset.ResetToGround`）。通常設置の「持ち替えで0へ戻す」と同じ意味。出所: agent前提
- クリックは「UI外で押下を登録→解放で1回」。ビルドメニュー選択クリックの解放漏れを無視する既存規則（`CommonBlockPlaceDragState.EndDrag`）に揃える。出所: agent前提
- ESCは2経路（Webモーダルの onClose→Cancel、ゲーム側 CloseUI→TryCancelInProgressOperation）とも「終点選択へ戻る」に収束させる。出所: agent前提（ユーザー裁定「ESCキャンセルしたら終点を選択する」の両経路への適用）
- 撮影器は `DontDestroyOnLoad` で主シーンへ持ち越し `ClientContext` から参照（新しい撮影器Prefab配置はシーン編集を要するため避ける）。出所: agent前提
- サムネイル未撮影の間は IconUrl を出さず、撮影完了で `OnThumbnailChanged` → トピック再配信（`IconEndpoint` の負キャッシュ404を踏まない）。出所: agent前提
- バッチ実行のサムネイルは `Texture2D.whiteTexture`（`ModAssetIconLoader` と同じ）。削除時にテクスチャを Destroy しない（ゲーム寿命・dispose考慮不要）。出所: agent前提
- 録画シナリオの名前入力はWebモーダルの文字打ちをDSLが持たないため `BlueprintNameInputState.Confirm` を直接呼ぶ（モーダルが開くことは `modal-input` の出現で検証）。出所: agent前提
- EditModeInPlayingTest は作らない。ランタイム挙動は Task 8 のunityプレイ録画テストで通し検証する。出所: agent前提
- beads `moorestech-izz2`（BP貼り付けYのRound/Floor不一致）は R1 で解消される。close は PR マージ時。出所: agent前提
- Phase 2.6 発火の裁定（ユーザーによる詳細設計の委任のもと agent が決定。出所: agent前提）:
  - [強 5-A] BP枠の IconUrl 不在＝未撮影: このままでよい。Web契約は種別ごとに iconUrl 省略を既に許し（blueprintCopy）、Web側は「画像か名前か」の表示分岐しか持たない
  - [強 5-B] `BlueprintCreateResult` は判別子 `Failure` 1本にし `Success` を派生へ（反映済み）
  - [強 5-D] サムネイル保持は `IBlueprintThumbnailLookup`（読み）と `BlueprintThumbnailContainer`（書き。DI注入のみ）へ分離、static 公開は読み面だけ（反映済み）
  - [強 検査7] ADR と plan の食い違い3件: (a) 送信中は `Creating` 局面で次の始点を受け付けない（plan を ADR に合わせた）、(b) 生成は要求時ではなくライブラリ更新を起点に先回りする（ADR を plan に合わせた。負キャッシュ404を踏まないため）、(c) 終点マーカーは新規の緑定数・範囲は既存 `PlaceableColor`（ADR を plan に合わせた。原文「終点は緑」。既存 `PlaceableColor` は青寄り）
  - [弱 5-F 高さ] 高さを0へ戻す書き手を `PlaceSystemStateController`（対象変更時）1本へ。BP系の `Enable` から `ResetToGround` を削除（反映済み・Task 3）
  - [弱 5-F ESC二経路] `BlueprintNameInputState.Cancel/Close` は `_isOpen` でガードされ、Web側Cancelとゲーム側ESCが同じ1回を二重に処理しない（先に閉じた側だけが効く）。所有者の統合はしない
  - [弱 5-C クリック] `TryConsumeClick` の bool は入力ゲート。理由の型化はしない（前例 `CommonBlockPlaceDragState.TryConsumeSendableRelease`）
  - [弱 5-C 未着/該当なし] ライブラリはログイン時に全件同期済み。`TryGetBlueprint` が見つからない理由をログに残す（反映済み）
  - [弱 5-F DontDestroyOnLoad] 撮影器はゲーム寿命。破棄の所有者は置かない（AGENTS.md 既知の制約）
  - [弱 5-G] `Game.Blueprint` は `BlueprintPasteCalculator` を既にクライアントが使う前例あり。Interface層へは移さない
  - [弱 検査6 重複] (e) ライブラリ検索は `TryGetBlueprint` へ、(f) 占有情報の生成は `BlueprintPlacementElementUtil.ToPositionInfo` へ統合（反映済み）。(a)(c) は同一メソッド内の2回で許容、(b) は共有規則の呼び出しであり重複ではない、(d) コピー（1クリック）と貼り付け（押下→ドラッグ→解放）は入力の形が違うため統合しない
  - [弱 検査7-2] 押下未登録の解放はビルドメニュー選択クリックの定常的な漏れで、拒否・縮退ではない。ログは出さない（前例 `CommonBlockPlaceDragState.EndDrag`）
  - [第3バケツ] `BlueprintCopyTargetRule.IntersectsBox` は既存 `BlockPositionInfoExtension.IsOverlap` へ委譲して重複を作らない（反映済み・Task 1）
- user-simulator review（2026-10-08）の反映（出所: シミュレーター予測→agent適用。ユーザーによる詳細設計の委任のもと）:
  - 全ブロックがマスタから消えたBPは撮影器が空被写体で例外を投げ以後の撮影が止まる → `TryBuild` で0件を検出しログして飛ばす（反映済み・Task 7）
  - 被写体と複製は撮影器直下のローカル原点に置き、撮影器は主シーンで隔離位置（y=-5000）へ退避。`BlockIconImagePhotographer` の複製配置を `localPosition` に（反映済み・Task 7）。Task 8 の目視項目に「地形が写り込まない」を追加
  - `BlueprintPasteCalculator` のマスタ欠損スキップに警告ログ（反映済み・Task 2）
  - Task 8 の ESC 検証を Web経路（`nameState.Cancel`）とゲーム側（Escキー）の2パターンに（反映済み）
  - 要裁定「全ブロック未解決のBPをロード時に削除するか」: A（サムネイル無し＋ログで残す）を採用。無効BPの整理は別件 bd 起票（本planの範囲外）
- Create結果はクライアント側enum `BlueprintCreateFailure` で表す（サーバーの `BlueprintFailureReason` に null 応答用の値を足さない。配置検査で修正）。出所: agent前提（前例 `BlueprintDeleteResult`）
