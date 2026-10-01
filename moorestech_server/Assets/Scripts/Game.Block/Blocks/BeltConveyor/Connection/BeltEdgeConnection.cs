using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.BeltConveyor.Connection
{
    // resolverは変更先コンポーネントを保持せず、読み取り結果だけを返す
    // The resolver returns read-only results without retaining components to mutate
    internal sealed class BeltEdgeConnection
    {
        internal readonly IBlock Source;
        internal readonly IBlockInventory Target;
        internal readonly ConnectedInfo Info;

        internal BeltEdgeConnection(IBlock source, IBlock target, IBlockConnector output, IBlockConnector input)
        {
            Source = source;
            Target = target.ComponentManager.GetComponent<IBlockInventory>();
            Info = new ConnectedInfo(output, input, target);
        }
    }
}
