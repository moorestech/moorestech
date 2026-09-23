using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;

namespace Game.Block.Component.ConnectOverride
{
    internal static class BeltConnectionSelector
    {
        internal static bool TrySelect(IReadOnlyList<BeltConnectionPort> outputs,
            IReadOnlyList<BeltConnectionPort> inputs, out BeltConnectionPort output,
            out BeltConnectionPort input)
        {
            output = default;
            input = default;
            if (outputs.Count == 0 || inputs.Count == 0) return false;
            var selectedOutput = outputs[0];
            foreach (var port in outputs)
                if (OutputPriority(selectedOutput.Slope) < OutputPriority(port.Slope)) selectedOutput = port;
            var selectedInput = inputs[0];
            foreach (var port in inputs)
                if (InputPriority(selectedInput.Slope) < InputPriority(port.Slope)) selectedInput = port;
            if (selectedOutput.Slope == BeltConveyorSlopeType.Down && selectedInput.Slope == BeltConveyorSlopeType.Up ||
                selectedOutput.Slope == BeltConveyorSlopeType.Up && selectedInput.Slope == BeltConveyorSlopeType.Down)
                return false;
            return TrySelectCompatiblePair(outputs, inputs, selectedOutput.OwnerCell,
                selectedInput.OwnerCell, out output, out input);

            #region Internal

            bool TrySelectCompatiblePair(IReadOnlyList<BeltConnectionPort> outputPorts,
                IReadOnlyList<BeltConnectionPort> inputPorts, UnityEngine.Vector3Int outputOwner,
                UnityEngine.Vector3Int inputOwner, out BeltConnectionPort compatibleOutput,
                out BeltConnectionPort compatibleInput)
            {
                compatibleOutput = default;
                compatibleInput = default;
                foreach (var outputPort in outputPorts)
                {
                    if (outputPort.OwnerCell != outputOwner) continue;
                    foreach (var inputPort in inputPorts)
                    {
                        if (inputPort.OwnerCell != inputOwner) continue;
                        if (!MasterHolder.BlockMaster.CanConnectConnectorShapes(
                                outputPort.Connector.ShapeGuid, inputPort.Connector.ShapeGuid)) continue;
                        compatibleOutput = outputPort;
                        compatibleInput = inputPort;
                        return true;
                    }
                }
                return false;
            }

            int OutputPriority(BeltConveyorSlopeType slope)
            {
                return slope switch
                {
                    BeltConveyorSlopeType.Down => 3,
                    BeltConveyorSlopeType.Straight => 2,
                    _ => 1
                };
            }

            int InputPriority(BeltConveyorSlopeType slope)
            {
                return slope switch
                {
                    BeltConveyorSlopeType.Up => 3,
                    BeltConveyorSlopeType.Straight => 2,
                    _ => 1
                };
            }

            #endregion
        }
    }
}
