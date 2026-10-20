namespace Client.Game.InGame.GraphicsQuality
{
    public enum GraphicsRenderScale
    {
        Percent100,
        Percent75,
        Percent50,
    }

    public static class GraphicsRenderScaleExtensions
    {
        public static float ToScale(this GraphicsRenderScale renderScale)
        {
            return renderScale switch
            {
                GraphicsRenderScale.Percent100 => 1f,
                GraphicsRenderScale.Percent75 => 0.75f,
                GraphicsRenderScale.Percent50 => 0.5f,
                _ => throw new System.ArgumentOutOfRangeException(nameof(renderScale), renderScale, null),
            };
        }
    }
}
