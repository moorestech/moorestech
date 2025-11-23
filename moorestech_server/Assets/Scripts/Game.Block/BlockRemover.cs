namespace Game.Block
{
    using System;
    using Game.Block.Interface;
    using Game.World.Interface.DataStore;
    using UnityEngine;

    /// <summary>
    /// IBlockRemoverの実装クラス
    /// Implementation of IBlockRemover
    /// </summary>
    public class BlockRemover : IBlockRemover
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// コンストラクタ
        /// Constructor
        /// </summary>
        /// <param name="serviceProvider">DIコンテナのサービスプロバイダー / Service provider</param>
        public BlockRemover(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// ブロックを削除する
        /// Remove block
        /// </summary>
        public void RemoveBlock(BlockPositionInfo position, BlockRemovalType removalType)
        {
            // 循環参照を避けるために遅延解決する
            // Lazy resolution to avoid circular dependency
            var worldBlockDatastore = (IWorldBlockDatastore)_serviceProvider.GetService(typeof(IWorldBlockDatastore));

            // 削除理由をログに記録
            // Log removal reason
            Debug.Log($"Block removal: Position={position.OriginalPos}, Type={removalType}");

            // WorldBlockDatastoreに削除を委譲
            // Delegate removal to WorldBlockDatastore
            worldBlockDatastore.RemoveBlock(position.OriginalPos);
        }
    }
}
