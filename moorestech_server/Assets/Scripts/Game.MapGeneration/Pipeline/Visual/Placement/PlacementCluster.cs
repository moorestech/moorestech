using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    // 見た目ステージが持ち回るクラスタ識別子と重心。クラスタ無しは番兵でなくnullで表す
    // The cluster id and centroid carried by the visual stages; "no cluster" is null, never an in-band sentinel
    public readonly struct PlacementCluster
    {
        public readonly int Id;
        public readonly Vector2 Center;

        public PlacementCluster(int id, Vector2 center)
        {
            Id = id;
            Center = center;
        }
    }
}
