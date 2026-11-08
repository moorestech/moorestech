using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     既存ブロックの搬入口・搬出口を、サーバーのBeltEdgeEndpoint/MachineInventoryEdgePortsと同じ面と高さで箱へ書く
    ///     Writes an existing block's input and output ports into the box as the same faces and heights the server's BeltEdgeEndpoint/MachineInventoryEdgePorts use
    /// </summary>
    public static class BeltRouteBlockEdgeScanner
    {
        private static readonly BlockDirection[] AllDirections = { BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West };

        public static void Write(BlockGameObject block, BeltRouteGrid grid)
        {
            Write(block.BlockMasterElement, block.BlockId, block.BlockPosInfo, grid);
        }

        // サーバーのブロックからも同じ辺を書けるよう、マスタと位置だけを受ける
        // Takes only the master and position so server-side blocks (tests) write the same edges
        public static void Write(BlockMasterElement master, BlockId blockId, BlockPositionInfo position, BeltRouteGrid grid)
        {
            if (master.BlockParam is not IInventoryConnectors connectors) return;

            if (IsBelt(master))
            {
                WriteBelt(blockId, position, connectors.InventoryConnectors, grid);
                return;
            }
            WriteMachine(position, connectors.InventoryConnectors, grid);
        }

        public static bool IsBelt(BlockMasterElement master)
        {
            return master.BlockType == BlockMasterElement.BlockTypeConst.BeltConveyor || master.BlockType == BlockMasterElement.BlockTypeConst.GearBeltConveyor;
        }

        // 坂は搬送軸の両端だけ。上りは前が1段上、下りは後ろが1段上。真上のセルは他のブロックに取られると坂が切れる
        // Slopes touch only their two transport ends: an up slope's front is one level up, a down slope's back is; the cell above must stay free
        private static void WriteBelt(BlockId blockId, BlockPositionInfo position, InventoryConnects ports, BeltRouteGrid grid)
        {
            var cell = position.OriginalPos;
            var facing = position.BlockDirection;
            var slope = ResolveSlope(blockId);
            if (slope != BlockVerticalDirection.Horizontal)
            {
                var upperIsFront = slope == BlockVerticalDirection.Up;
                grid.AddInputEdge(BeltRouteEdge.FromFace(cell, BeltRouteDirection.Opposite(facing), cell.y + (upperIsFront ? 0 : 1)));
                grid.AddOutputEdge(BeltRouteEdge.FromFace(cell, facing, cell.y + (upperIsFront ? 1 : 0)));
                grid.MarkSlopeHead(cell + Vector3Int.up);
                return;
            }

            // 水平ベルトはマスタの入出力方向を回転して底面四辺のうち該当する面に辺を書く
            // A flat belt rotates its master port directions and writes edges on the matching bottom faces
            WriteFlatBeltPorts(ports.InputConnects, true);
            WriteFlatBeltPorts(ports.OutputConnects, false);

            #region Internal

            void WriteFlatBeltPorts(IBlockConnector[] connectors, bool isInput)
            {
                if (connectors == null) return;
                foreach (var port in connectors)
                {
                    foreach (var direction in WorldDirections(port, facing, isInput))
                    {
                        var edge = BeltRouteEdge.FromFace(cell, direction, cell.y);
                        if (isInput) grid.AddInputEdge(edge);
                        else grid.AddOutputEdge(edge);
                    }
                }
            }

            #endregion
        }

        // 機械は外向きの側面にある口だけが辺になる。縦向きや内向きの口は機械同士用なので飛ばす
        // Only ports on an outward side face of a machine form edges; vertical or inward ports serve machine-to-machine links and are skipped
        private static void WriteMachine(BlockPositionInfo position, InventoryConnects ports, BeltRouteGrid grid)
        {
            WriteMachinePorts(ports.InputConnects, true);
            WriteMachinePorts(ports.OutputConnects, false);

            #region Internal

            void WriteMachinePorts(IBlockConnector[] connectors, bool isInput)
            {
                if (connectors == null) return;
                foreach (var port in connectors)
                {
                    var cell = position.ConvertBlockLocalToWorldCell(port.Offset);
                    foreach (var outward in WorldDirections(port, position.BlockDirection, isInput))
                    {
                        if (!IsExposedFace(position, cell, outward)) continue;
                        var edge = BeltRouteEdge.FromFace(cell, outward, cell.y);
                        if (isInput) grid.AddInputEdge(edge);
                        else grid.AddOutputEdge(edge);
                    }
                }
            }

            #endregion
        }

        // 方向未定義の入口は全側面、方向未定義の出口は搬出口なし（サーバーと同じ規則）
        // Undefined input directions mean every side face; undefined output directions mean no emitting face (same rule as the server)
        private static System.Collections.Generic.IEnumerable<BlockDirection> WorldDirections(IBlockConnector port, BlockDirection facing, bool isInput)
        {
            if (port.Directions == null)
            {
                if (!isInput) yield break;
                foreach (var direction in AllDirections) yield return direction;
                yield break;
            }
            foreach (var localDirection in port.Directions)
            {
                var world = facing.ConvertLocalCell(localDirection);
                // 鉛直成分は面の選択に使わない。水平の単位ベクトルだけが側面を指す
                // The vertical component never selects a face; only a horizontal unit vector names a side
                if (Mathf.Abs(world.x) + Mathf.Abs(world.z) != 1) continue;
                yield return BeltRouteDirection.FromStep(new Vector3Int(world.x, 0, world.z));
            }
        }

        private static bool IsExposedFace(BlockPositionInfo position, Vector3Int cell, BlockDirection outward)
        {
            return Contains(position, cell) && !Contains(position, cell + BeltRouteDirection.Forward(outward));
        }

        private static bool Contains(BlockPositionInfo position, Vector3Int cell)
        {
            return position.MinPos.x <= cell.x && cell.x <= position.MaxPos.x &&
                   position.MinPos.y <= cell.y && cell.y <= position.MaxPos.y &&
                   position.MinPos.z <= cell.z && cell.z <= position.MaxPos.z;
        }

        private static BlockVerticalDirection ResolveSlope(BlockId blockId)
        {
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var family)) return BlockVerticalDirection.Horizontal;
            family.TryGetSlopeDirection(blockId, out var direction);
            return direction;
        }
    }
}
