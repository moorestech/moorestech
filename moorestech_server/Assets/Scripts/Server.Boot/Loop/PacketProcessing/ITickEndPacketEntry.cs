namespace Server.Boot.Loop.PacketProcessing
{
    public interface ITickEndPacketEntry
    {
        bool IsActive { get; }
        void Process();
    }
}
