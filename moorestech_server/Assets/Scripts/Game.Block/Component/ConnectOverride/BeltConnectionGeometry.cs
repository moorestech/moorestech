using System.Collections.Generic;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Game.Block.Component.ConnectOverride
{
    internal static class BeltConnectionGeometry
    {
        internal static bool IsEligible(BlockPositionInfo position, InventoryConnects connectors)
        {
            if (position.BlockSize != Vector3Int.one || position.BlockDirection < BlockDirection.North ||
                BlockDirection.West < position.BlockDirection) return false;
            return Valid(connectors.InputConnects) && Valid(connectors.OutputConnects);

            #region Internal

            bool Valid(IReadOnlyList<IBlockConnector> portDefinitions)
            {
                if (portDefinitions == null) return true;
                foreach (var connector in portDefinitions)
                {
                    if (connector.Offset != Vector3Int.zero || connector.Directions == null) return false;
                    foreach (var direction in connector.Directions)
                        if (Mathf.Abs(direction.x) + Mathf.Abs(direction.z) != 1 ||
                            1 < Mathf.Abs(direction.y))
                            return false;
                }
                return true;
            }

            #endregion
        }

        internal static List<BeltConnectionPort> Create(BlockPositionInfo position,
            BeltConveyorSlopeType slope, IReadOnlyList<IBlockConnector> connectors, bool output)
        {
            var result = new List<BeltConnectionPort>();
            if (connectors == null) return result;
            var height = slope switch
            {
                BeltConveyorSlopeType.Up => output ? 1 : 0,
                BeltConveyorSlopeType.Down => output ? 0 : 1,
                _ => 0
            };
            foreach (var connector in connectors)
            foreach (var direction in connector.Directions)
            {
                var localNormal = new Vector3Int(direction.x, 0, direction.z);
                var normal = position.BlockDirection.ConvertLocalCell(localNormal);
                var offset = new Vector3Int(direction.x, 2 * height - 1, direction.z);
                var boundary = position.OriginalPos * 2 + Vector3Int.one +
                               position.BlockDirection.ConvertLocalCell(offset);
                var port = new BeltConnectionPort(position.OriginalPos, slope, connector, boundary, normal);
                // 高さだけ違う方向指定は同じ物理面として一度だけ扱う。
                // Direction entries at different heights name the same physical face.
                var duplicate = false;
                foreach (var existing in result)
                    if (existing.Boundary == boundary && existing.OutwardNormal == normal &&
                        ReferenceEquals(existing.Connector, connector)) duplicate = true;
                if (!duplicate) result.Add(port);
            }
            return result;
        }
    }
}
