using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Unit;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Train.Unit;
using UnityEngine;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    public static class TrainTimetableDtoBuilder
    {
        public static TrainTimetableDto Build(long trainCarInstanceId, TrainUnitClientCache cache, BlockGameObjectDataStore blocks)
        {
            if (!cache.TryGetCarSnapshot(new TrainCarInstanceId(trainCarInstanceId), out var unit, out _, out _, out _))
            {
                Debug.LogWarning($"[TrainTimetableDto] Missing car snapshot: {trainCarInstanceId}");
                return null;
            }

            // ワールド上の駅ブロックを列挙し、駅名は受信済みブロック状態から引く
            // Enumerate station blocks in the world; names come from the received block state
            var stationNames = new Dictionary<Vector3Int, string>();
            var stations = new List<TrainTimetableStationDto>();
            foreach (var block in blocks.BlockGameObjectDictionary.Values)
            {
                if (block.BlockMasterElement.BlockType != BlockTypeConst.TrainStation) continue;
                var name = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName;
                var position = block.BlockPosInfo.OriginalPos;
                stationNames[position] = name ?? string.Empty;
                stations.Add(CreateStationDto(position, name));
            }
            SortStations(stations);

            var stops = new List<TrainTimetableStationDto>(unit.TimetableStops.Count);
            foreach (var stop in unit.TimetableStops)
            {
                stationNames.TryGetValue(stop.StationPosition, out var name);
                stops.Add(CreateStationDto(stop.StationPosition, name));
            }

            return new TrainTimetableDto
            {
                TrainUnitId = unit.TrainUnitInstanceId.ToString(),
                IsAutoRun = unit.IsAutoRun,
                CurrentIndex = unit.TimetableCurrentIndex,
                Stops = stops,
                Stations = stations,
            };
        }

        internal static TrainTimetableStationDto CreateStationDto(Vector3Int position, string name)
        {
            return new TrainTimetableStationDto
            {
                Position = new TrainStationPositionDto { X = position.x, Y = position.y, Z = position.z },
                Name = name ?? string.Empty,
            };
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
