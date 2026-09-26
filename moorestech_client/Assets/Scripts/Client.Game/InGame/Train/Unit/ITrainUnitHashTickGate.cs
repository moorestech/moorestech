namespace Client.Game.InGame.Train.Unit
{
    public interface ITrainUnitHashTickGate
    {
        bool CanAdvanceTick(ulong currentTickUnifiedId);
    }
}
