using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Game.Block.Component.ConnectOverride
{
    internal sealed class BeltConnectionOverride : IConnectorConnectionOverride<IBlockInventory>
    {
        private readonly BlockPositionInfo _position;
        private readonly BeltConveyorSlopeType _slope;
        private readonly InventoryConnects _connectors;
        private readonly bool _eligible;
        public IReadOnlyList<Vector3Int> ObservationPositions { get; }

        internal BeltConnectionOverride(BlockPositionInfo position, BeltConveyorSlopeType slope,
            InventoryConnects connectors)
        {
            _position = position;
            _slope = slope;
            _connectors = connectors;
            _eligible = BeltConnectionGeometry.IsEligible(position, connectors);
            if (!_eligible)
            {
                Debug.Log($"Belt connection override outside ruled domain at {position.OriginalPos}, direction {position.BlockDirection}");
                ObservationPositions = new Vector3Int[0];
                return;
            }
            var observed = new HashSet<Vector3Int>();
            var self = position.OriginalPos;
            for (var dy = -1; dy <= 1; dy++) observed.Add(self + Vector3Int.up * dy);
            foreach (var port in BeltConnectionGeometry.Create(position, slope, connectors.OutputConnects, true))
                for (var dy = -1; dy <= 1; dy++)
                    observed.Add(self + port.OutwardNormal + Vector3Int.up * dy);
            ObservationPositions = new List<Vector3Int>(observed);
        }

        public void ApplyTo(Dictionary<IBlockInventory, ConnectedInfo> connectedTargets)
        {
            if (!_eligible) return;
            // 置換対象のベルコン宛てだけを消し、機械と対象外の接続は保持する。
            // Only eligible belt targets participate in this replacement.
            var remove = new List<IBlockInventory>();
            foreach (var pair in connectedTargets)
                if (TryDescribe(pair.Value.TargetBlock, out _, out _)) remove.Add(pair.Key);
            foreach (var target in remove) connectedTargets.Remove(target);

            var selfCell = _position.OriginalPos;
            var selfPorts = BeltConnectionGeometry.Create(_position, _slope, _connectors.OutputConnects, true);
            var handled = new HashSet<(Vector3Int, Vector3Int)>();
            foreach (var selfPort in selfPorts)
            {
                if (!handled.Add((selfPort.Boundary, selfPort.OutwardNormal))) continue;
                var outputs = new List<BeltConnectionPort>();
                var inputs = new List<BeltConnectionPort>();
                CollectColumn(selfCell, selfPort.Boundary, selfPort.OutwardNormal, true, outputs);
                CollectColumn(selfCell + selfPort.OutwardNormal, selfPort.Boundary,
                    -selfPort.OutwardNormal, false, inputs);
                if (!BeltConnectionSelector.TrySelect(outputs, inputs, out var output, out var input) ||
                    output.OwnerCell != selfCell) continue;
                var targetBlock = ServerContext.WorldBlockDatastore.GetBlock(input.OwnerCell);
                if (targetBlock == null)
                    throw new InvalidOperationException($"Selected belt target is missing at {input.OwnerCell}");
                if (!targetBlock.TryGetComponent<IBlockInventory>(out var inventory))
                    throw new InvalidOperationException($"Selected belt target has no inventory at {input.OwnerCell}");
                connectedTargets[inventory] = new ConnectedInfo(output.Connector, input.Connector, targetBlock);
            }

            #region Internal

            void CollectColumn(Vector3Int column, Vector3Int boundary, Vector3Int normal,
                bool output, List<BeltConnectionPort> result)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var cell = column + Vector3Int.up * dy;
                    var block = cell == _position.OriginalPos
                        ? null : ServerContext.WorldBlockDatastore.GetBlock(cell);
                    BlockPositionInfo position;
                    BeltConveyorSlopeType slope;
                    InventoryConnects connectors;
                    if (cell == _position.OriginalPos)
                    {
                        position = _position;
                        slope = _slope;
                        connectors = _connectors;
                    }
                    else if (!TryDescribe(block, out slope, out connectors))
                        continue;
                    else
                        position = block.BlockPositionInfo;

                    IReadOnlyList<IBlockConnector> definitions = output
                        ? connectors.OutputConnects : connectors.InputConnects;
                    foreach (var port in BeltConnectionGeometry.Create(position, slope, definitions, output))
                        if (port.Boundary == boundary && port.OutwardNormal == normal) result.Add(port);
                }
            }

            bool TryDescribe(IBlock block, out BeltConveyorSlopeType slope,
                out InventoryConnects connectors)
            {
                slope = default;
                connectors = null;
                if (block == null || !block.TryGetComponent<SegmentBeltComponent>(out var belt)) return false;
                var param = MasterHolder.BlockMaster.GetBlockMaster(block.BlockGuid).BlockParam;
                if (param is not IInventoryConnectors inventoryConnectors) return false;
                connectors = inventoryConnectors.InventoryConnectors;
                if (connectors == null || !BeltConnectionGeometry.IsEligible(block.BlockPositionInfo, connectors))
                    return false;
                slope = belt.SlopeType;
                return true;
            }

            #endregion
        }
    }
}
