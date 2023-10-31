using Core.Util;
using UnityEngine;

namespace MainGame.Basic.Server
{
    public static class ServerExtension
    {
        public static Vector2 ToVec2(this CoreVector2 coreVector2)
        {
            return new Vector2(coreVector2.X, coreVector2.Y);
        }
    }
}