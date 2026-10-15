# ベルトの地形乗り越え Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 通常ベルトのドラッグ経路が、L字の形を保ったまま経路途中の丘を坂で登って越え、地形に埋まらないようにする。

**Architecture:** 既存の自動立体交差（`ConveyorOverpassRaiser` → `ConveyorObstacleScanner` → `ConveyorVerticalEnvelope`）の入口で、中間セルの基準Yを地形下限（地形最高点を含むセル）まで持ち上げてから既存ソルバーで解く。地形込みで端点へ戻れないときは地形なしで解き直し、その旨を列単位のフラグで返し、ベルト設置システムがカーソルツールチップへ「設置を妨げない案内」を1行積む。地形の問い合わせは interface `IConveyorTerrainFloor` で注入し、ソルバーは Physics を知らない。

**Tech Stack:** Unity C#（moorestech_client / Client.Game）、NUnit（Client.Tests）、ローカライズCSV＋SourceGenerator（`Localization/localization.csv`）、webui の `gen:i18n`、プレイテストDSL（unity-playmode-recorded-playtest）

## Requirements

1. 通常ベルト（直線・自動坂）のドラッグで、経路途中のセルの地形下限Yが経路本来のYを上回るとき、そのセルは地形下限Y以上に持ち上がる（坂で登って越え、越えたら降りる）。受け入れ: 丘セル x=4,5 の地形下限33・端点32の東向き7セル列で、x=4,5 が y=33 になる（ユニット＋録画）
2. 経路の水平形状（L字・1コーナー）は変えない。受け入れ: 地形ありでも各セルの XZ が地形なしと一致する
3. 地形は下限としてだけ効き、谷（地形下限 < 経路Y）では降りない。受け入れ: 地形下限が経路Yより低いセルはYが変わらない
4. 地形下限Yは端点と同じ「セル内の地形最高点を含むセル」（`PlacementGroundCellResolver.TryResolveCellFromGround`、ADR 0047。heightOffset 0、1x1x1）。受け入れ: `GroundConveyorTerrainFloor` がこの関数をそのまま呼ぶ
5. 越え方が複数あるときは既存高架と同じ規則（`ConveyorVerticalEnvelope` をそのまま使う。最低高さ＋狭い隙間の橋渡し）。受け入れ: 丘セル x=2,4（下限1）・x=3（下限なし）・端点0の7セル列で Y が 0,0,1,1,1,0,0 になる
6. 地形込みで端点へ戻れない（`feasible` に偽がある）ときは、地形を無視した現行の経路で解き直して設置する。既存ブロック起因の `ImpossibleOverpass` はそのまま残る。受け入れ: 3セル列の中央が地形下限2のとき全セル y=0・全セル Placeable・`OverpassBlocked` 全偽。同じ列に既存ブロック (1,0,0)(1,1,0) もあれば端点は従来どおり不可
7. 6で戻した結果が設置可能（地形なしの解に `feasible` 偽が無い）のときだけ、カーソルツールチップに設置を妨げない案内 `ui.tooltip.placeBeltTerrainClimbAbandoned` を1行出す（カーソルセル位置によらない）。セルは赤にしない。受け入れ: レポーターのユニットテストと録画テストでキーを確認
8. 坂ベルト手持ちの一定勾配ドラッグ（`BeltConveyorSlopePathBuilder`）には適用しない。受け入れ: `CalculateSlopePoint` は地形を参照せず、案内フラグは常に偽
9. 端点セル（列の先頭と末尾）には地形下限を付けない（プレイヤーが照準・Q/Eで決めた高さ）。受け入れ: 端点セルにだけ高い地形下限があっても列は変わらず、案内も出ない
10. 持ち上げた基準Yから既存ブロックの積み上がりを走査する（丘の上の既存ブロックも跨ぐ）。受け入れ: 丘セル x=3（下限1）の上の (3,1,0) に既存ブロックがあると x=3 が y=2 になる
11. 地表が取れないセル（プローブ失敗）には地形下限を付けない
12. 変更はクライアントの設置プレビュー・送信列だけ。サーバー・プロトコル・セーブ形式は変えない

やらないこと: 水平に回り込む経路探索、谷へ降りる追従、地形めり込みによる設置不可判定（全設置系で入れない方針のまま）、電線など接続線の地形判定、坂ベルト手持ちの経路変更、BP貼り付け・張替え設置の経路変更。

## Global Constraints

- 1ファイル200行未満。1ディレクトリのコードは10ファイルまで
- 新規コードで `Func<>` を使わない（既存の `Func<Vector3Int, bool> isOccupied` 等の引数はそのまま残す。地形は interface で渡す）
- 単純な getter/setter プロパティ禁止。結果型は `public readonly` フィールドで持つ
- デフォルト引数禁止。引数追加時は全呼び出し側を変更する
- 主要処理に日本語→英語の2行コメント（各1行）。`#region Internal` はメソッド内ローカル関数だけを囲う
- try-catch 禁止。テスト専用の型はテストアセンブリ（Client.Tests）に置き、プロダクションに残さない
- 意図して黙って続行する行には `silent-ok: <理由> / <誰がどこで気づくか>` を書く
- .cs 変更後は `uloop compile --project-path ./moorestech_client`（ErrorCount 0）
- .meta は手で作らない（Unity が生成したものはコミット可）
- ローカライズCSVを変更したら `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` の印（SchemaWatcher が更新）も一緒にコミットし、`moorestech_web/webui` で `npm run gen:i18n` を実行して `src/shared/i18n/generated/localizationKeys.ts` も更新する
- 設計ADR: `docs/adr/0081-belt-drag-climbs-over-terrain.md`

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / 地表探査の本番実装を前例（Ground/PlacementGroundFollowStep）に合わせ Ground/ へ移設。死活表: ベルトのドラッグ・坂ベルト手持ち・既存ブロック高架・不可理由ツールチップはすべて存続
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## File Structure

