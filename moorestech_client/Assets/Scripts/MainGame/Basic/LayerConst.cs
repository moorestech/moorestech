using UnityEngine;

namespace MainGame.Basic
{
    public class LayerConst
    {
        public static readonly int BlockLayer = LayerMask.NameToLayer("Block");
        public static readonly int MapObjectLayer = LayerMask.NameToLayer("MapObject");

        public static readonly int BlockOnlyLayerMask = 1 << BlockLayer;
        public static readonly int MapObjectOnlyLayerMask = 1 << MapObjectLayer;
        public static readonly int WithoutOnlyMapObjectLayerMask = ~MapObjectOnlyLayerMask;
    }
}