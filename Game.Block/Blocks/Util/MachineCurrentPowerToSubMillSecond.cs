using Core.Update;

namespace Game.Block.Blocks.Util
{
    /// <summary>
    ///     （）
    /// </summary>
    public static class MachineCurrentPowerToSubMillSecond
    {
        public static int GetSubMillSecond(int currentPower, int requiredPower)
        {
            //0
            if (requiredPower == 0) return (int)GameUpdater.UpdateMillSecondTime;
            
            //10050
            return (int)(GameUpdater.UpdateMillSecondTime * (currentPower / (double)requiredPower));
        }
    }
}