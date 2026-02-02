using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.RailCalc;
using UnityEngine;

namespace Game.Train.SaveLoad
{
    public class RailGraphSaveLoadService
    {
        private readonly IRailGraphDatastore _railGraphDatastore;

        public RailGraphSaveLoadService(IRailGraphDatastore railGraphDatastore)
        {
            _railGraphDatastore = railGraphDatastore;
        }

        // レールセグメントの保存データを作成する
        // Build save data for rail segments
        public List<RailSegmentSaveData> GetSaveData()
        {
            var results = new List<RailSegmentSaveData>();
            var segments = _railGraphDatastore.GetRailSegments();
            foreach (var segment in segments)
            {
                if (!TryCreateSaveData(segment, out var saveData))
                    continue;
                results.Add(saveData);
            }
            return results;
        }

        // 保存データからレールセグメントを復元する
        // Restore rail segments from save data
        public void RestoreRailSegments(IEnumerable<RailSegmentSaveData> segments)
        {
            if (segments == null)
                return;

            foreach (var segment in segments)
            {
                if (segment == null)
                    continue;
                // 描画対象の距離検証と補正
                // Validate and fix drawable segment length
                var length = segment.Length;
                if (segment.IsDrawable && TryResolveExpectedLength(segment.A, segment.B, out var expectedLength) && expectedLength != length)
                {
                    var a = segment.A;
                    var b = segment.B;
                    Debug.LogWarning($"[RailGraphSaveLoadService] RailSegment length mismatch. saved={length} expected={expectedLength} A=({a.blockPosition.x},{a.blockPosition.y},{a.blockPosition.z},{a.componentIndex},{a.IsFront}) B=({b.blockPosition.x},{b.blockPosition.y},{b.blockPosition.z},{b.componentIndex},{b.IsFront})");
                    length = expectedLength;
                }
                _railGraphDatastore.TryRestoreRailSegment(segment.A, segment.B, length, segment.RailTypeGuid, segment.IsDrawable);
            }
        }

        #region Internal

        // セグメントから保存データを組み立てる
        // Build save payload from a segment
        private bool TryCreateSaveData(RailSegment segment, out RailSegmentSaveData saveData)
        {
            saveData = null;
            if (segment == null)
                return false;
            if (!_railGraphDatastore.TryGetRailNode(segment.StartNodeId, out var startNode))
                return false;
            if (!_railGraphDatastore.TryGetRailNode(segment.EndNodeId, out var endNode))
                return false;
            if (startNode.ConnectionDestination.IsDefault() || endNode.ConnectionDestination.IsDefault())
                return false;

            saveData = new RailSegmentSaveData
            {
                A = startNode.ConnectionDestination,
                B = endNode.ConnectionDestination,
                Length = segment.Length,
                RailTypeGuid = segment.RailTypeGuid,
                IsDrawable = segment.IsDrawable
            };
            return true;
        }

        // 描画対象の距離検証を行う
        // Validate drawable segment length
        private bool TryResolveExpectedLength(ConnectionDestination start, ConnectionDestination end, out int expectedLength)
        {
            expectedLength = 0;
            var startNode = _railGraphDatastore.ResolveRailNode(start);
            if (startNode == null)
                return false;
            var endNode = _railGraphDatastore.ResolveRailNode(end);
            if (endNode == null)
                return false;
            var rawLength = BezierUtility.GetBezierCurveLength(startNode, endNode);
            var scaledLength = rawLength * BezierUtility.RAIL_LENGTH_SCALE;
            expectedLength = (int)(scaledLength + 0.5f);
            return true;
        }

        #endregion
    }
}
