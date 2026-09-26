namespace Client.Game.InGame.Train.Unit
{
    internal interface ITrainUnitHashTickGate
    {
        bool CanAdvanceTick(ulong currentTickUnifiedId);
    }
}