| ファイル | 種別 | 責務 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/IConveyorTerrainFloor.cs` | Create | セル列の地形下限Yを答える問い合わせ口 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/GroundConveyorTerrainFloor.cs` | Create | 本番実装。`PlacementGroundCellResolver` で地形最高点を含むセルを返す |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorOverpassRaiseResult.cs` | Create | Raiser の戻り値（セル毎の立体交差不能列＋地形乗り越え断念フラグ） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorOverpassRaiser.cs` | Modify | 地形で基準を持ち上げて解く→解けなければ地形なしで解き直す |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlacePointCalculator.cs` | Modify | 地形口を Raiser へ渡し、断念フラグを out で返す |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlaceRunBuilder.cs` | Modify | 断念フラグを out で中継（坂経路は常に偽） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorFeedbackReporter.cs` | Create | ベルト固有の不可理由行と断念案内行をフィードバックへ積む（PlaceSystem の `PushBeltReason` を移設） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs` | Modify | 断念フラグを受けてレポーターを呼ぶ（196行→200行未満を保つため local 関数を移設） |
| `Localization/localization.csv` | Modify | `ui.tooltip.placeBeltTerrainClimbAbandoned` 行 |
| `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` | Modify（SchemaWatcher 自動） | 再compile印 |
| `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` | Modify（生成） | webui 側キー |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConveyorOverpass/FakeConveyorTerrainFloor.cs` | Create | テスト用の地形口（XZ→下限Y辞書） |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConveyorOverpass/ConveyorOverpassRaiserTerrainTest.cs` | Create | 要件1〜3,5,6,9,10,11 のユニットテスト |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConveyorOverpass/ConveyorOverpassRaiserTest.cs` | Modify | 新シグネチャへ追従 |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConveyorOverpass/ConveyorOverpassConveyanceTest.cs` | Modify | 新シグネチャへ追従 |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorPlacePointCalculatorTest.cs` | Modify | 新シグネチャへ追従 |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorFeedbackReporterTest.cs` | Create | 要件7 のユニットテスト |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-terrain-climb-via-ui.cs` | Create | unityプレイ録画テストのシナリオ |

`Common/ConveyorOverpass/` は3→5ファイル、`Ground/` は5→6ファイル、`BeltConveyor/Parts/` は9→10ファイル、`Client.Tests/PlaceSystem/ConveyorOverpass/` は4→6ファイル（いずれも10以下）。

---

### Task 1: 地形下限で持ち上げる立体交差ソルバー

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/IConveyorTerrainFloor.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/GroundConveyorTerrainFloor.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorOverpassRaiseResult.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass/ConveyorOverpassRaiser.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlacePointCalculator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlaceRunBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`（`out _` を足すだけ。Task 2 で置き換える）
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConveyorOverpass/FakeConveyorTerrainFloor.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConveyorOverpass/ConveyorOverpassRaiserTerrainTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConveyorOverpass/ConveyorOverpassRaiserTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConveyorOverpass/ConveyorOverpassConveyanceTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorPlacePointCalculatorTest.cs`

**Interfaces:**
- Consumes: `PlacementGroundCellResolver.TryResolveCellFromGround(Vector3Int cellPosition, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset, out Vector3Int resolvedPosition)`（既存）
- Produces:
  - `public interface IConveyorTerrainFloor { bool TryGetFloorY(Vector3Int cell, out int floorY); }`（namespace `Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass`）
  - `public class GroundConveyorTerrainFloor : IConveyorTerrainFloor`（namespace `Client.Game.InGame.BlockSystem.PlaceSystem.Ground`。Calculator 側に `using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;` を追加）
  - `public readonly struct ConveyorOverpassRaiseResult { public readonly IReadOnlyList<bool> OverpassBlocked; public readonly bool TerrainClimbAbandoned; public ConveyorOverpassRaiseResult(IReadOnlyList<bool> overpassBlocked, bool terrainClimbAbandoned) }`
  - `ConveyorOverpassRaiser.Raise(List<PlaceInfo> placeInfos, int cornerIndex, Func<Vector3Int, bool> isOccupied, IConveyorTerrainFloor terrainFloor) : ConveyorOverpassRaiseResult`
  - `BeltConveyorPlacePointCalculator.CalculateStraightPoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement straightBlockMaster, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons, out bool terrainClimbAbandoned)`（インスタンス）
  - `BeltConveyorPlacePointCalculator.CalculateStraightPoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement straightBlockMaster, Func<PlaceInfo, BlockMasterElement, bool> isNotExistBlock, Func<Vector3Int, bool> isOccupied, IConveyorTerrainFloor terrainFloor, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons, out bool terrainClimbAbandoned)`（static）
  - `BeltConveyorPlaceRunBuilder.Build(Vector3Int dragStartPoint, Vector3Int placePoint, BlockDirection blockDirection, BeltConveyorHoldingBlock holdingBlock, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons, out bool terrainClimbAbandoned)`
  - テスト用: `public class FakeConveyorTerrainFloor : IConveyorTerrainFloor { public FakeConveyorTerrainFloor(Dictionary<Vector2Int, int> floorYByXZ) }`（namespace `Client.Tests.PlaceSystem.ConveyorOverpass`。キーは `new Vector2Int(x, z)`）

- [ ] **Step 1: 地形口・本番実装・結果型を作る**

`IConveyorTerrainFloor.cs`:

```csharp
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // ベルト経路の各セルで、地形がベルトを持ち上げる下限Yを答える（ADR 0081）
    // Answers, per belt-path cell, the lower-bound Y the terrain lifts the belt to (ADR 0081)
    public interface IConveyorTerrainFloor
    {
        // 地表が取れなければfalse。そのセルには下限を付けない
        // Returns false when no ground is found; that cell then gets no lower bound
        bool TryGetFloorY(Vector3Int cell, out int floorY);
    }
}
```

`GroundConveyorTerrainFloor.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    // 端点と同じ「セル内の地形最高点を含むセル」を地形下限にする（ADR 0047 / ADR 0081 決定5）
    // Uses the same "cell containing the terrain max" as the endpoints for the terrain lower bound (ADR 0047 / ADR 0081 decision 5)
    public class GroundConveyorTerrainFloor : IConveyorTerrainFloor
    {
        public bool TryGetFloorY(Vector3Int cell, out int floorY)
        {
            // ベルトは1x1x1なので向きは結果に効かない。Q/Eの高さ補正は端点側の責務なので0で問う
            // A belt is 1x1x1 so the direction has no effect; the Q/E offset belongs to the endpoints, so query with 0
            floorY = 0;
            // silent-ok: 地表が無いセルは下限を付けず経路高さのまま置く(ADR 0081) / プレビューでそのセルが持ち上がらないことが見える
            if (!PlacementGroundCellResolver.TryResolveCellFromGround(cell, BlockDirection.North, Vector3Int.one, 0, out var resolved)) return false;

            floorY = resolved.y;
            return true;
        }
    }
}
```

`ConveyorOverpassRaiseResult.cs`:

```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 立体交差計算の結果。設置不可にしたセル列と、地形の乗り越えを諦めたかを運ぶ
    // Result of the overpass calculation: the cells it blocked, and whether it gave up climbing the terrain
    public readonly struct ConveyorOverpassRaiseResult
    {
        // placeInfosと同じ添字で「立体交差が組めずこの呼び出しで設置不可にしたセル」。理由の文言は呼び出し側が持つ
        // Indexed like placeInfos: cells this call made unplaceable because no overpass fits; the wording belongs to the caller
        public readonly IReadOnlyList<bool> OverpassBlocked;

        // 地形込みでは解けず地形を無視した経路で置けるようにしたか（ADR 0081 決定3・7）
        // Whether it fell back to the terrain-ignoring path, which is placeable, because the terrain-aware one was unsolvable (ADR 0081 decisions 3, 7)
        public readonly bool TerrainClimbAbandoned;

        public ConveyorOverpassRaiseResult(IReadOnlyList<bool> overpassBlocked, bool terrainClimbAbandoned)
        {
            OverpassBlocked = overpassBlocked;
            TerrainClimbAbandoned = terrainClimbAbandoned;
        }
    }
}
```

- [ ] **Step 2: Raiser を地形対応に書き換える**

`ConveyorOverpassRaiser.cs` を全置換:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 地形と障害物の下限→包絡線→PlaceInfo列にY上昇・縦方向再計算・設置可否を反映する
    // Terrain and obstacle lower bounds -> envelope -> apply raised Y, recompute vertical direction, and mark placeability on the PlaceInfo list.
    public class ConveyorOverpassRaiser
    {
        private readonly ConveyorObstacleScanner _scanner = new();

        public ConveyorOverpassRaiseResult Raise(List<PlaceInfo> placeInfos, int cornerIndex, Func<Vector3Int, bool> isOccupied, IConveyorTerrainFloor terrainFloor)
        {
            var overpassBlocked = new bool[placeInfos.Count];
            if (placeInfos.Count == 0) return new ConveyorOverpassRaiseResult(overpassBlocked, false);

            // 地形で持ち上げた基準から解き、端点へ戻れなければ地形を外して解き直す（ADR 0081 決定3）
            // Solve from the terrain-lifted base; if the endpoints cannot be reached, re-solve without terrain (ADR 0081 decision 3)
            var cells = placeInfos.Select(info => info.Position).ToList();
            var terrainLiftedCells = LiftByTerrain();
            var (beltY, feasible) = SolveFrom(terrainLiftedCells);
            var terrainClimbAbandoned = false;
            if (feasible.Contains(false) && !terrainLiftedCells.SequenceEqual(cells))
            {
                (beltY, feasible) = SolveFrom(cells);

                // 地形なしでも解けないなら既存ブロック起因の不可が出るので、案内は出さない
                // If even the terrain-free solve fails, the block-caused unplaceability shows instead, so no notice
                terrainClimbAbandoned = !feasible.Contains(false);
            }

            // 上昇したセルとその隣接のみ縦方向を再計算する（無関係なセルの既存方向は維持）
            // Recompute vertical direction only for raised cells and their neighbors (untouched cells keep their existing direction).
            var raised = new bool[placeInfos.Count];
            for (var i = 0; i < placeInfos.Count; i++) raised[i] = beltY[i] != cells[i].y;

            for (var i = 0; i < placeInfos.Count; i++)
            {
                var info = placeInfos[i];
                var pos = info.Position;
                pos.y = beltY[i];
                info.Position = pos;

                if (NeighborhoodChanged(i)) info.VerticalDirection = ResolveVertical(i);

                // 端点を固定できず立体交差が組めないセル（セル自体は空でも置けない）
                // Cells where the endpoints cannot be pinned, so no overpass fits (unplaceable even on an empty cell)
                if (info.Placeable && !feasible[i])
                {
                    info.Placeable = false;
                    overpassBlocked[i] = true;
                }
            }

            return new ConveyorOverpassRaiseResult(overpassBlocked, terrainClimbAbandoned);

            #region Internal

            List<Vector3Int> LiftByTerrain()
            {
                // 端点はプレイヤーが決めた高さなので触らない。谷は下げず、丘だけ持ち上げる（ADR 0081 決定2）
                // Endpoints keep the player-chosen height; valleys never lower the belt, only hills lift it (ADR 0081 decision 2)
                var lifted = new List<Vector3Int>(cells);
                for (var i = 1; i < lifted.Count - 1; i++)
                {
                    // silent-ok: 地表が無いセルは下限を付けない(ADR 0081) / プレビューでそのセルが持ち上がらないことが見える
                    if (!terrainFloor.TryGetFloorY(lifted[i], out var floorY)) continue;
                    if (floorY <= lifted[i].y) continue;
                    lifted[i] = new Vector3Int(lifted[i].x, floorY, lifted[i].z);
                }
                return lifted;
            }

            (int[] beltY, bool[] feasible) SolveFrom(List<Vector3Int> baseCells)
            {
                // 既存ブロックは持ち上げ後の基準から上へ辿る（丘の上のブロックを見落とさない）
                // Scan stacked blocks upward from the lifted base so blocks standing on a hill are not missed
                var lowerBounds = _scanner.ComputeLowerBounds(baseCells, isOccupied);
                return ConveyorVerticalEnvelope.Solve(lowerBounds, cells[0].y, cells[^1].y, cornerIndex);
            }

            bool NeighborhoodChanged(int i)
            {
                if (raised[i]) return true;
                if (i > 0 && raised[i - 1]) return true;
                if (i + 1 < raised.Length && raised[i + 1]) return true;
                return false;
            }

            BlockVerticalDirection ResolveVertical(int i)
            {
                // 次が高ければ登り、前が高ければ下り、それ以外は水平
                // Up if the next cell is higher, Down if the previous is higher, otherwise Horizontal.
                if (i + 1 < beltY.Length && beltY[i + 1] > beltY[i]) return BlockVerticalDirection.Up;
                if (i - 1 >= 0 && beltY[i - 1] > beltY[i]) return BlockVerticalDirection.Down;
                return BlockVerticalDirection.Horizontal;
            }

            #endregion
        }
    }
}
```

