namespace Client.Game.TickSynchronization
{
    internal interface ITickAdvanceGate
    {
        bool CanAdvanceTick(ulong currentTickUnifiedId);
    }
}
