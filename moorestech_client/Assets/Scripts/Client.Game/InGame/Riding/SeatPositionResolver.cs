using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Riding
{
    public sealed class SeatPositionResolver : MonoBehaviour
    {
        private SeatPosition[] _seatPositions = Array.Empty<SeatPosition>();

        private void Awake()
        {
            // Prefab配下の座席markerをindex順に集めて保持する
            // Collect child seat markers ordered by index and cache them
            var markers = GetComponentsInChildren<SeatPosition>(true);
            if (markers.Length == 0)
            {
                _seatPositions = Array.Empty<SeatPosition>();
                return;
            }

            Array.Sort(markers, (left, right) => left.GetSeatIndex().CompareTo(right.GetSeatIndex()));
            _seatPositions = CollectUniqueMarkers(markers);
        }

        public bool TryGetSeatPosition(int seatIndex, out Transform seatTransform)
        {
            seatTransform = null;
            if (seatIndex < 0)
            {
                return false;
            }

            // cache済みmarkerから指定indexの座席Transformを探す
            // Find the requested seat Transform from cached markers
            for (var i = 0; i < _seatPositions.Length; i++)
            {
                if (_seatPositions[i].GetSeatIndex() != seatIndex)
                {
                    continue;
                }
                seatTransform = _seatPositions[i].transform;
                return true;
            }
            return false;
        }

        private SeatPosition[] CollectUniqueMarkers(SeatPosition[] markers)
        {
            // index重複はログを出して最初のmarkerだけを採用する
            // Log duplicate indexes and keep only the first marker
            var uniqueMarkers = new List<SeatPosition>(markers.Length);
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker.GetSeatIndex() < 0)
                {
                    Debug.LogError($"SeatPosition has negative index. Root:{name} SeatIndex:{marker.GetSeatIndex()}");
                    continue;
                }
                if (uniqueMarkers.Count > 0 && uniqueMarkers[uniqueMarkers.Count - 1].GetSeatIndex() == marker.GetSeatIndex())
                {
                    Debug.LogError($"SeatPosition index duplicated. Root:{name} SeatIndex:{marker.GetSeatIndex()}");
                    continue;
                }
                uniqueMarkers.Add(marker);
            }
            return uniqueMarkers.ToArray();
        }
    }
}
