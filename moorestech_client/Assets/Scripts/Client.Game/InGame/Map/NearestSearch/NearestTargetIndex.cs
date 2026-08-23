using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Map.NearestSearch
{
    /// <summary>
    ///     key（mapObjectGuid・veinGuid等）別に独立したk-d treeを持つ最寄り索引。点集合の差し替えはkey単位の再構築
    ///     Nearest index holding one independent k-d tree per key (mapObjectGuid, veinGuid, ...); replacing a set rebuilds that key only
    /// </summary>
    public sealed class NearestTargetIndex<T> where T : class, INearestSearchTarget
    {
        private readonly Dictionary<Guid, KdTree<T>> _treesByKey = new();

        public void SetTargets(Guid key, IReadOnlyList<T> targets)
        {
            _treesByKey[key] = new KdTree<T>(targets);
        }

        public T SearchNearest(Guid key, Vector3 position)
        {
            return _treesByKey.TryGetValue(key, out var tree) ? tree.SearchNearest(position) : null;
        }
    }
}
