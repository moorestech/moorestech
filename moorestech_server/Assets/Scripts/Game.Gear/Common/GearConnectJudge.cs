using Game.Block.Interface.Component.ConnectJudge;

namespace Game.Gear.Common
{
    /// <summary>
    ///     歯車ドメインの接続判定。噛み合い軸の平行チェックを行う（実装はTask 4）
    ///     Gear-domain connect judge; checks meshing-axis parallelism (implemented in Task 4)
    /// </summary>
    public class GearConnectJudge : IConnectorConnectJudge
    {
        public bool CanConnect(ConnectJudgeContext context)
        {
            return true;
        }
    }
}
