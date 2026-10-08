namespace Game.MapGeneration.Pipeline.Surface
{
    internal interface ISurfaceDisplayHeightSource
    {
        float[,] Load(int tileX, int tileZ);
    }
}