注意: ローカル関数 `ResolveVertical` は再代入される `beltY` を捕捉するが、参照は再代入後のループ内だけなので最終解を読む。

- [ ] **Step 3: Calculator を地形口と断念フラグに対応させる**

`BeltConveyorPlacePointCalculator.cs` の変更点:

フィールドに追加（`_blockGameObjectDataStore` の下）:

```csharp
        private readonly IConveyorTerrainFloor _terrainFloor = new GroundConveyorTerrainFloor();
```

インスタンス版を置換:

```csharp
        public List<PlaceInfo> CalculateStraightPoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement straightBlockMaster, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons, out bool terrainClimbAbandoned)
        {
            return CalculateStraightPoint(startPoint, endPoint, isStartDirectionZ, blockDirection, straightBlockMaster, IsNotExistBlock, IsOccupied, _terrainFloor, out blockCauses, out beltReasons, out terrainClimbAbandoned);
        }
```

static 版の署名と Raiser 呼び出しを置換:

```csharp
        public static List<PlaceInfo> CalculateStraightPoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement straightBlockMaster, Func<PlaceInfo, BlockMasterElement, bool> isNotExistBlock, Func<Vector3Int, bool> isOccupied, IConveyorTerrainFloor terrainFloor, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons, out bool terrainClimbAbandoned)
        {
            var (placeInfos, startToCornerDistance) = BeltConveyorStraightPathBuilder.Build(startPoint, endPoint, isStartDirectionZ, blockDirection);

            // 地形の丘と既存ブロックを自動で跨ぐ立体交差プロファイルを後段で重ねる
            // Layer the auto-overpass profile that steps over terrain hills and existing blocks
            var raiseResult = new ConveyorOverpassRaiser().Raise(placeInfos, startToCornerDistance, isOccupied, terrainFloor);
            var overpassBlocked = raiseResult.OverpassBlocked;
            terrainClimbAbandoned = raiseResult.TerrainClimbAbandoned;
```

