using Core.BeltTransport;
using MessagePack;
namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltCellSurfaceMessagePack
    {
        [Key(0)] public float InputHeight { get; }
        [Key(1)] public float OutputHeight { get; }
        [SerializationConstructor]
        public BeltCellSurfaceMessagePack(float inputHeight, float outputHeight)
        { InputHeight = inputHeight; OutputHeight = outputHeight; }
        public BeltCellSurfaceMessagePack(BeltCellSurfaceProfile value)
        { InputHeight = value.InputHeight; OutputHeight = value.OutputHeight; }
        internal BeltCellSurfaceProfile ToCore() => new(InputHeight, OutputHeight);
    }
}
