using Client.Game.InGame.Block.DistanceCulling;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// ブロックをプレビュー半径判定へ占有セルの範囲で登録する
    /// Registers blocks with the preview radius culling using their occupied cells
    /// </summary>
    public sealed class BlockPreviewRadiusRegisterService : IStartable, IBlockDistanceCullingSource
    {
        private readonly BlockDistanceCullingRegistrar _registrar;

        public BlockPreviewRadiusRegisterService(BlockGameObjectDataStore dataStore, BlockPreviewRadiusCullingManager manager)
        {
            _registrar = new BlockDistanceCullingRegistrar(dataStore, manager.Culling, this);
        }

        public void Start()
        {
            _registrar.Start();
        }

        public BlockDistanceCullingEntry CreateEntry(BlockGameObject block)
        {
            // 占有セルのAABB（MaxPosはセル座標なので+1して外周まで含める）
            // AABB over occupied cells (MaxPos is a cell coordinate, so +1 to reach the far edge)
            var posInfo = block.BlockPosInfo;
            var bounds = new Bounds();
            bounds.SetMinMax(posInfo.MinPos, posInfo.MaxPos + Vector3Int.one);
            return new BlockDistanceCullingEntry(bounds, new BlockPreviewRadiusCullingTarget(block));
        }
    }
}
