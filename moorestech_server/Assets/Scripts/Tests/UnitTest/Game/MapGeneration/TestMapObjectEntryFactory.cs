using Newtonsoft.Json.Linq;

namespace Tests.UnitTest.Game.MapGeneration
{
    // pipelineテスト用の独立散布エントリを構築
    // Builds the independently scattered entry used by pipeline tests
    internal static class TestMapObjectEntryFactory
    {
        public static JObject Create(string mapObjectGuid)
        {
            return new JObject
            {
                ["prefabs"] = new JArray(new JObject { ["mapObjectGuid"] = mapObjectGuid }),
                ["terrainSurroundEffectType"] = "rockNoBareGround",
                // 半径・密度差で転写違いを検出
                // Differing radii and densities expose a band-to-ring transcription mix-up
                ["placementMode"] = "scatter",
                ["placementParam"] = new JObject
                {
                    ["bands"] = new JArray(
                        new JObject
                        {
                            ["outerRadiusMeters"] = 250.0,
                            ["pointsPerHectare"] = 2.0,
                        },
                        new JObject
                        {
                            ["outerRadiusMeters"] = -1,
                            ["pointsPerHectare"] = 1.0,
                        }),
                },
                ["scaleRange"] = new JArray(1.0, 1.0),
                ["slopeAlignment"] = 0.0,
                ["sinkRange"] = new JArray(0.0, 0.0),
                ["noiseType"] = "None",
                ["noiseFrequency"] = 10.0,
                ["noiseAmplitude"] = 1.0,
                ["noiseThreshold"] = 0.5,
                ["useSlopeFilter"] = false,
                ["slopeMin"] = 0.0,
                ["slopeMax"] = 90.0,
                ["slopeSmoothness"] = 4.0,
                ["minDistanceFromTree"] = 0.0,
                ["maxDistanceFromTree"] = 0.0,
            };
        }
    }
}
