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

        // 起点セルに既設ファミリーブロックがあれば張替え起点。天面ヒットで1段浮いた座標は直下も見るが、非ベルトが埋まっているセルは貫通させない
        // The cell is a replace origin when it holds a family block, or, for a top-face hit floated one step up, when the empty cell directly above one does
        public static bool TryResolveOrigin(BlockGameObjectDataStore store, Vector3Int cell, out Vector3Int originCell)
        {
            originCell = cell;
            if (TryGetFamilyBlock(store, cell, out _, out _)) return true;

            // 他ブロックが居るセルは天面ヒットの浮きではないので、直下のベルトを起点に取らない
            // A cell holding another block is not a floated top-face hit, so the belt below must not become the origin
            if (store.ContainsBlockGameObject(cell)) return false;

            var below = cell + Vector3Int.down;
            if (!TryGetFamilyBlock(store, below, out _, out _)) return false;

            originCell = below;
            return true;
        }

        public List<PlaceInfo> Build(Vector3Int originCell, Vector3Int cursorCell, bool isStartDirectionZ, BeltConveyorHoldingBlock holdingBlock, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons)
        {
            // ローカル関数はout引数を捕捉できないため、内部はローカルの列へ積んで最後に受け渡す
            // Local functions cannot capture out parameters, so build into local columns and hand them over at the end
            var result = new List<PlaceInfo>();
            var cellCauses = new List<PlacementBlockCause>();
            var cellBeltReasons = new List<BeltConveyorPlacementBlockReason>();
            blockCauses = cellCauses;
            beltReasons = cellBeltReasons;

            // XZ経路はカーソルのYを無視して起点の高さで作り、Yは既設に追従させる
            // Build the XZ path at the origin's height ignoring the cursor's Y, then follow the existing blocks' Y
            var flatCursor = new Vector3Int(cursorCell.x, originCell.y, cursorCell.z);
            var (xzCells, _) = BeltConveyorPositionListBuilder.BuildHorizontalPositions(originCell, flatCursor, isStartDirectionZ);

            var currentY = originCell.y;
            foreach (var xz in xzCells)
            {
                if (!TryFindFamilyBlockNear(xz.x, xz.z, currentY, out var existing, out var existingRole)) continue;
                currentY = existing.BlockPosInfo.OriginalPos.y;
                AppendCell(existing, existingRole);
            }

            return result;

            #region Internal

            void AppendCell(BlockGameObject existing, BeltConveyorRole existingRole)
            {
                // 手持ちファミリーに同ロールが無ければ不可色で止め、同ブロックならno-opとして出さない
                // Stop with the unplaceable color when the held family lacks the role; omit same-block cells as no-ops
                var hasTargetBlock = holdingBlock.Family.TryGetBlockIdOfRole(existingRole, out var targetBlockId);
                if (hasTargetBlock && targetBlockId == existing.BlockId) return;

                result.Add(new PlaceInfo
                {
                    Position = existing.BlockPosInfo.OriginalPos,
                    Direction = existing.BlockPosInfo.BlockDirection,
                    VerticalDirection = ToVerticalDirection(existingRole),
                    IsReplace = true,
                    BlockId = hasTargetBlock ? targetBlockId : existing.BlockId,
                    Placeable = hasTargetBlock,
                });
                cellCauses.Add(PlacementBlockCause.None);
                cellBeltReasons.Add(hasTargetBlock ? BeltConveyorPlacementBlockReason.None : BeltConveyorPlacementBlockReason.ReplaceRoleMissing);
            }

            bool TryFindFamilyBlockNear(int x, int z, int y, out BlockGameObject existing, out BeltConveyorRole role)
            {
                // ベルトの坂は毎セル±1なので、直前の高さ・1つ上・1つ下の順に探す
                // Belt slopes change one step per cell, so search the previous height, one above, then one below
                if (TryGetFamilyBlock(_blockGameObjectDataStore, new Vector3Int(x, y, z), out existing, out role)) return true;
                if (TryGetFamilyBlock(_blockGameObjectDataStore, new Vector3Int(x, y + 1, z), out existing, out role)) return true;
                return TryGetFamilyBlock(_blockGameObjectDataStore, new Vector3Int(x, y - 1, z), out existing, out role);
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

        // ファミリー所属かつロールを引けたブロックだけを既設として扱う
        // Only a block that belongs to a family and resolves a role counts as an existing belt
        private static bool TryGetFamilyBlock(BlockGameObjectDataStore store, Vector3Int cell, out BlockGameObject existing, out BeltConveyorRole role)
        {
            role = BeltConveyorRole.Straight;
            if (!store.TryGetBlockGameObject(cell, out existing)) return false;
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(existing.BlockId, out var family)) return false;
            return family.TryGetRole(existing.BlockId, out role);
        }
    }
}
