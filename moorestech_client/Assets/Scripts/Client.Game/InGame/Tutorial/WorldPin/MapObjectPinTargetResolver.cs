using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.MapObject;
using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    ///     後着中の空振りと確定欠落を分離する
    ///     Separates misses during streaming from confirmed absence
    /// </summary>
    internal sealed class MapObjectPinTargetResolver
    {
        private readonly IMapObjectPinTargetSource _targetSource;

        public MapObjectPinTargetResolver(IMapObjectPinTargetSource targetSource)
        {
            _targetSource = targetSource;
        }

        public bool TryResolve(
            HashSet<Guid> targetGuids,
            Vector3 position,
            bool missingReported,
            out MapObjectGameObject mapObject,
            out bool shouldReportMissing)
        {
            mapObject = _targetSource.SearchNearestMapObject(targetGuids, position);
            if (mapObject != null)
            {
                shouldReportMissing = false;
                return true;
            }

            shouldReportMissing = _targetSource.IsAllInstantiated.Value && !missingReported;
            return false;
        }
    }
}
