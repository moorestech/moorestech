using UnityEngine;

namespace Client.Common
{
    public class LayerConst
    {
        public static readonly int PlayerLayer = LayerMask.NameToLayer("Player");
        public static readonly int BlockLayer = LayerMask.NameToLayer("Block");
        public static readonly int BlockBoundingBoxLayer = LayerMask.NameToLayer("BlockBoundingBox");
        public static readonly int MapObjectLayer = LayerMask.NameToLayer("MapObject");
        
        public static readonly int BlockOnlyLayerMask = 1 << BlockLayer;
        public static readonly int MapObjectOnlyLayerMask = 1 << MapObjectLayer;
        public static readonly int PlayerOnlyLayerMask = 1 << PlayerLayer;
        
        public static readonly int Without_Player_MapObject_Block_LayerMask = ~MapObjectOnlyLayerMask & ~PlayerOnlyLayerMask & ~BlockOnlyLayerMask;
    }
}