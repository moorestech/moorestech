using System;
using Game.Block.Interface.State;

namespace Game.Block.Interface
{
    public interface IBlock : IEquatable<IBlock>
    {
        public int EntityId { get; }
        public int BlockId { get; }
        public long BlockHash { get; }
        public IBlockComponentManager ComponentManager { get; }
        public BlockPositionInfo BlockPositionInfo { get; }

        /// <summary>
        ///     ブロックで何らかのステートが変化したときに呼び出されます
        ///     例えば、動いている機械が止まったなど
        ///     クライアント側で稼働アニメーションや稼働音を実行するときに使用します
        /// </summary>
        public IObservable<ChangedBlockState> BlockStateChange { get; }
        public string GetSaveState();
    }
}