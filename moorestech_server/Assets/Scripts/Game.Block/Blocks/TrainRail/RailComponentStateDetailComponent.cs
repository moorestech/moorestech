using Game.Block.Interface.Component;
using MessagePack;

namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// RailComponentのStateDetailを返すためのコンポーネント
    /// Component that returns StateDetail for RailComponent
    /// </summary>
    public class RailComponentStateDetailComponent : IBlockStateDetail
    {
        private readonly RailComponent _railComponent;

        public bool IsDestroy { get; private set; }

        /// <summary>
        /// コンストラクタ
        /// Constructor
        /// </summary>
        /// <param name="railComponent">レールコンポーネント / Rail component</param>
        public RailComponentStateDetailComponent(RailComponent railComponent)
        {
            _railComponent = railComponent;
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            var bytes = MessagePackSerializer.Serialize(new RailBridgePierComponentStateDetail(_railComponent.RailDirection));
            return new BlockStateDetail[] { new(RailBridgePierComponentStateDetail.StateDetailKey, bytes) };
        }
    }
}
