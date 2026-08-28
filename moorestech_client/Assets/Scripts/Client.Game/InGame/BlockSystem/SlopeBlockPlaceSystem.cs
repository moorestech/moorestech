using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem
{
    public class SlopeBlockPlaceSystem
    {
        /// <summary>
        ///     TODO ここの定義の場所を変える
        /// </summary>
        public static Vector3 GetBlockPositionToPlacePosition(Vector3Int blockPosition, BlockDirection blockDirection, BlockId blockId)
        {
            // 大きさをBlockDirection系に変換
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
            var originPos = blockDirection.GetBlockModelOriginPos(blockPosition, blockSize);
            
            return originPos;
        }
    }
}
