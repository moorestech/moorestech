using UnityEngine;

namespace Client.Common
{
    public class LayerConst
    {
        public static readonly int PlayerLayer = LayerMask.NameToLayer("Player");
        public static readonly int BlockLayer = LayerMask.NameToLayer("Block");
        public static readonly int BlockBoundingBoxLayer = LayerMask.NameToLayer("BlockBoundingBox");
        public static readonly int MapObjectLayer = LayerMask.NameToLayer("MapObject");
        public static readonly int GroundLayer = LayerMask.NameToLayer("Ground");
        public static readonly int ConnectionLineLayer = LayerMask.NameToLayer("ConnectionLine");

        // フォーカス時の輪郭だけを描くレイヤー
        // Layer that draws nothing but the focus outline
        public static readonly int OutlineLayer = LayerMask.NameToLayer("Outline");

        // このレイヤーマスク、列車の追加によって「ブロック」だけでなく、ワールド中にインタラクトできるもの、という意味になりつつあるからリネームを検討する
        public static readonly int BlockOnlyLayerMask = 1 << BlockLayer;
        public static readonly int BlockBoundingBoxOnlyLayerMask = 1 << BlockBoundingBoxLayer;
        public static readonly int MapObjectOnlyLayerMask = 1 << MapObjectLayer;
        public static readonly int PlayerOnlyLayerMask = 1 << PlayerLayer;
        public static readonly int ConnectionLineOnlyLayerMask = 1 << ConnectionLineLayer;

        // 接続線は専用操作だけが狙うため汎用レイキャストから除外する
        // Connection lines are targeted only by dedicated interactions, so exclude them from generic raycasts
        public static readonly int Without_Player_MapObject_Block_LayerMask = ~MapObjectOnlyLayerMask & ~PlayerOnlyLayerMask & ~BlockOnlyLayerMask & ~ConnectionLineOnlyLayerMask;
        public static readonly int Without_Player_MapObject_BlockBoundingBox_LayerMask = ~MapObjectOnlyLayerMask & ~PlayerOnlyLayerMask & ~BlockBoundingBoxOnlyLayerMask & ~ConnectionLineOnlyLayerMask;
    }
}