（以降の `blockCauses = ...` から `return placeInfos;` までは既存のまま。`overpassBlocked[i]` の参照もそのまま通る）

`CalculateSlopePoint` は変更しない（ADR 0081 決定4）。

- [ ] **Step 4: RunBuilder と PlaceSystem を新シグネチャへ追従させる**

`BeltConveyorPlaceRunBuilder.Build` を置換:

```csharp
        // blockCauses/beltReasonsはPlaceInfo列と同添字で並走する原因列。terrainClimbAbandonedは列全体の地形乗り越え断念
        // blockCauses/beltReasons are cause columns indexed like the returned PlaceInfo list; terrainClimbAbandoned is per run
        public List<PlaceInfo> Build(Vector3Int dragStartPoint, Vector3Int placePoint, BlockDirection blockDirection, BeltConveyorHoldingBlock holdingBlock, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons, out bool terrainClimbAbandoned)
        {
            // 軸決めはドラッグに属するためドラッグ状態へ委ねる
            // The axis belongs to the drag, so the drag state owns it
            var isStartDirectionZ = _dragState.ResolveDragAxisIsZ(dragStartPoint, placePoint);

            // 坂選択中は一定勾配の専用経路のみ。地形乗り越えは効かせない（ADR 0081 決定4）
            // A slope selection uses only the constant-grade path, without terrain climbing (ADR 0081 decision 4)
            if (holdingBlock.SlopeGrade.HasValue)
            {
                terrainClimbAbandoned = false;
                return _placePointCalculator.CalculateSlopePoint(dragStartPoint, placePoint, isStartDirectionZ, blockDirection, holdingBlock, out blockCauses, out beltReasons);
            }

            var cellInfos = _placePointCalculator.CalculateStraightPoint(dragStartPoint, placePoint, isStartDirectionZ, blockDirection, holdingBlock.BlockMaster, out blockCauses, out beltReasons, out terrainClimbAbandoned);

            // セル列へ直線・坂を1対1で割り当てる
            // Assign straight/slope blocks to cells one-to-one
            return BeltConveyorStraightCellBlockResolver.ResolveStraightRun(cellInfos, holdingBlock.Family, beltReasons);
        }
```

`BeltConveyorPlaceSystem.cs` の `UpdateCurrentPlaceInfos` 内の呼び出しだけ一時的に:

```csharp
                _currentPlaceInfos = _placeRunBuilder.Build(dragStartPoint, placePoint, _currentBlockDirection, holdingBlock, out var cellCauses, out var cellBeltReasons, out _);
```

- [ ] **Step 5: 既存テストを新シグネチャへ追従させ、テスト用地形口を作る**

`FakeConveyorTerrainFloor.cs`（Client.Tests）:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using UnityEngine;

namespace Client.Tests.PlaceSystem.ConveyorOverpass
{
    // XZごとの地形下限Yを辞書で返すテスト用の地形口。辞書に無いセルは地表なし扱い
    // Test terrain floor returning per-XZ lower-bound Y from a dictionary; a missing cell is treated as no ground
    public class FakeConveyorTerrainFloor : IConveyorTerrainFloor
    {
        private readonly Dictionary<Vector2Int, int> _floorYByXZ;

        public FakeConveyorTerrainFloor(Dictionary<Vector2Int, int> floorYByXZ)
        {
            _floorYByXZ = floorYByXZ;
        }

        public bool TryGetFloorY(Vector3Int cell, out int floorY)
        {
            return _floorYByXZ.TryGetValue(new Vector2Int(cell.x, cell.z), out floorY);
        }
    }
}
```

既存の呼び出しを置換（地形なし＝空辞書。挙動は従来と同一であることを既存テストが保証する）:

- `ConveyorOverpassRaiserTest.cs` の `new ConveyorOverpassRaiser().Raise(infos, N, X)` 5箇所 → `new ConveyorOverpassRaiser().Raise(infos, N, X, NoTerrain())`。108行目は `var overpassBlocked = new ConveyorOverpassRaiser().Raise(infos, 2, occupied.Contains, NoTerrain()).OverpassBlocked;`。クラス内に追加:

```csharp
        private static FakeConveyorTerrainFloor NoTerrain()
        {
            return new FakeConveyorTerrainFloor(new Dictionary<Vector2Int, int>());
        }
