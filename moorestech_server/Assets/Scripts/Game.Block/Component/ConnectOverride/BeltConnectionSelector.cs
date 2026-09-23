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
            output = outputs[0];
            foreach (var port in outputs)
                if (OutputPriority(port.Slope) > OutputPriority(output.Slope)) output = port;
            input = inputs[0];
            foreach (var port in inputs)
                if (InputPriority(port.Slope) > InputPriority(input.Slope)) input = port;
            if (output.Slope == BeltConveyorSlopeType.Down && input.Slope == BeltConveyorSlopeType.Up ||
                output.Slope == BeltConveyorSlopeType.Up && input.Slope == BeltConveyorSlopeType.Down)
                return false;
            return MasterHolder.BlockMaster.CanConnectConnectorShapes(
                output.Connector.ShapeGuid, input.Connector.ShapeGuid);
        }

        private static int OutputPriority(BeltConveyorSlopeType slope)
        {
            return slope switch
            {
                BeltConveyorSlopeType.Down => 3,
                BeltConveyorSlopeType.Straight => 2,
                _ => 1
            };
        }

        private static int InputPriority(BeltConveyorSlopeType slope)
        {
            return slope switch
            {
                BeltConveyorSlopeType.Up => 3,
                BeltConveyorSlopeType.Straight => 2,
                _ => 1
            };
        }
    }
}
