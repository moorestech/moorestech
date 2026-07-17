using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Interface
{
    public interface IBlock : IEquatable<IBlock>
    {
        public BlockInstanceId BlockInstanceId { get; }
        public BlockId BlockId { get; }
        public Guid BlockGuid { get; }
        public BlockMasterElement BlockMasterElement { get; }
        public IBlockComponentManager ComponentManager { get; }
        public BlockPositionInfo BlockPositionInfo { get; }
        
        /// <summary>
        ///     ブロックで何らかのステートが変化したときに呼び出されます
        ///     例えば、動いている機械が止まったなど
        ///     クライアント側で稼働アニメーションや稼働音を実行するときに使用します
        /// </summary>
        public IObservable<BlockState> BlockStateChange { get; }
        
        public BlockState GetBlockState();

        /// <summary>
        ///     共通tickループ（ServerTickUpdater）から正準順で呼ばれるブロック更新
        ///     自走駆動を宣言したコンポーネント（ISelfDrivenUpdatableBlockComponent）はここでは更新しない
        ///     Block update driven by the central tick loop (ServerTickUpdater) in canonical order.
        ///     Components declaring self-driven updates (ISelfDrivenUpdatableBlockComponent) are not updated here
        /// </summary>
        public void TickUpdate();

        public Dictionary<string,string> GetSaveState();
        
        public void Destroy();
    }
}