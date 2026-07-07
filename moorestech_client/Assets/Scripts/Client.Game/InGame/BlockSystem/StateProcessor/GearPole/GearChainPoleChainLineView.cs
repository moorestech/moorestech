using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.GearPole
{
    /// <summary>
    /// GearChainPoleのチェーン接続を視覚的に表示するコンポーネント
    /// Component for visually displaying chain connections of GearChainPole
    /// </summary>
    public class GearChainPoleChainLineView : ConnectionLineViewBase<GearChainPoleChainLineViewElement>
    {
        private const string ChainLinePrefabAddress = "Vanilla/Block/Util/GearChainLine";

        protected override string GetLinePrefabAddress()
        {
            return ChainLinePrefabAddress;
        }
    }
}
