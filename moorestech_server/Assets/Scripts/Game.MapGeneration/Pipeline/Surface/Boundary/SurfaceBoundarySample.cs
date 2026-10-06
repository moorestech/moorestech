using Game.MapGeneration.Pipeline.Jobs;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal sealed class SurfaceBoundarySample
    {
        private readonly float _height;
        private readonly float _shore;
        private readonly float _land;
        private readonly float _beach;
        private readonly float _landTexture;
        private readonly float _seaTexture;
        private readonly float _plateau;
        private readonly int _winner;
        private readonly float[] _biomeWeights;

        internal SurfaceBoundarySample(JobBuffers source, int index, int biomeCount)
        {
            // 窓の寿命を越えて使う境界だけを独立所有する
            // Own only the boundary values that must outlive the temporary window
            _height = source.heights[index];
            _shore = source.shoreMask[index];
            _land = source.landMask[index];
            _beach = source.beachFactor[index];
            _landTexture = source.landTextureFactor[index];
            _seaTexture = source.seaTextureFactor[index];
            _plateau = source.plateauMask[index];
            _winner = source.winnerBiomeIndex[index];
            _biomeWeights = new float[biomeCount];
            for (int biome = 0; biome < biomeCount; biome++)
                _biomeWeights[biome] = source.biomeWeights[index * biomeCount + biome];
        }

        internal void Write(JobBuffers destination, int index)
        {
            // 高さと分類を必ず同じ所有タイルから配信する
            // Emit heights and classification from the same owning tile
            destination.heights[index] = _height;
            destination.shoreMask[index] = _shore;
            destination.landMask[index] = _land;
            destination.beachFactor[index] = _beach;
            destination.landTextureFactor[index] = _landTexture;
            destination.seaTextureFactor[index] = _seaTexture;
            destination.plateauMask[index] = _plateau;
            destination.winnerBiomeIndex[index] = _winner;
            // regionLabelsは窓内IDなので共有せず、各窓の対応表と対で保持する
            // Keep window-local region labels paired with their own window's region table
            for (int biome = 0; biome < _biomeWeights.Length; biome++)
                destination.biomeWeights[index * _biomeWeights.Length + biome] = _biomeWeights[biome];
        }
    }
}
