using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire
{
    /// <summary>
    /// 電力ワイヤーの接続を視覚的に表示するコンポーネント
    /// Component for visually displaying electric wire connections
    /// </summary>
    public class ElectricWireLineView : ConnectionLineViewBase<ElectricWireLineViewElement>
    {
        private const string WireLinePrefabAddress = "Vanilla/Block/Util/ElectricWireLine";
        private readonly IConnectionLineCommands _commands = new ElectricWireLineCommands();

        protected override ConnectionLineKind GetLineKind()
        {
            return ConnectionLineKind.ElectricWire;
        }

        protected override IConnectionLineCommands GetLineCommands()
        {
            return _commands;
        }

        protected override string GetLinePrefabAddress()
        {
            return WireLinePrefabAddress;
        }
    }
}
