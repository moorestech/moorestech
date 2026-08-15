using UnityEngine;

namespace Game.MapGeneration.Pipeline.Config
{
    // フィルタ・クラスタリングに使うノイズパラメータ。
    // Noise parameters for filters/clustering.
    public struct PlacementNoise
    {
        public MapNoiseType noiseType;
        public float frequency;
        public float amplitude;
        public float offset;
        public float balance;

        // マスタ由来のテクスチャノイズ源。サーバーデータディレクトリ相対の PNG パスと読み出す成分。
        // Texture noise source from master: a server-data-relative PNG path plus the component to read.
        public string texturePngPath;
        public TextureChannel channel;

        // 生成直前に PlacementNoiseTextureResolver が展開する画素。null ならテクスチャ源は無い。
        // Pixels expanded by PlacementNoiseTextureResolver right before generation; null means no texture source.
        public Color32[] texturePixels;
        public int textureWidth;
        public int textureHeight;
    }
}
