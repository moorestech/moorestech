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
        public static Vector3 AddBlockPlaceOffset(this Vector3Int pos)
        {
            return pos + new Vector3(0.5f, 0.5f, 0.5f);
        }

        public static (Vector3 minPos, Vector3 maxPos) GetWorldBlockBoundingBox(this Vector3Int blockPos,BlockDirection blockDirection,Vector3Int blockSize)
        {
            var maxPos = WorldBlockData.CalcBlockMaxPos(blockPos, blockDirection, blockSize);
            //これはグリッド上のどこが最大値なのかを表しているので、実際のバウンディングボックスにするために +1 する
            maxPos += Vector3Int.one;
            
            return (blockPos,maxPos);
        }
    }
}