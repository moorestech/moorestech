using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Timetable;
using Client.Ui.State.Train;
using Client.Ui.State.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.WebUiHost.Game.Actions;
using UnityEngine;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    public static class TrainTimetableDtoBuilder
    {
        // 欠けの警告の重複抑止を publish を跨いで持つため、State の組み立て器を 1 つだけ持つ
        // Hold one State builder so the missing-station and unknown-side warning dedupe persists across publishes
        private static readonly TrainTimetableReadyDataFactory ReadyDataFactory = new();

        public static TrainTimetableStateDto Build(long trainCarInstanceId, TrainUnitClientCache cache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher fetcher, BlockGameObjectDataStore blocks)
        {
            // 状態の優先順は State の口が決める。ここでは DTO へ写すだけ
            // The State call decides the state priority; this only maps to the DTO
            var state = ReadyDataFactory.Build(trainCarInstanceId, cache, timetables, fetcher, blocks);
            if (state is ReadyTrainTimetableViewState ready) return TrainTimetableStateDto.Ready(ToDto(ready.Data));
            return state is LoadingTrainTimetableViewState ? TrainTimetableStateDto.Loading() : TrainTimetableStateDto.Unavailable();
        }

        // 用意済みの時刻表を wire の DTO へ写す
        // Map ready timetable data to the wire DTO
        internal static TrainTimetableDto ToDto(TrainTimetableReadyData data)
        {
            var stops = new List<TrainTimetableStopDto>(data.Stops.Count);
            foreach (var stop in data.Stops)
            {
                stops.Add(new TrainTimetableStopDto { Position = CreatePositionDto(stop.Position), Name = stop.Name, Side = TrainTimetableStopSideWire.ToWire(stop.Side) });
            }

            var stations = new List<TrainTimetableStationDto>(data.Stations.Count);
            foreach (var station in data.Stations)
            {
                stations.Add(new TrainTimetableStationDto { Position = CreatePositionDto(station.Position), Name = station.Name });
            }

            return new TrainTimetableDto
            {
                TrainUnitId = data.TrainUnitInstanceId.ToString(),
                IsAutoRun = data.IsAutoRun,
                CurrentIndex = data.CurrentIndex,
                Stops = stops,
                Stations = stations,
            };
        }

        private static TrainStationPositionDto CreatePositionDto(Vector3Int position)
        {
            return new TrainStationPositionDto { X = position.x, Y = position.y, Z = position.z };
        }
    }
}
