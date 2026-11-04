using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Pruning
{
    /// <summary>
    /// 外したブロックのレール接続・列車を、外した原因に依らず外す
    /// Remove rail connections and trains of taken-out blocks regardless of cause
    /// 綴りはRailSegmentSaveData/TrainUnitSaveData/RailPositionSaveDataのフィールド名・プロパティ名（JsonProperty無し）に一致させる
    /// Spellings match the field and property names of RailSegmentSaveData/TrainUnitSaveData/RailPositionSaveData, which carry no JsonProperty
    /// </summary>
    public static class RemovedBlockRailCollateral
    {
        private const string SegmentStartKey = "A";
        private const string SegmentEndKey = "B";
        private const string TrainUnitInstanceIdKey = "TrainUnitInstanceId";
        private const string RailPositionSaveDataKey = "railPositionSaveData";
        private const string RailSnapshotKey = "RailSnapshot";

        // 残すとRailGraphDatastore.TryRestoreRailSegmentが端を解決できずfalseを返し、ロードで無音に消える
        // Left in place, RailGraphDatastore.TryRestoreRailSegment cannot resolve the end, returns false and the connection silently vanishes on load
        public static JArray RemoveRailSegmentsTouching(JArray railSegments, RemovedWorldBlockPositions removed)
        {
            var removedSegments = new JArray();
            if (removed.IsEmpty) return removedSegments;

            // 退避用に複製してから外す。後日の返金はレール種別と長さからコストを引く
            // Clone for the archive before removing; a later refund derives the cost from the rail type and length
            foreach (var segment in railSegments.OfType<JObject>().ToList())
            {
                if (!removed.ContainsConnectionDestination(segment[SegmentStartKey]) && !removed.ContainsConnectionDestination(segment[SegmentEndKey])) continue;

                removedSegments.Add(segment.DeepClone());
                segment.Remove();
            }

            return removedSegments;
        }

        // レールノードが1つでも解決できないと、RailPositionFactory.Restoreが位置を欠いたまま作るか、全滅でnullを返し列車が無音で消える
        // One unresolvable rail node makes RailPositionFactory.Restore build a position with gaps, or return null when none resolve so the train silently vanishes
        // 貨車と積荷ごと複製してから外し、後日の返金の入力に残す
        // Clone the train with its cars and cargo before removing it, so a later refund still has its input
        public static JArray RemoveTrainUnitsOnRemovedBlocks(JArray trainUnits, RemovedWorldBlockPositions removed)
        {
            var removedTrainUnits = new JArray();
            if (removed.IsEmpty) return removedTrainUnits;

            foreach (var trainUnit in trainUnits.OfType<JObject>().ToList())
            {
                // JSONのnullはJValueで返り?.を素通りするため、JObjectであることを型で確かめてから降りる
                // A JSON null comes back as a JValue that slips past ?., so check for JObject before descending
                if (trainUnit[RailPositionSaveDataKey] is not JObject railPositionSaveData || railPositionSaveData[RailSnapshotKey] is not JArray railSnapshot)
                {
                    Debug.LogWarning($"列車のレール位置が読めないため、除去したブロックのレールに載るかを判定せず残します。 trainUnitInstanceId={trainUnit[TrainUnitInstanceIdKey]}");
                    continue;
                }

                if (!railSnapshot.Any(removed.ContainsConnectionDestination)) continue;

                Debug.LogWarning($"除去したブロックのレールに載る列車を、貨車と積荷ごとセーブから除去します。 trainUnitInstanceId={trainUnit[TrainUnitInstanceIdKey]}");
                removedTrainUnits.Add(trainUnit.DeepClone());
                trainUnit.Remove();
            }

            return removedTrainUnits;
        }
    }
}
