using UnityEngine;

namespace Game.MapGeneration.Pipeline.Config
{
    // 従属配置グループのモード。
    // Placement mode for a subordinate group.
    public enum SecondaryPlacementMode { Ring, Saddle }

    // Primary クラスターに従属する配置グループ（prefabs は mapObjectGuid 配列へ置換）。
    // Subordinate group attached to a primary cluster (prefabs replaced by mapObjectGuid array).
    public class ObjectClusterSecondary
    {
        public SecondaryPlacementMode mode;
        public string[] mapObjectGuids;
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
        public float slopeAlignment;
        public Vector2 sinkRange = Vector2.zero;
        public int countPerCluster = 6;
        public float minDistanceFromTree;
        public float minDistance = 1.5f;
        public float maxDistance = 8f;
        public float density = 1f;
        public float clusterRadius = 12f;
    }
}