```

- `BeltConveyorPlacePointCalculatorTest.cs` 121行目の呼び出し: `_ => false,` の次行に `new FakeConveyorTerrainFloor(new Dictionary<Vector2Int, int>()),` を挿入し、末尾の `out _,\n out _` を `out _,\n out _,\n out _` にする。153行目の呼び出し: `blockMasterElement, (_, _) => true, obstacle.Contains, new FakeConveyorTerrainFloor(new Dictionary<Vector2Int, int>()), out _, out var beltReasons, out _);`。ファイル先頭に `using Client.Tests.PlaceSystem.ConveyorOverpass;` を追加
- `ConveyorOverpassConveyanceTest.cs` 59行目の呼び出し: `cell => world.Exists(cell), new FakeConveyorTerrainFloor(new Dictionary<Vector2Int, int>()), out _, out _, out _);`

- [ ] **Step 6: 地形乗り越えのテストを書く**

`ConveyorOverpassRaiserTerrainTest.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.ConveyorOverpass
{
    // 地形を下限として乗り越えるベルト列（ADR 0081）の高さプロファイルを検証する
    // Verifies the height profile of a belt run that climbs over terrain as a lower bound (ADR 0081)
    public class ConveyorOverpassRaiserTerrainTest
    {
        [Test]
        public void HillInMiddle_ClimbsOverAndComesBack()
        {
            var infos = FlatEastPath(7);
            var result = Raise(infos, Terrain((3, 1), (4, 1)));

            Assert.AreEqual(new[] { 0, 0, 0, 1, 1, 0, 0 }, Ys(infos));
            Assert.AreEqual(BlockVerticalDirection.Up, infos[2].VerticalDirection);
            Assert.AreEqual(BlockVerticalDirection.Down, infos[5].VerticalDirection);
            Assert.IsFalse(result.TerrainClimbAbandoned);
            Assert.IsTrue(infos.All(info => info.Placeable));
        }

        [Test]
        public void Valley_DoesNotDescend()
        {
            var infos = FlatEastPath(5);
            var result = Raise(infos, Terrain((1, -3), (2, -3), (3, -3)));

            Assert.AreEqual(new[] { 0, 0, 0, 0, 0 }, Ys(infos));
            Assert.IsFalse(result.TerrainClimbAbandoned);
        }

        [Test]
        public void HorizontalShape_IsKept()
        {
            var infos = FlatEastPath(7);
            Raise(infos, Terrain((3, 2)));

            for (var x = 0; x < 7; x++) Assert.AreEqual(new Vector2Int(x, 0), new Vector2Int(infos[x].Position.x, infos[x].Position.z));
        }

        [Test]
        public void TwoHillsNarrowGap_BridgedLikeBlockOverpass()
        {
            var infos = FlatEastPath(7);
            Raise(infos, Terrain((2, 1), (4, 1)));

            Assert.AreEqual(new[] { 0, 0, 1, 1, 1, 0, 0 }, Ys(infos));
        }

        [Test]
        public void SteepNextToEndpoint_FallsBackToTerrainFreePathAndNotifies()
        {
            var infos = FlatEastPath(3);
            var result = Raise(infos, Terrain((1, 2)));

            Assert.AreEqual(new[] { 0, 0, 0 }, Ys(infos));
            Assert.IsTrue(infos.All(info => info.Placeable));
            Assert.IsTrue(result.OverpassBlocked.All(blocked => !blocked));
            Assert.IsTrue(result.TerrainClimbAbandoned);
        }

        [Test]
        public void FallbackStillUnsolvable_KeepsBlockInfeasibilityWithoutNotice()
        {
            var infos = FlatEastPath(3);
            var occupied = new HashSet<Vector3Int> { new(1, 0, 0), new(1, 1, 0) };
            var result = new ConveyorOverpassRaiser().Raise(infos, 2, occupied.Contains, Terrain((1, 2)));

            Assert.IsFalse(infos[0].Placeable);
            Assert.IsFalse(infos[2].Placeable);
            Assert.AreEqual(new[] { true, false, true }, result.OverpassBlocked);
            Assert.IsFalse(result.TerrainClimbAbandoned);
        }

        [Test]
        public void EndpointTerrain_IsIgnored()
        {
            var infos = FlatEastPath(5);
            var result = Raise(infos, Terrain((0, 3), (4, 3)));

            Assert.AreEqual(new[] { 0, 0, 0, 0, 0 }, Ys(infos));
            Assert.IsFalse(result.TerrainClimbAbandoned);
        }

        [Test]
        public void BlockOnHill_IsScannedFromLiftedBase()
        {
            var infos = FlatEastPath(7);
            var occupied = new HashSet<Vector3Int> { new(3, 1, 0) };
            new ConveyorOverpassRaiser().Raise(infos, 6, occupied.Contains, Terrain((3, 1)));

            Assert.AreEqual(new[] { 0, 0, 1, 2, 1, 0, 0 }, Ys(infos));
        }

        [Test]
        public void NoGroundCell_GetsNoLowerBound()
        {
            var infos = FlatEastPath(5);
            var result = Raise(infos, Terrain());

            Assert.AreEqual(new[] { 0, 0, 0, 0, 0 }, Ys(infos));
            Assert.IsFalse(result.TerrainClimbAbandoned);
        }

        #region Helpers

        private static ConveyorOverpassRaiseResult Raise(List<PlaceInfo> infos, FakeConveyorTerrainFloor terrain)
        {
            return new ConveyorOverpassRaiser().Raise(infos, infos.Count - 1, _ => false, terrain);
        }

        private static FakeConveyorTerrainFloor Terrain(params (int x, int floorY)[] cells)
        {
            return new FakeConveyorTerrainFloor(cells.ToDictionary(cell => new Vector2Int(cell.x, 0), cell => cell.floorY));
        }

        private static int[] Ys(List<PlaceInfo> infos)
        {
            return infos.Select(info => info.Position.y).ToArray();
        }

        private static List<PlaceInfo> FlatEastPath(int length)
        {
            // 原点から+X方向へ水平に並ぶベルト列
            // A horizontal belt row extending in +X from the origin
            var list = new List<PlaceInfo>();
            for (var x = 0; x < length; x++)
            {
                list.Add(new PlaceInfo
                {
                    Position = new Vector3Int(x, 0, 0),
                    Direction = BlockDirection.East,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    Placeable = true,
                });
            }
            return list;
        }

        #endregion
    }
}
```

期待値の導出（実装者が値を疑ったときの照合用）: `HillInMiddle` は下限 [0,0,0,1,1,0,0] を TwoPass しても同じで端点0は可。`TwoHillsNarrowGap` は下限 [0,0,1,0,1,0,0] の x=3 が幅1の窪みで、必要幅 (1-0)+(1-0)+1=3 > 1 のため 1 で橋渡し。`SteepNextToEndpoint` は下限 [0,2,0] が TwoPass で [1,2,1] となり端点不可→地形なし [0,0,0] へ戻る。`BlockOnHill` は基準 x=3 を1へ持ち上げてから (3,1,0) を走査して下限2、TwoPass で [0,0,1,2,1,0,0]。`HillInMiddle` の `Placeable` 全真は「地形でセルを不可にしない」を確かめる。

- [ ] **Step 7: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\.(ConveyorOverpass|BeltConveyor)\."`
Expected: ErrorCount 0 / 全PASS（既存の Raiser・Envelope・Scanner・Conveyance・Calculator テストも通る）

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ConveyorOverpass moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem
git commit -m "feat: ベルトのドラッグ経路が地形の丘を下限として乗り越える"
```

---

### Task 2: 地形乗り越えを諦めたときのツールチップ案内

**Files:**
- Modify: `Localization/localization.csv`
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`（SchemaWatcher が更新したものをコミット）
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（生成）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorFeedbackReporter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorFeedbackReporterTest.cs`

**Interfaces:**
- Consumes: `BeltConveyorPlaceRunBuilder.Build(..., out bool terrainClimbAbandoned)`（Task 1）
- Produces:
  - `LocalizationKeys.Ui.Tooltip.PlaceBeltTerrainClimbAbandoned`（CSVキー `ui.tooltip.placeBeltTerrainClimbAbandoned` から生成）
  - `public static class BeltConveyorFeedbackReporter { public static void Report(PlacementFeedback feedback, IReadOnlyList<BeltConveyorPlacementBlockReason> beltReasons, int cursorIndex, bool terrainClimbAbandoned) }`（namespace `Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts`）

- [ ] **Step 1: ローカライズ行を足す**

`Localization/localization.csv` の `ui.tooltip.placeBeltNoSlopeBlock` 行の直後に1行追加:

```csv
ui.tooltip.placeBeltTerrainClimbAbandoned,Terrain too steep to climb; the belt will be placed buried,Terrain too steep to climb; the belt will be placed buried,地形が急で乗り越えられないため、地形に埋めて設置します,Gelände zu steil zum Überqueren; das Band wird eingegraben platziert,지형이 너무 가팔라 넘을 수 없어 묻힌 채로 설치됩니다
```

続けて `cd moorestech_web/webui && npm run gen:i18n` を実行し、`src/shared/i18n/generated/localizationKeys.ts` に `placeBeltTerrainClimbAbandoned` が入ったことを `grep -c placeBeltTerrainClimbAbandoned src/shared/i18n/generated/localizationKeys.ts`（1以上）で確かめる。node_modules が無くて失敗したら `npm ci` してから再実行する。

- [ ] **Step 2: レポーターを作る**

`BeltConveyorFeedbackReporter.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// ベルト固有の不可理由と地形乗り越え断念の案内をカーソルツールチップへ積む
    /// Pushes belt-specific block reasons and the terrain-climb-abandoned notice onto the cursor tooltip
    /// </summary>
    public static class BeltConveyorFeedbackReporter
    {
        public static void Report(PlacementFeedback feedback, IReadOnlyList<BeltConveyorPlacementBlockReason> beltReasons, int cursorIndex, bool terrainClimbAbandoned)
        {
            // カーソルセルのベルト固有理由（立体交差不能・坂ブロック欠落）
            // The cursor cell's belt-specific reason (impossible overpass, missing slope block)
            if (0 <= cursorIndex && beltReasons[cursorIndex] != BeltConveyorPlacementBlockReason.None)
            {
                feedback.Add(new TooltipLine(BeltConveyorPlacementBlockReasonTooltipKey.ToKey(beltReasons[cursorIndex])));
            }

            // 断念は列単位の判断なので、カーソル位置によらず設置を妨げない案内を1行出す（ADR 0081 決定7）
            // Abandonment is decided per run, so one non-blocking notice line is shown regardless of the cursor cell (ADR 0081 decision 7)
            if (terrainClimbAbandoned) feedback.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceBeltTerrainClimbAbandoned));
        }
    }
}
```

- [ ] **Step 3: PlaceSystem から呼ぶ**

`BeltConveyorPlaceSystem.cs` の変更:

1. `var (placeCauses, beltReasons) = UpdateCurrentPlaceInfos();` → `var (placeCauses, beltReasons, terrainClimbAbandoned) = UpdateCurrentPlaceInfos();`
2. `PushBeltReason();` の行とその直前2行コメントを次に置換:

```csharp
            // ベルト固有の理由と地形乗り越え断念の案内はベルト側が積む
            // Belt-specific reasons and the terrain-climb-abandoned notice are pushed on the belt side
            BeltConveyorFeedbackReporter.Report(feedback, beltReasons, cursorIndex, terrainClimbAbandoned);
