using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    /// <summary>
    ///     鉱脈別の最寄り露頭索引
    ///     Nearest-outcrop index by vein
    /// </summary>
    public sealed class OutcropGuidIndex
    {
        private readonly Dictionary<Guid, List<OutcropGameObject>> _outcropsByVeinGuid = new();

        public void Add(Guid veinGuid, OutcropGameObject outcrop)
        {
            if (!_outcropsByVeinGuid.TryGetValue(veinGuid, out var outcrops))
            {
                outcrops = new List<OutcropGameObject>();
                _outcropsByVeinGuid.Add(veinGuid, outcrops);
            }

            outcrops.Add(outcrop);
        }

        public OutcropGameObject SearchNearest(Guid veinGuid, Vector3 position)
        {
            if (!_outcropsByVeinGuid.TryGetValue(veinGuid, out var outcrops)) return null;

            // 平方距離で最寄りを探索
            // Find nearest by squared distance
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
