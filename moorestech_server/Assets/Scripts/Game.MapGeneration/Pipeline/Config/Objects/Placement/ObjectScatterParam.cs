namespace Game.MapGeneration.Pipeline.Config
{
    // 点をそのまま撒く方式のパラメータ。
    // Parameters of the mode that scatters points directly.
    public class ObjectScatterParam : ObjectPlacementParam
    {
        public ObjectScatterBand[] bands = new ObjectScatterBand[0];
    }
}
