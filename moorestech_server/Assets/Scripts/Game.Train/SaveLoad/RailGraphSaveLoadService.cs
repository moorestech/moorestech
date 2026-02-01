using System.Collections.Generic;
using Game.Train.RailGraph;

namespace Game.Train.SaveLoad
{
    public class RailGraphSaveLoadService
    {
        private readonly IRailGraphDatastore _railGraphDatastore;

        public RailGraphSaveLoadService(IRailGraphDatastore railGraphDatastore)
        {
            _railGraphDatastore = railGraphDatastore;
        }

        // レールセグメントの保存データを生成する
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
                _railGraphDatastore.TryRestoreRailSegment(segment.A, segment.B, segment.Length, segment.BezierStrength);
            }
        }

        #region Internal

        // セグメント情報から保存データを組み立てる
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
                BezierStrength = segment.BezierStrength
            };
            return true;
        }

        #endregion
    }
}
