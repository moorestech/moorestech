namespace Client.Game.TickSynchronization
{
    // streamtick進行の可否を判定するゲート。
    // Gate that decides whether a stream can advance by one tick.
    internal interface ITickAdvanceGate
    {
        bool CanAdvanceTick(ulong currentTickUnifiedId);
    }
}
