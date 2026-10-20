namespace Client.Game.InGame.GraphicsQuality
{
    public enum GraphicsDrawDistance
    {
        Meters300,
        Meters1000,
        Meters5000,
    }

    public static class GraphicsDrawDistanceExtensions
    {
        public static float ToMeters(this GraphicsDrawDistance drawDistance)
        {
            return drawDistance switch
            {
                GraphicsDrawDistance.Meters300 => 300f,
                GraphicsDrawDistance.Meters1000 => 1000f,
                GraphicsDrawDistance.Meters5000 => 5000f,
                _ => throw new System.ArgumentOutOfRangeException(nameof(drawDistance), drawDistance, null),
            };
        }
    }
}