```

3. `#region Internal` 内の `void PushBeltReason() { ... }` ローカル関数を削除する
4. `UpdateCurrentPlaceInfos` を置換:

```csharp
            // 設置点列を更新し、セル毎の共有原因・ベルト固有理由の列と列全体の地形乗り越え断念を返す（列はPlaceInfo列と同じ添字）
            // Updates the placement point list and returns the per-cell shared cause and belt reason columns plus the per-run terrain-climb abandonment
            (List<PlacementBlockCause> placeCauses, List<BeltConveyorPlacementBlockReason> beltReasons, bool terrainClimbAbandoned) UpdateCurrentPlaceInfos()
            {
                var dragStartPoint = _dragState.ResolveDragStartCell(placePoint);
                _currentPlaceInfos = _placeRunBuilder.Build(dragStartPoint, placePoint, _currentBlockDirection, holdingBlock, out var cellCauses, out var cellBeltReasons, out var climbAbandoned);
                return (cellCauses, cellBeltReasons, climbAbandoned);
            }
```

5. 不要になった `using Client.Game.InGame.UI.Tooltip;` は、ファイル内に他の `TooltipLine` 参照が無ければ削除する。`wc -l` で200行未満を確認する

- [ ] **Step 4: レポーターのテストを書く**

`BeltConveyorFeedbackReporterTest.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// ベルトのツールチップ行の積み方のテスト（ADR 0081 決定7）
    /// Tests for how belt tooltip lines are pushed (ADR 0081 decision 7)
    /// </summary>
    public class BeltConveyorFeedbackReporterTest
    {
        [Test]
        public void 断念したときはカーソル外でも案内を1行出す()
        {
            var feedback = new PlacementFeedback();
            var reasons = new List<BeltConveyorPlacementBlockReason> { BeltConveyorPlacementBlockReason.None };

            BeltConveyorFeedbackReporter.Report(feedback, reasons, -1, true);

            Assert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.PlaceBeltTerrainClimbAbandoned.Key }, feedback.Lines.Select(line => line.Key.Key).ToArray());
        }

        [Test]
        public void 断念していなければ案内は出さない()
        {
            var feedback = new PlacementFeedback();
            var reasons = new List<BeltConveyorPlacementBlockReason> { BeltConveyorPlacementBlockReason.None };

            BeltConveyorFeedbackReporter.Report(feedback, reasons, 0, false);

            Assert.AreEqual(0, feedback.Lines.Count);
        }

        [Test]
        public void カーソルセルの固有理由は従来どおり積む()
        {
            var feedback = new PlacementFeedback();
            var reasons = new List<BeltConveyorPlacementBlockReason> { BeltConveyorPlacementBlockReason.ImpossibleOverpass };

            BeltConveyorFeedbackReporter.Report(feedback, reasons, 0, false);

            Assert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.PlaceBeltOverpassInfeasible.Key }, feedback.Lines.Select(line => line.Key.Key).ToArray());
        }
    }
}
```

