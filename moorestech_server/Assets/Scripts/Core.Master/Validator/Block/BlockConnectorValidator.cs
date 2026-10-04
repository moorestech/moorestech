using System;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    internal static class BlockConnectorValidator
    {
        internal static string ValidateSettings(Blocks blocks)
        {
            // 互換ペアの参照先形状の実在を検証（foreignKeyは自動生成されないため手動確認）
            // Validate pair references exist (foreignKey validation is not auto-generated)
            var logs = "";
            var connectableShapePairs = blocks.ConnectorSettings?.ConnectableShapePairs;
            if (connectableShapePairs == null) return logs;
            foreach (var pair in connectableShapePairs)
            {
                if (!ExistsConnectorShape(blocks, pair.Shape0)) logs += $"[BlockMaster] ConnectableShapePair has invalid Shape0:{pair.Shape0}\n";
                if (!ExistsConnectorShape(blocks, pair.Shape1)) logs += $"[BlockMaster] ConnectableShapePair has invalid Shape1:{pair.Shape1}\n";
            }
            return logs;
        }

        internal static string ValidateShapeGuids(Blocks blocks)
        {
            // コネクタに設定されたshapeGuidの実在を検証（fluid側は形状運用開始時に追加する）
            // Validate shapeGuid on connectors (fluid-side check to be added when fluids adopt shapes)
            var logs = "";
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is IGearConnectors gearConnectors)
                    foreach (var connector in gearConnectors.Gear.GearConnects)
                        logs += ValidateConnectorShapeGuid(block.Name, connector.ShapeGuid);

                if (block.BlockParam is IInventoryConnectors inventoryConnectors)
                {
                    var connects = inventoryConnectors.InventoryConnectors;
                    if (connects.InputConnects != null)
                        foreach (var connector in connects.InputConnects)
                            logs += ValidateConnectorShapeGuid(block.Name, connector.ShapeGuid);
                    if (connects.OutputConnects != null)
                        foreach (var connector in connects.OutputConnects)
                            logs += ValidateConnectorShapeGuid(block.Name, connector.ShapeGuid);
                }
            }
            return logs;

            #region Internal

            string ValidateConnectorShapeGuid(string blockName, Guid? shapeGuid)
            {
                if (shapeGuid == null || ExistsConnectorShape(blocks, shapeGuid.Value)) return "";
                return $"[BlockMaster] Name:{blockName} has invalid connector ShapeGuid:{shapeGuid}\n";
            }
            #endregion
        }

        internal static string ValidateMeshingAxes(Blocks blocks)
        {
            // 歯車コネクタの噛み合い軸が軸整列単位ベクトルであることを検証する
            // Validate that gear connector meshing axes are axis-aligned unit vectors
            var logs = "";
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is not IGearConnectors gearConnectors) continue;
                foreach (var connector in gearConnectors.Gear.GearConnects)
                {
                    if (!connector.Option.MeshingAxis.HasValue) continue;
                    var axis = connector.Option.MeshingAxis.Value;
                    if (IsAxisAlignedUnitVector(axis)) continue;
                    logs += $"[BlockMaster] Name:{block.Name} has invalid meshingAxis:{axis} (must be an axis-aligned unit vector, e.g. (0,0,1))\n";
                }
            }
            return logs;

            #region Internal

            bool IsAxisAlignedUnitVector(UnityEngine.Vector3Int axis)
            {
                var unitComponentCount = 0;
                if (Math.Abs(axis.x) == 1) unitComponentCount++;
                if (Math.Abs(axis.y) == 1) unitComponentCount++;
                if (Math.Abs(axis.z) == 1) unitComponentCount++;
                return unitComponentCount == 1 && Math.Abs(axis.x) <= 1 && Math.Abs(axis.y) <= 1 && Math.Abs(axis.z) <= 1;
            }
            #endregion
        }

        private static bool ExistsConnectorShape(Blocks blocks, Guid shapeGuid)
        {
            var connectorShapes = blocks.ConnectorSettings?.ConnectorShapes;
            return connectorShapes != null && Array.Exists(connectorShapes, s => s.ShapeGuid == shapeGuid);
        }
    }
}
