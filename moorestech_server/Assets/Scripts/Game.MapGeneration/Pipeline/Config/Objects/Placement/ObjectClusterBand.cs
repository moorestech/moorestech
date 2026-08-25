using Core.Master;

namespace Game.MapGeneration.Pipeline.Config
{
    // クラスタ配置の1スポーン距離バンド（同心円リング）。量は1haあたりのクラスタ中心数。
    // A single spawn-distance band for cluster placement; the amount is cluster centres per hectare.
    public class ObjectClusterBand : SpawnDistanceBand
    {
        public float clusterCentersPerHectare = 1f;
    }
}