- [ ] **Step 5: コンパイルしテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\.(ConveyorOverpass|BeltConveyor)\."`
Expected: ErrorCount 0 / 全PASS。`LocalizationKeys.Ui.Tooltip.PlaceBeltTerrainClimbAbandoned` が未定義でコンパイルが落ちたら、SchemaWatcher が `_CompileRequester.cs` を更新したか `git status` で確かめ、されていなければ `uloop compile` をもう一度実行する

- [ ] **Step 6: コミットする**

```bash
git add Localization/localization.csv moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor
git commit -m "feat: ベルトが地形を乗り越えられず埋めて置くときにツールチップで案内する"
```

---

### Task 3: unityプレイ録画テストで丘越えと断念案内を確かめる

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-terrain-climb-via-ui.cs`

**Interfaces:**
- Consumes: Task 1・2 の挙動、キー `ui.tooltip.placeBeltTerrainClimbAbandoned`
- Produces: 録画（`moorestech_client/PlaytestResults/` 配下）と `result.json`

- [ ] **Step 1: シナリオを書く**

```csharp
// ベルトの地形乗り越え(ADR 0081)の通し検証。Groundレイヤーの箱で丘と崖を作り、UI経路のドラッグで
// (1) 丘の上のセルが持ち上がること (2) 崖で解けないとき地形に埋めて置き、案内行がツールチップに出ることを確かめる
// End-to-end check of belt terrain climbing (ADR 0081): Ground-layer boxes form a hill and a cliff, and UI drags verify
// (1) hill cells are lifted and (2) on an unsolvable cliff the belt is placed buried with the notice line in the tooltip
using Client.Common;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using System.Linq;
using UnityEngine;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("belt-terrain-climb-via-ui", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { SpawnPosition = new Vector3(5f, 33.5f, -4f) });
    await p.SkipOpeningSkit();

    // 丘: x=4..5・z=2 の2セルを覆い上面y=33.4（地形最高点を含むセルは33）。足場上面は32
    // Hill: covers cells x=4..5, z=2 with top y=33.4 (cell containing the max is 33); the scaffold top is 32
    CreateGroundBox("PlaytestBeltHill", new Vector3(5f, 31.7f, 2.5f), new Vector3(2f, 3.4f, 1f));
    // 崖: x=12・z=6 の1セルで上面y=35.4（下限35）。端点32の3セル列では端点へ戻れない
    // Cliff: one cell x=12, z=6 with top y=35.4 (floor 35); a 3-cell run with endpoints at 32 cannot come back down
    CreateGroundBox("PlaytestBeltCliff", new Vector3(12.5f, 32.7f, 6.5f), new Vector3(1f, 5.4f, 1f));
    await UniTask.DelayFrame(5);

    // ---------------------------------------------------------------- 1. 丘を乗り越える
    p.Note("フェーズ1: 丘を横切る東向きドラッグ → 丘の上のセルが y=33 に持ち上がる");
    await p.DragPlaceViaUi("ベルトコンベア", new Vector3Int(2, 32, 2), new Vector3Int(8, 32, 2));
    await p.Until(() => p.GetBlock(new Vector3Int(4, 33, 2)) != null && p.GetBlock(new Vector3Int(5, 33, 2)) != null, 10f, "丘の上のセルが y=33 に置かれる");
    p.Assert(p.GetBlock(new Vector3Int(8, 32, 2)) != null, "終端は地面の高さ y=32 のまま");
    await p.Screenshot("01-belt-over-hill");
    await p.ExitToGameScreen();

    // ---------------------------------------------------------------- 2. 崖で諦めて埋めて置く
    p.Note("フェーズ2: 崖を横切る3セルドラッグ → 埋めて置く案内が出て、y=32 に設置される");
    p.WarpPlayer(new Vector3(12.5f, 33.5f, -3f));
    await p.WaitSeconds(0.5f);
    await p.OpenBuildMenuAndSelectBlock("ベルトコンベア");

    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(11, 32, 6), BlockDirection.East));
    await UniTask.DelayFrame(10);
    SemanticInput.MouseButtonDown(0);
    await UniTask.DelayFrame(5);
    await p.AimAt(PlaytestUiOps.PlaceAimPoint("ベルトコンベア", new Vector3Int(13, 32, 6), BlockDirection.East));
    await UniTask.DelayFrame(10);
    p.Note($"[崖ドラッグ中] tooltip = {Snapshot()}");
    p.Assert(HasKey("ui.tooltip.placeBeltTerrainClimbAbandoned"), "崖を越えられない案内がツールチップに出る");
    await p.Screenshot("02-cliff-notice");
    SemanticInput.MouseButtonUp(0);

    await p.Until(() => p.GetBlock(new Vector3Int(12, 32, 6)) != null, 10f, "崖のセルは地形に埋めて y=32 に置かれる");
    await p.Screenshot("03-belt-buried-in-cliff");
    await p.ExitToGameScreen();

    #region Internal

    void CreateGroundBox(string name, Vector3 center, Vector3 size)
    {
        // 地表探査はGroundレイヤーを見るため、足場と同じレイヤーとマーカーを付ける
        // Ground probing looks at the Ground layer, so give it the same layer and marker as the scaffold
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = center;
        box.transform.localScale = size;
        box.AddComponent<GroundGameObject>();
        box.layer = LayerConst.GroundLayer;
    }

    IMouseCursorTooltip Tooltip()
    {
        return ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMouseCursorTooltip>();
    }

    string Snapshot()
    {
        var presentation = Tooltip().GetPresentation();
        if (!presentation.Visible) return "(hidden)";
        return string.Join(" / ", presentation.Lines.Select(line => line.Key.Key));
    }

    bool HasKey(string key)
    {
        var presentation = Tooltip().GetPresentation();
        return presentation.Visible && presentation.Lines.Any(line => line.Key.Key == key);
    }

    #endregion
});
```

照準メモ: カメラは南から北向き。フェーズ1は (5,_, -4) から見て始点 x=2.5・終点 x=8.5 への視線が z=2 の丘前面を x≈2.7 / x≈8.2 で通過し、丘（x=4..6）に遮られない。フェーズ2は (12.5,_, -3) から見て始点 x=11.5・終点 x=13.5 への視線が崖前面（z=6）を x≈11.55 / x≈13.45 で通過し、崖（x=12..13）に遮られない。照準が遮られて別セルに当たる場合は、`SpawnPosition`／`WarpPlayer` の z を2ずつ南へ下げて再実行する。

- [ ] **Step 2: 調査実行（録画なし）で通ることを確かめる**

```bash
pwd
uloop control-play-mode --project-path ./moorestech_client --action stop
SKILL=.claude/skills/unity-playmode-recorded-playtest
SCRATCH="$PWD/moorestech_client/PlaytestResults/scratch"
mkdir -p "$SCRATCH"
sed 's/Record = true/Record = false/' "$SKILL/scenarios/building/belt-terrain-climb-via-ui.cs" > "$SCRATCH/belt-terrain-climb-via-ui.cs"
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SCRATCH/belt-terrain-climb-via-ui.cs"
```

Expected: `result.json` の `Success: true`、Asserts 全通過。失敗したら `Note` に出た tooltip と `uloop get-logs --project-path ./moorestech_client --log-type Error` を読み、照準遮蔽（照準メモ）か実装不具合かを切り分ける。実装不具合なら Task 1/2 へ戻って直す

- [ ] **Step 3: 提出用の録画実行をする**

`Record = true` のまま同じ手順で実行し（`cp "$SKILL/scenarios/building/belt-terrain-climb-via-ui.cs" "$SCRATCH/"`）、動画とスクショ `01-belt-over-hill`（丘の上でベルトが坂で登って越えている）・`02-cliff-notice`（案内行が見える）・`03-belt-buried-in-cliff`（崖に埋まって置かれている）を目視で確かめる。実行後チェックリストは `references/run-scenario.md` に従う

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-terrain-climb-via-ui.cs
git commit -m "test: ベルトの地形乗り越えのunityプレイ録画テストシナリオを追加"
```

