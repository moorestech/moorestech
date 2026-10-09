using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    // 描画レールのコライダーへ論理区間IDを付ける
    // Attach the logical edge ID to the rendered rail colliders
    internal static class RailColliderObjectIdBinder
    {
        public static void Apply(GameObject lineObject, ulong railObjectId)
        {
            // レール用コライダーにIDを埋め込む
            // Embed the rail object id into colliders for raycast lookup
            var colliders = lineObject.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                var carrier = collider.GetComponent<RailObjectIdCarrier>();
                if (carrier == null)
                    carrier = collider.gameObject.AddComponent<RailObjectIdCarrier>();
                carrier.SetRailObjectId(railObjectId);
            }
        }
    }
}
