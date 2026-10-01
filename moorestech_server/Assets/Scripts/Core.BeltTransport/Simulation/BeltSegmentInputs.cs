// MyBeltConvSegmentのCPU実装をムアステ向けに変更。
// Adapted from MyBeltConvSegment CPU implementation; see LICENSE.txt.
namespace Core.BeltTransport
{
    internal sealed class BeltSegmentInputs
    {
        private readonly IBeltSource[] inputs = new IBeltSource[3];
        private int inputCount, inputPorts, inputMask;
        internal int PriorityOrder { get; private set; }
        internal BeltDirection ReservedDirection { get; private set; } = BeltDirection.None;

        internal BeltSegmentInputs(int priorityOrder)
        {
            PriorityOrder = priorityOrder;
        }

        internal void Attach(IBeltSource source, BeltDirection direction)
        {
            // 方向から搬入元を引けるように登録する。
            // Register a source by its incoming direction.
            inputs[inputCount] = source;
            inputPorts |= inputCount << ((int)direction * 2);
            inputMask |= 1 << (int)direction;
            inputCount++;
        }

        internal void Resolve(bool occupied)
        {
            // 問い合わせだけでは優先順を変更しない。
            // Reservation queries never rotate priority.
            ReservedDirection = BeltDirection.None;
            if (occupied) return;
            for (int offset = 0; offset < 3; offset++)
            {
                var direction = (BeltDirection)BeltPriority.Direction(PriorityOrder, offset);
                if ((inputMask & (1 << (int)direction)) == 0) continue;
                int port = (inputPorts >> ((int)direction * 2)) & 3;
                if (!inputs[port].TryGetOutput(direction)) continue;
                ReservedDirection = direction;
                return;
            }
        }

        internal void CompleteInput(BeltDirection direction)
        {
            PriorityOrder = BeltPriority.MoveLast(PriorityOrder, (int)direction);
        }
    }
}
