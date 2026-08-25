using Core.Master;

namespace Game.MapGeneration.Pipeline.Config
{
    // 独立散布の1スポーン距離バンド（同心円リング）。量は1haあたりの点数。
    // A single spawn-distance band for object scatter; the amount is points per hectare.
    public class ObjectScatterBand : SpawnDistanceBand
    {
        public float pointsPerHectare = 1f;
    }
}
