using Core.Master;

namespace Game.MapGeneration.Pipeline.Config
{
    // 独立散布の1スポーン距離バンド（同心円リング）。量は非クラスタ・クラスタとも density で決める。
    // A single spawn-distance band for object scatter; density drives the amount in both scatter and cluster mode.
    public class ObjectScatterBand : SpawnDistanceBand
    {
        // 1haあたり密度。非クラスタでは点数、クラスタモードではクラスタ中心数を決める。
        // Per-hectare density: point count in scatter mode, cluster-centre count in cluster mode.
        public float density = 1f;
    }
}
