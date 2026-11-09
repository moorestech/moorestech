namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// キャンバスの寸法と、ツリー座標をキャンバス座標へずらす量（Web の TreeCanvasBounds）
    /// Canvas size and the offsets that map tree coordinates into canvas coordinates (Web TreeCanvasBounds)
    /// </summary>
    internal readonly struct NodeGraphCanvasBounds
    {
        public float Width { get; }
        public float Height { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }

        public NodeGraphCanvasBounds(float width, float height, float offsetX, float offsetY)
        {
            Width = width;
            Height = height;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }
    }
}
