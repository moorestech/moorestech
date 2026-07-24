using UnityEngine;

namespace Game.MapGeneration.Pipeline.Config
{
    // Primary(大岩)クラスター + 任意数の Secondary グループ設定（primary は mapObjectGuid 配列へ置換）。
    // Primary rock cluster plus subordinate groups (primary replaced by mapObjectGuid array).
    public class ObjectClusterEntry
    {
        public string[] primary;
        public float density = 1f;
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
        public float slopeAlignment;
        public Vector2 sinkRange = Vector2.zero;
        public MapNoiseType noiseType = MapNoiseType.None;
        public float noiseFrequency = 10f;
        public float noiseAmplitude = 1f;
        public float noiseThreshold = 0.5f;
        public int clusterCount = 8;
        public int objectsPerCluster = 4;
        public float clusterRadius = 12f;
        public float minDistanceFromTree;
        public ObjectClusterSecondary[] secondaries;
    }
}
