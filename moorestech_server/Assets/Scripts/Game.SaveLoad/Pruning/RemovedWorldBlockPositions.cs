using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning
{
    /// <summary>world節から除去したブロックの原点座標。レールノードはこの座標でブロックを指すので、巻き添えの判定に使う</summary>
    /// <summary>Origin positions of the blocks pruned from the world section; rail nodes point at their block by this position, so it decides collateral removal</summary>
    public sealed class RemovedWorldBlockPositions
    {
        // ConnectionDestination（JsonProperty無し）のプロパティ名とSerializableVector3Intのフィールド名
        // Property names of ConnectionDestination (no JsonProperty) and field names of SerializableVector3Int
        private const string BlockPositionKey = "blockPosition";

        private readonly HashSet<Vector3Int> _positions = new();

        public RemovedWorldBlockPositions(JArray removedBlocks)
        {
            foreach (var block in removedBlocks.OfType<JObject>())
            {
                // BlockJsonObjectのX/Y/Zは原点座標。RailComponentはこの原点をConnectionDestinationに刻む
                // BlockJsonObject's X/Y/Z is the origin, which RailComponent stamps into its ConnectionDestination
                var x = (int?)block["X"];
                var y = (int?)block["Y"];
                var z = (int?)block["Z"];
                if (x == null || y == null || z == null)
                {
                    Debug.LogWarning($"除去したブロックの座標が読めないため、レール上の列車とレール接続の巻き添え判定から外します。 instanceId={block["instanceId"]}");
                    continue;
                }

                _positions.Add(new Vector3Int(x.Value, y.Value, z.Value));
            }
        }

        public bool IsEmpty => _positions.Count == 0;

        // レールノードの接続先が除去したブロックの上にあるか。形の読めない接続先は判定できないのでfalse
        // Whether a rail node's connection destination sits on a pruned block; an unreadable destination cannot be judged and yields false
        public bool ContainsConnectionDestination(JToken connectionDestination)
        {
            if (connectionDestination is not JObject destination || destination[BlockPositionKey] is not JObject position) return false;

            var x = (int?)position["x"];
            var y = (int?)position["y"];
            var z = (int?)position["z"];
            return x != null && y != null && z != null && _positions.Contains(new Vector3Int(x.Value, y.Value, z.Value));
        }
    }
}
