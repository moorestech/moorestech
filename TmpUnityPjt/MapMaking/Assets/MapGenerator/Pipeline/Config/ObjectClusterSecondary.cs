using System;
using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    public enum SecondaryPlacementMode
    {
        [InspectorName("環状配置")] Ring,
        [InspectorName("サドル/偏り配置")] Saddle
    }

    /// <summary>
    /// Primaryクラスターに従属する配置グループ。
    /// Ring(環状)かSaddle(サドル/偏り)の配置モードを選択し、
    /// ObjectClusterEntry.secondaries[] に任意数追加できる。
    /// </summary>
    [Serializable]
    public class ObjectClusterSecondary
    {
        [Label("配置モード")]
        public SecondaryPlacementMode mode;

        [Label("プレハブ")]
        public GameObject[] prefabs;

        [Label("スケール範囲")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);

        [Label("傾斜追従")]
        [Range(0f, 1f)] public float slopeAlignment;

        [Label("埋め込み範囲")]
        public Vector2 sinkRange = Vector2.zero;

        [Label("クラスターあたり数")]
        [Range(1, 40)] public int countPerCluster = 6;

        [Label("Tree最小距離")]
        public float minDistanceFromTree;

        // Primaryクラスターからの距離制約（Ring/Saddle共通）
        [Header("距離制約")]
        [Label("最小距離(m)")]
        [Range(0f, 30f)] public float minDistance = 1.5f;
        [Label("最大距離(m)")]
        [Range(0f, 50f)] public float maxDistance = 8f;

        // Saddleモード専用: パッチあたりの密度とパッチ半径
        [Header("Saddle専用")]
        [Label("密度")]
        [Range(0f, 10f)] public float density = 1f;
        [Label("パッチ半径(m)")]
        [Range(1f, 50f)] public float clusterRadius = 12f;
    }
}
