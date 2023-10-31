
using UnityEngine;

namespace MainGame.Basic
{
    /// <summary>
    /// 0を基準としたサーバーの座標値を、0.5をプラスして、オブジェクトなどの座標の左端を0,0に合わせるように変換する
    /// </summary>
    public static class WorldObjectOffsetConverter
    {
        public static Vector3 AddOffset(this Vector3 vector3)
        {
            return vector3 + new Vector3(0.5f, 0, 0.5f);
        }
    }
}