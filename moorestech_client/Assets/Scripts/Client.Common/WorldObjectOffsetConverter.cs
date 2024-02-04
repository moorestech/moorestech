using Game.World.Interface.DataStore;
using UnityEngine;

namespace Constant
{
    /// <summary>
    ///     0を基準としたサーバーの座標値を、0.5をプラスして、オブジェクトなどの座標の左端を0,0に合わせるように変換する
    /// </summary>
    public static class WorldObjectOffsetConverter
    {
        public static Vector3 AddBlockPlaceOffset(this Vector3 vector3)
        {
            return vector3 + new Vector3(0.5f, 0, 0.5f);
        }
        public static Vector2 AddBlockPlaceOffset(this Vector2 vector2)
        {
            return vector2 + new Vector2(0.5f, 0.5f);
        }
        public static Vector2 AddBlockPlaceOffset(this Vector2Int vector2)
        {
            return vector2 + new Vector2(0.5f, 0.5f);
        }

        public static (Vector2 minPos, Vector2 maxPos) GetWorldBlockBoundingBox(this Vector2Int blockPos,BlockDirection blockDirection,Vector2Int blockSize)
        {
            var maxPos = WorldBlockData.CalcBlockGridMaxPos(blockPos, blockDirection, blockSize);
            //これはグリッド上のどこか最大値化を表しているので、実際のバウンディングボックスにするために +1 する
            maxPos += Vector2Int.one;
            
            return (blockPos,maxPos);
        }
    }
}