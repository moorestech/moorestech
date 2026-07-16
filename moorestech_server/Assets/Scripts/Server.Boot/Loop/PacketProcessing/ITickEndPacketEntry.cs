using Server.Protocol;

namespace Server.Boot.Loop.PacketProcessing
{
    public interface ITickEndPacketEntry
    {
        bool IsActive { get; }
        TickEndPacketProcessResult Process();
    }
}
