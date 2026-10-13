using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.WebUiHost.Game.Actions;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Train.RailGraph;
using Game.Train.Unit;
using UnityEngine;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    public static class TrainTimetableDtoBuilder
    {
        // 直前に警告した欠損集合。publishのたびに同じ警告を出さないために持つ
        // The last warned missing set; Build runs on every publish and would otherwise repeat the same warning
        private static readonly HashSet<Vector3Int> LastWarnedMissingStationPositions = new();

        public static TrainTimetableStateDto Build(long trainCarInstanceId, TrainUnitClientCache cache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher fetcher, BlockGameObjectDataStore blocks)
        {
            if (!OpenTrainUnitResolver.TryResolveOwningTrain(trainCarInstanceId, cache, out var trainUnitInstanceId))
            {
                Debug.LogWarning($"[TrainTimetableDto] Missing car snapshot: {trainCarInstanceId}");
                return TrainTimetableStateDto.Unavailable();
            }

            // 取得失敗と取得中はキャッシュより優先する
            // A failed or in-flight fetch outranks the cache so a stale timetable never stays shown as ready
            if (fetcher.IsUnavailable(trainUnitInstanceId)) return TrainTimetableStateDto.Unavailable();
            if (fetcher.IsFetchInFlight(trainUnitInstanceId)) return TrainTimetableStateDto.Loading();
            if (!timetables.TryGet(trainUnitInstanceId, out var timetable)) return TrainTimetableStateDto.Loading();

            // 駅は索引から引き、駅名はブロック状態から引く
            // Station blocks come from the datastore index; names come from the received block state
            var stations = new List<TrainTimetableStationDto>();
            foreach (var block in blocks.GetBlocksByBlockType(BlockTypeConst.TrainStation))
            {
                var name = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName;
                stations.Add(CreateStationDto(block.BlockPosInfo.OriginalPos, name));
            }
            SortStations(stations);
            var dto = CreateFromTimetable(timetable, stations);
            return dto == null ? TrainTimetableStateDto.Unavailable() : TrainTimetableStateDto.Ready(dto);
        }

        // 受信済み時刻表を停車駅DTOへ写す。未知の端ならnull
        // Map a received timetable to stop DTOs; null when it contains an unknown side
        internal static TrainTimetableDto CreateFromTimetable(TrainTimetableSnapshot timetable, List<TrainTimetableStationDto> stations)
        {
            var stationNames = new Dictionary<Vector3Int, string>();
            foreach (var station in stations)
            {
                stationNames[new Vector3Int(station.Position.X, station.Position.Y, station.Position.Z)] = station.Name;
            }

            var stops = new List<TrainTimetableStopDto>(timetable.Stops.Count);
            var missingStationPositions = new HashSet<Vector3Int>();
            foreach (var stop in timetable.Stops)
            {
                var wireSide = TrainTimetableStopSideWire.ToWire(stop.Side);
                if (wireSide == null)
                {
                    // 未知の端は時刻表全体をunavailableにする(fail-closed)
                    // An unknown side marks the whole timetable unavailable, not just this stop (fail-closed)
                    Debug.LogError($"[TrainTimetableDto] discarding timetable with unknown side: {timetable.TrainUnitInstanceId}");
                    return null;
                }
                var position = stop.StationPosition;
                // 駅ブロック未着の停車駅は名前なしで出し、欠損は後でまとめて警告する
                // A stop whose station block has not arrived is emitted nameless; the misses are warned about together below
                if (!stationNames.TryGetValue(position, out var name)) missingStationPositions.Add(position);
                stops.Add(new TrainTimetableStopDto
                {
                    Position = CreatePositionDto(position),
                    Name = name ?? string.Empty,
                    Side = wireSide,
                });
            }
            WarnMissingStationsWhenChanged(timetable.TrainUnitInstanceId, missingStationPositions);

            return new TrainTimetableDto
            {
                TrainUnitId = timetable.TrainUnitInstanceId.ToString(),
                IsAutoRun = timetable.IsAutoRun,
                CurrentIndex = timetable.CurrentIndex,
                Stops = stops,
                Stations = stations,
            };
        }

        // 欠損集合が変わったときだけ警告し、無音にもログ洪水にもしない
        // Warn only when the missing set changes, so the degradation stays visible without flooding the log
        private static void WarnMissingStationsWhenChanged(TrainUnitInstanceId trainUnitInstanceId, HashSet<Vector3Int> missingStationPositions)
        {
            if (LastWarnedMissingStationPositions.SetEquals(missingStationPositions)) return;
            LastWarnedMissingStationPositions.Clear();
            LastWarnedMissingStationPositions.UnionWith(missingStationPositions);
            if (missingStationPositions.Count == 0) return;
            Debug.LogWarning($"[TrainTimetableDto] station blocks not found for stops at {string.Join(", ", missingStationPositions)}; emitting empty names: {trainUnitInstanceId}");
        }

        internal static TrainTimetableStationDto CreateStationDto(Vector3Int position, string name)
        {
            return new TrainTimetableStationDto
            {
                Position = CreatePositionDto(position),
                Name = name ?? string.Empty,
            };
        }

        private static TrainStationPositionDto CreatePositionDto(Vector3Int position)
        {
            return new TrainStationPositionDto { X = position.x, Y = position.y, Z = position.z };
        }

        // 座標順で並べ、開くたびに順序が変わらないようにする
        // Sort by position so the list order is stable across opens
        internal static void SortStations(List<TrainTimetableStationDto> stations)
        {
            stations.Sort((a, b) =>
            {
                var byX = a.Position.X.CompareTo(b.Position.X);
                if (byX != 0) return byX;
                var byZ = a.Position.Z.CompareTo(b.Position.Z);
                return byZ != 0 ? byZ : a.Position.Y.CompareTo(b.Position.Y);
            });
        }
    }
}
