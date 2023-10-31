using Core.Util;
using UnityEngine;

namespace MainGame.Basic.Util
{
    public static class CoreVector3Extension
    {
        public static Vector3 ToUniVector3(this CoreVector3 coreVector3)
        {
            return new Vector3(coreVector3.X, coreVector3.Y, coreVector3.Z);
        }
        public static Quaternion ToQuotation(this CoreVector3 coreVector3)
        {
            return Quaternion.Euler(ToUniVector3(coreVector3));
        }
    }
}