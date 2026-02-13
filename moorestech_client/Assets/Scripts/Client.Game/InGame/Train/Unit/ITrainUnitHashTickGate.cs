namespace Client.Game.InGame.Train.Unit
{
    // 列車シミュレーションtick進行の可否を判定するゲート。
    // Gate that decides whether train simulation can advance by one tick.
    public interface ITrainUnitHashTickGate
    {
        bool CanAdvanceTick(uint currentTick);
    }
}
