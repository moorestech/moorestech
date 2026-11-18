namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// キャンバスへ当てる変換（平行移動と倍率。Web の ViewportTransform）
    /// The transform applied to the canvas (translation and scale; Web ViewportTransform)
    /// </summary>
    public readonly struct NodeGraphViewport
    {
        public float X { get; }
        public float Y { get; }
        public float Scale { get; }

        public NodeGraphViewport(float x, float y, float scale)
        {
            X = x;
            Y = y;
            Scale = scale;
        }

        public static NodeGraphViewport Identity => new(0f, 0f, 1f);
    }
}
