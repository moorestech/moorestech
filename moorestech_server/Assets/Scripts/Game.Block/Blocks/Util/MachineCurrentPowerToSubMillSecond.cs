using Core.Update;

namespace Game.Block.Blocks.Util
{
    /// <summary>
    ///     機械の現在の電力量から、機械のプロセス（例えば採掘やアイテムの加工など）をどれくらい進めるかを計算する
    /// </summary>
    public static class MachineCurrentPowerToSubMillSecond
    {
        public static int GetSubMillSecond(int currentPower, float requiredPower)
        {
            //必要電力が0の時はそのフレームの時間を返す
            if (requiredPower == 0) return (int)GameUpdater.UpdateMillSecondTime;
            //現在の電力量を必要電力で割った割合で、そのフレームの時間を返す
            //例えば、必要電力が100、現在の電力が50だったら、そのフレームの半分の時間を返すことで、機械の速度を半分にする
            return (int)(GameUpdater.UpdateMillSecondTime * (currentPower / (double)requiredPower));
        }
    }
}