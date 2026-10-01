using System.Collections.Generic;

namespace Core.BeltTransport
{
    public sealed class BeltTransportPath
    {
        public readonly BeltNetworkCell[] Cells;
        public readonly BeltConveyorSegment Segment;
        internal readonly List<BeltNetworkConnection> Outputs;

        internal BeltTransportPath(BeltNetworkCell[] cells, BeltSegmentKind kind, int priority, BeltDirection forward,
            List<BeltNetworkConnection> outputs)
        {
            Cells = cells;
            Segment = new BeltConveyorSegment(cells.Length, cells[0].Speed, kind, priority, forward);
            Outputs = outputs;
        }

        internal int IndexOf(int cellId)
        {
            for (int i = 0; i < Cells.Length; i++) if (Cells[i].Id == cellId) return i;
            return -1;
        }

        internal int Distance(int cellId, int progress) => (Cells.Length - IndexOf(cellId)) * BeltConstants.ItemWidth - progress;
    }
}
