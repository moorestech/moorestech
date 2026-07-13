using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace MapGenerator.Pipeline.Jobs
{
    /// <summary>
    /// ComputeSlopeのBurst並列版。TerrainGenerator.ComputeSlopesのマネージドループを置換する。
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct SlopeComputeJob : IJobParallelFor
    {
        public int resolution;
        public float terrainWidth, terrainHeight, terrainLength;

        [ReadOnly] public NativeArray<float> heights;
        [WriteOnly] public NativeArray<float> slopes;

        public void Execute(int idx)
        {
            int x = idx % resolution;
            int z = idx / resolution;
            slopes[idx] = BurstTerrainMath.ComputeSlope(
                heights, resolution, x, z,
                terrainWidth, terrainHeight, terrainLength);
        }
    }
}
