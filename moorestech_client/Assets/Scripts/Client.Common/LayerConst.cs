using UnityEngine;

namespace Client.Common
{
    public class LayerConst
    {
        public static readonly int PlayerLayer = LayerMask.NameToLayer("Player");
        public static readonly int BlockLayer = LayerMask.NameToLayer("Block");
        public static readonly int BlockBoundingBoxLayer = LayerMask.NameToLayer("BlockBoundingBox");
        public static readonly int MapObjectLayer = LayerMask.NameToLayer("MapObject");
        public static readonly int ElectricWireLayer = LayerMask.NameToLayer("ElectricWire");

        // このレイヤーマスク、列車の追加によって「ブロック」だけでなく、ワールド中にインタラクトできるもの、という意味になりつつあるからリネームを検討する
        public static readonly int BlockOnlyLayerMask = 1 << BlockLayer;
        public static readonly int BlockBoundingBoxOnlyLayerMask = 1 << BlockBoundingBoxLayer;
        public static readonly int MapObjectOnlyLayerMask = 1 << MapObjectLayer;
        public static readonly int PlayerOnlyLayerMask = 1 << PlayerLayer;
        public static readonly int ElectricWireOnlyLayerMask = 1 << ElectricWireLayer;

        // ワイヤーは専用クリックのみ対象のため、汎用レイキャストから除外する
        // Wires are targeted only by dedicated clicks, so exclude them from generic raycasts
        public static readonly int Without_Player_MapObject_Block_LayerMask = ~MapObjectOnlyLayerMask & ~PlayerOnlyLayerMask & ~BlockOnlyLayerMask & ~ElectricWireOnlyLayerMask;
        public static readonly int Without_Player_MapObject_BlockBoundingBox_LayerMask = ~MapObjectOnlyLayerMask & ~PlayerOnlyLayerMask & ~BlockBoundingBoxOnlyLayerMask & ~ElectricWireOnlyLayerMask;
    }
}