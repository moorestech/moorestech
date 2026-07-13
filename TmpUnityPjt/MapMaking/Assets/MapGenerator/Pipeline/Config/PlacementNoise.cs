using System;
using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// フィルタやクラスタリングに使うノイズパラメータ。
    /// MicroVerseのNoise classに相当し、テクスチャノイズ・offset・balanceを追加。
    /// </summary>
    [Serializable]
    public struct PlacementNoise
    {
        [Label("ノイズタイプ")]
        public MapNoiseType noiseType;
        [Label("周波数")]
        [Range(0.1f, 50f)] public float frequency;
        [Label("振幅")]
        [Range(0f, 50f)] public float amplitude;
        // ノイズ出力にオフセットを加算して全体を上下シフト
        [Label("オフセット")]
        public float offset;
        // -0.5〜+0.5でノイズの中心をずらし、配置密度の偏りを制御
        [Label("バランス")]
        [Range(-0.5f, 0.5f)] public float balance;
        // テクスチャを直接ノイズソースとして使う場合のマスク画像
        [Label("テクスチャ")]
        public Texture2D texture;
        [Label("チャンネル")]
        public TextureChannel channel;
    }
}
