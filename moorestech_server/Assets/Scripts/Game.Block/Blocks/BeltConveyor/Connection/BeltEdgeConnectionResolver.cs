using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    internal static class BeltEdgeConnectionResolver
    {
        internal static void Resolve(IWorldBlockDatastore world, BeltEdge edge, List<BeltEdgeConnection> connections)
        {
            // 接触する上側を先に選び、不成立でも下側へ戻らない
            // Select touching upper blocks first; an invalid pair never falls back to lower blocks
            var negative = Select(false);
            var positive = Select(true);
            if (negative == null || positive == null) return;
            if (TryConnect(negative, positive, edge.Normal)) return;
            TryConnect(positive, negative, -edge.Normal);

            #region Internal
            IBlock Select(bool positiveSide)
            {
                var upper = edge.UpperCell(positiveSide);
                foreach (var cell in new[] { upper, upper + Vector3Int.down })
                {
                    var block = world.GetBlock(cell);
                    if (block == null || !BeltInventoryConnectionContext.TryGetContext(block, out var context)) continue;
                    if (context.Edges.Contains(edge)) return block;
                }
                return null;
            }

            bool TryConnect(IBlock source, IBlock target, Vector3Int outward)
            {
                BeltInventoryConnectionContext.TryGetContext(source, out var sourceContext);
                BeltInventoryConnectionContext.TryGetContext(target, out var targetContext);
                // 機械同士の接続は既存経路だけが所有する
                // Leave machine-to-machine connections exclusively owned by the existing path
                if (!sourceContext.IsBelt && !targetContext.IsBelt) return false;
                // 山と谷は接触しても搬送面が連続しない
                // Peaks and valleys do not form a continuous transport surface
                if (sourceContext.Slope == BeltConveyorSlopeType.Up && targetContext.Slope == BeltConveyorSlopeType.Down ||
                    sourceContext.Slope == BeltConveyorSlopeType.Down && targetContext.Slope == BeltConveyorSlopeType.Up) return false;

                // 未定義portは搬送口なし、方向未定義は入力だけ全方向を許可する
                // Missing ports mean no transport port; unrestricted directions apply only to inputs
                if (sourceContext.Outputs == null || targetContext.Inputs == null) return false;
                foreach (var output in sourceContext.Outputs)
                {
                    if (output.Directions == null || !FacesEdge(output, sourceContext, edge, outward, false)) continue;
                    foreach (var input in targetContext.Inputs)
                    {
                        if (!FacesEdge(input, targetContext, edge, -outward, true)) continue;
                        if (!MasterHolder.BlockMaster.CanConnectConnectorShapes(output.ShapeGuid, input.ShapeGuid)) continue;
                        connections.Add(new BeltEdgeConnection(source, target, output, input));
                        return true;
                    }
                }
                return false;
            }
            #endregion
        }

        private static bool FacesEdge(IBlockConnector port, BeltInventoryConnectionContext context, BeltEdge edge, Vector3Int outward, bool isInput)
        {
            if (!context.IsBelt) return MachineInventoryEdgePorts.FacesEdge(port, context.Position, edge, outward, isInput);
            var position = context.Position;
            // 高さは実形状端点で確定済み。旧セル指定のyではなく水平の入出力方向を読む
            // Physical endpoints already establish height; read horizontal flow rather than the old cell-address y
            if (position.ConvertBlockLocalToWorldCell(port.Offset) != position.OriginalPos) return false;
            if (port.Directions == null) return true;
            foreach (var localDirection in port.Directions)
            {
                var direction = position.BlockDirection.ConvertLocalCell(localDirection);
                if (direction.x == outward.x && direction.z == outward.z) return true;
            }
            return false;
        }
    }
}