---

### Task 4: 全ブランチレビュー

- [ ] **Step 1: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

レビュー指摘の反映が `ConveyorOverpassRaiser` の判定経路・条件式・評価時点に触れたら、Task 3 の unityプレイ録画テストを反映後のバイナリで再実施してから完了とする。

### Task 5: PR作成とセッション終了可能状態

- [ ] **Step 1: セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える**

PR本文に、ADR 0081 と `.decisions/2026-10-15-ベルト*` の裁定記録へのリンクを載せる。

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0081-belt-drag-climbs-over-terrain.md`（決定1〜7はユーザー裁定。裁定記録は `.decisions/2026-10-15-ベルトの地形迂回はL字のまま上下に乗り越える.md` / `…谷には降りず水平を保つ.md` / `…解けないときは地形を無視した経路に戻す.md` / `2026-10-15-坂ベルト手持ちのドラッグには地形乗り越えを効かせない.md` / `…中間セル高さは端点と同じ地形最高点を含むセル.md` / `…高さ最適化は既存高架と同じ規則.md` / `…諦めたときは設置を妨げない案内を出す.md`）
- 案内を出す条件は「地形なしで解き直した結果が設置可能なとき」に絞る。地形なしでも解けないときは既存の `ImpossibleOverpass` が赤で出るため、「埋めて設置します」と矛盾する案内を重ねない。出所: agent前提（planning）
- 端点セルには地形下限を付けない。端点Yはプレイヤーが照準・Q/Eで決めた値で、Q/Eで下げた端点に地形下限を当てると毎回断念へ落ちるため。出所: agent前提（planning）
- 既存ブロックの走査は地形で持ち上げた基準から行う。地形なしの基準から走査すると丘の上の既存ブロックを見落とし、持ち上げたセルが既存ブロックと重なって赤になるため。出所: agent前提（planning）
- 地形の問い合わせは interface `IConveyorTerrainFloor`（Common/ConveyorOverpass）で注入し、地表探査の本番実装は `Ground/` に置く（前例: `Common/IPlacementGroundFollower` と `Ground/PlacementGroundFollowStep`）（`Func<>` 新設禁止・`ConveyorVerticalEnvelope` を純粋ロジックのまま保つ。テストは `FakeConveyorTerrainFloor` で地形を与える）。出所: agent前提（planning）
- `BeltConveyorPlaceSystem` が196行のため、ベルト固有理由の local 関数 `PushBeltReason` を `BeltConveyorFeedbackReporter` へ移して200行未満を保つ。出所: agent前提（planning）
- unityプレイ録画テストを含める（Task 3）。地表探査（Physics）→ソルバー→プレビュー→送信→ツールチップの通し経路はユニットテストでは届かないため。地形は Ground レイヤーの箱で作り、生成マップの起伏に依存しない決定的な配置にする。出所: agent前提（planning）
