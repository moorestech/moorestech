using System.Collections.Generic;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    internal static class MachineInventoryEdgePorts
    {
        private static readonly Vector3Int[] HorizontalDirections =
            { Vector3Int.right, Vector3Int.left, Vector3Int.forward, Vector3Int.back };

        internal static List<BeltEdge> GetEdges(InventoryConnects ports, BlockPositionInfo position)
        {
            var edges = new List<BeltEdge>();
            AddPorts(ports.InputConnects, true);
            AddPorts(ports.OutputConnects, false);
            return edges;

            #region Internal
            void AddPorts(IEnumerable<IBlockConnector> connectors, bool isInput)
            {
                if (connectors == null) return;
                foreach (var port in connectors)
                foreach (var outward in GetWorldDirections(port, position, isInput))
                {
                    var cell = position.ConvertBlockLocalToWorldCell(port.Offset);
                    // 外向きの側面だけが隣接ベルトと共有edgeを作る
                    // Only outward side faces form shared edges with adjacent belts
                    if (!IsExposedFace(position, cell, outward))
                    {
                        if (port.Directions != null || !Contains(position, cell))
                            Debug.Log($"Inventory port has no external horizontal face: {port.ConnectorGuid} at {cell}, direction {outward}.");
                        continue;
                    }
                    var edge = new BeltEdge(cell, outward, cell.y);
                    if (!edges.Contains(edge)) edges.Add(edge);
                }
            }
            #endregion
        }

        internal static bool FacesEdge(IBlockConnector port, BlockPositionInfo position, BeltEdge edge, Vector3Int outward, bool isInput)
        {
            var cell = position.ConvertBlockLocalToWorldCell(port.Offset);
            // 原点以外のportも、その面の下辺で照合する
            // Match non-origin ports at the lower horizontal edge of their own face
            if (!IsExposedFace(position, cell, outward) || !new BeltEdge(cell, outward, cell.y).Equals(edge)) return false;
            foreach (var direction in GetWorldDirections(port, position, isInput))
                if (direction == outward) return true;
            return false;
        }

        private static IEnumerable<Vector3Int> GetWorldDirections(IBlockConnector port, BlockPositionInfo position, bool isInput)
        {
            // 無制限入力は全側面、方向未定義の出力は搬出口なし
            // Unrestricted inputs cover all side faces; unspecified outputs have no emitting face
            if (port.Directions == null)
            {
                if (isInput)
                    foreach (var direction in HorizontalDirections) yield return direction;
                yield break;
            }
            foreach (var direction in port.Directions)
                yield return position.BlockDirection.ConvertLocalCell(direction);
        }

        private static bool IsExposedFace(BlockPositionInfo position, Vector3Int cell, Vector3Int outward)
        {
            return outward.y == 0 && Mathf.Abs(outward.x) + Mathf.Abs(outward.z) == 1 &&
                Contains(position, cell) && !Contains(position, cell + outward);
        }

        private static bool Contains(BlockPositionInfo position, Vector3Int cell) =>
            position.MinPos.x <= cell.x && cell.x <= position.MaxPos.x &&
            position.MinPos.y <= cell.y && cell.y <= position.MaxPos.y &&
            position.MinPos.z <= cell.z && cell.z <= position.MaxPos.z;
    }
}
