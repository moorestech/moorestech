namespace Game.MapGeneration.Pipeline.Config
{
    // クラスタ中心を撒いて背骨状にメンバーを並べる方式のパラメータ。
    // Parameters of the mode that scatters cluster centres and lays members along a backbone.
    public class ObjectClusterParam : ObjectPlacementParam
    {
        public ObjectClusterBand[] bands = new ObjectClusterBand[0];
        public int objectsPerCluster = 4;
        public float clusterRadius = 12f;
    }
}
