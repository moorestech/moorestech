using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
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
        public static TrainTimetableDto Build(long trainCarInstanceId, TrainUnitClientCache cache, ClientTrainTimetableDatastore timetables, BlockGameObjectDataStore blocks)
        {
            if (!cache.TryGetCarSnapshot(new TrainCarInstanceId(trainCarInstanceId), out var unit, out _, out _, out _))
            {
                Debug.LogWarning($"[TrainTimetableDto] Missing car snapshot: {trainCarInstanceId}");
                return null;
            }

            // ワールド上の駅ブロックを列挙し、駅名は受信済みブロック状態から引く
            // Enumerate station blocks in the world; names come from the received block state
            var stations = new List<TrainTimetableStationDto>();
            foreach (var block in blocks.BlockGameObjectDictionary.Values)
            {
                if (block.BlockMasterElement.BlockType != BlockTypeConst.TrainStation) continue;
                var name = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName;
                stations.Add(CreateStationDto(block.BlockPosInfo.OriginalPos, name));
            }
            SortStations(stations);
            return CreateFromTimetable(unit.TrainUnitInstanceId, timetables, stations);
        }

        // 受信済みの時刻表を停車駅DTOへ写す。未着ならnull（取得はBlockInventoryTopicが起こす）
        // Map the received timetable to stop DTOs; null until received (BlockInventoryTopic triggers the fetch)
        internal static TrainTimetableDto CreateFromTimetable(TrainUnitInstanceId trainUnitInstanceId, ClientTrainTimetableDatastore timetables, List<TrainTimetableStationDto> stations)
        {
            if (!timetables.TryGet(trainUnitInstanceId, out var timetable))
            {
                Debug.Log($"[TrainTimetableDto] timetable not received yet: {trainUnitInstanceId}");
                return null;
            }

            var stationNames = new Dictionary<Vector3Int, string>();
            foreach (var station in stations)
            {
                stationNames[new Vector3Int(station.Position.X, station.Position.Y, station.Position.Z)] = station.Name;
            }

            var stops = new List<TrainTimetableStopDto>(timetable.Stops.Count);
            foreach (var stop in timetable.Stops)
            {
                var position = stop.StationPosition.Vector3Int;
                stationNames.TryGetValue(position, out var name);
                stops.Add(new TrainTimetableStopDto
                {
                    Position = CreatePositionDto(position),
                    Name = name ?? string.Empty,
                    Side = stop.Side == StationNodeSide.Front ? "front" : "back",
                });
            }

            return new TrainTimetableDto
            {
                TrainUnitId = trainUnitInstanceId.ToString(),
                IsAutoRun = timetable.IsAutoRun,
                CurrentIndex = timetable.CurrentIndex,
                Stops = stops,
                Stations = stations,
            };
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
