using Core.BeltTransport;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.RandomNetwork
{
    // 生成途中でまだ搬出先が決まっていない出口。通常segmentは方向自由、bufferは方向固定
    // An exit whose target is not decided yet while generating. A normal segment picks any direction; a buffer slot has a fixed one
    public readonly struct BeltOpenEnd
    {
        public readonly BeltNormalSegment Normal;
        public readonly BeltBuffer Buffer;
        public readonly BeltDirection BufferDirection;

        public BeltOpenEnd(BeltNormalSegment normal, BeltBuffer buffer, BeltDirection bufferDirection)
        {
            Normal = normal;
            Buffer = buffer;
            BufferDirection = bufferDirection;
        }

        public bool IsBuffer => Buffer != null;

        // 通常segmentならoutputDirectionで、bufferなら固定方向で接続する
        // Connect with outputDirection for a normal segment, or with the fixed slot direction for a buffer
        // 生成網は全て同じ高さなので、進入方向は実際の搬出方向の反対
        // The generated network is all at one height, so the entry direction is the opposite of the actual output direction
        public void ConnectTo(IBeltReceiver target, BeltDirection outputDirection)
        {
            var direction = IsBuffer ? BufferDirection : outputDirection;
            var entryDirection = BeltEntryDirections.Level(BeltDirections.Opposite(direction));
            if (IsBuffer) Buffer.ConnectTo(target, direction, entryDirection);
            else Normal.ConnectTo(target, direction, entryDirection);
        }
    }
}
