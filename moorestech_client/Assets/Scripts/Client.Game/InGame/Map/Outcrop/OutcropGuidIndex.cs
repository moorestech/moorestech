using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    /// <summary>
    ///     生成済み露頭をveinGuid別に保持し、プレイヤー位置から最寄りを解決する
    ///     Stores instantiated outcrops per vein GUID and resolves the nearest one to a player position
    /// </summary>
    internal sealed class OutcropGuidIndex
    {
        private readonly Dictionary<Guid, List<OutcropGameObject>> _outcropsByVeinGuid = new();

        internal void Add(Guid veinGuid, OutcropGameObject outcrop)
        {
            if (!_outcropsByVeinGuid.TryGetValue(veinGuid, out var outcrops))
            {
                outcrops = new List<OutcropGameObject>();
                _outcropsByVeinGuid.Add(veinGuid, outcrops);
            }

            outcrops.Add(outcrop);
        }

        internal OutcropGameObject SearchNearest(Guid veinGuid, Vector3 position)
        {
            if (!_outcropsByVeinGuid.TryGetValue(veinGuid, out var outcrops)) return null;

            // 平方距離で同種露頭を一巡し、余分な平方根計算を避ける
            // Scan same-vein outcrops by squared distance and avoid unnecessary square roots
            var nearest = outcrops[0];
            var nearestDistance = (nearest.transform.position - position).sqrMagnitude;
            for (var i = 1; i < outcrops.Count; i++)
            {
                var candidate = outcrops[i];
                var candidateDistance = (candidate.transform.position - position).sqrMagnitude;
                if (nearestDistance <= candidateDistance) continue;
                nearest = candidate;
                nearestDistance = candidateDistance;
            }

            return nearest;
        }
    }
}
