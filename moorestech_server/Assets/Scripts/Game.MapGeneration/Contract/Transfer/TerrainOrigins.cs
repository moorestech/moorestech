using UnityEngine;

namespace Game.MapGeneration.Transfer
{
    // 生成時にしか決まらない2つの原点。どちらも同型のVector2なので、隣り合う引数ではなく名前付きの対で運ぶ
    // The two origins that exist only at generation; both are Vector2, so they travel as a named pair instead of adjacent arguments
    public readonly struct TerrainOrigins
    {
        // 分類段(海陸・ビーチ・バイオーム重み)を再現するためのノイズ窓原点。スポーン探索の中央化オフセットGを含む
        // Noise window origin for reproducing the classification stage; it embeds the spawn-search centering offset G
        public readonly Vector2 NoiseOrigin;

        // 生成タイルがシーン上で占める原点。地形の設置位置であり、map.jsonの座標もこれを基準にする
        // Scene-space origin of the generated tile: where the terrain is placed and the basis of map.json coordinates
        public readonly Vector2 SceneOrigin;

        public TerrainOrigins(Vector2 noiseOrigin, Vector2 sceneOrigin)
        {
            NoiseOrigin = noiseOrigin;
            SceneOrigin = sceneOrigin;
        }
    }
}
