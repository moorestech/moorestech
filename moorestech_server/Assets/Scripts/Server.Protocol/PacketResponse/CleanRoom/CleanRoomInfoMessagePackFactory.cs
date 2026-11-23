using System;
using Game.CleanRoom.Info;
using Game.CleanRoom.Util;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     サーバーの部屋照会結果をプロトコル専用の型へ詰め替える
    ///     Converts the server's room query result into protocol-only types
    /// </summary>
    public static class CleanRoomInfoMessagePackFactory
    {
        public static GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack CreateResponse(CleanRoomInfo info)
        {
            // 種別ごとの型をそのまま種別ごとの factory へ写す
            // Map each per-kind type onto its per-kind factory
            return info switch
            {
                CleanRoomEnclosedInfo enclosed => GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateEnclosed(CreateSummary(enclosed.Room)),
                CleanRoomNotEnclosedInfo notEnclosed => GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateNotEnclosed(CreateLeak(notEnclosed.Leak)),
                _ => throw new ArgumentOutOfRangeException(nameof(info), info.GetType().Name, "未対応の部屋照会結果 / Unsupported room query result"),
            };

            #region Internal

            static CleanRoomSummaryMessagePack CreateSummary(CleanRoomRoomSummary room)
            {
                var queriedAirFilter = CreateQueriedAirFilter(room.QueriedFrom);
                return room.Class switch
                {
                    CleanRoomClassifiedStanding classified => CleanRoomSummaryMessagePack.CreateClassified(classified.ClassName, classified.ThresholdIndex, room.AirFilterCount, room.StoppedAirFilterCount, queriedAirFilter),
                    CleanRoomOutStanding _ => CleanRoomSummaryMessagePack.CreateOut(room.AirFilterCount, room.StoppedAirFilterCount, queriedAirFilter),
                    _ => throw new ArgumentOutOfRangeException(nameof(room.Class), room.Class.GetType().Name, "未対応のクラス種別 / Unsupported class standing"),
                };
            }

            // 照会の出どころと次のクラスの見通しを、照会した清浄機の1欄へ写す
            // Map the query source and next-class outlook onto the single queried-purifier field
            static CleanRoomQueriedAirFilterMessagePack CreateQueriedAirFilter(CleanRoomQueriedFrom queriedFrom)
            {
                return queriedFrom switch
                {
                    CleanRoomQueriedFromAirFilter { NextClass: CleanRoomUnitsToNextClassOutlook units } airFilter => CleanRoomQueriedAirFilterMessagePack.CreateAirFilterWithUnits(airFilter.Status.PowerRatio, airFilter.Status.CurrentFilterRemainingRatio, units.NextClassName, units.Units),
                    CleanRoomQueriedFromAirFilter { NextClass: CleanRoomNoUnitsToNextClassOutlook } airFilter => CleanRoomQueriedAirFilterMessagePack.CreateAirFilter(airFilter.Status.PowerRatio, airFilter.Status.CurrentFilterRemainingRatio),
                    CleanRoomQueriedFromNonAirFilter _ => CleanRoomQueriedAirFilterMessagePack.CreateNotAirFilter(),
                    _ => throw new ArgumentOutOfRangeException(nameof(queriedFrom), queriedFrom.GetType().Name, "未対応の照会の出どころ / Unsupported query source"),
                };
            }

            static CleanRoomLeakMessagePack CreateLeak(CleanRoomLeakReport leak)
            {
                // 種別ごとの型を種別ごとの factory へ写す。上限体積は TooLarge のときだけ載せる
                // Map each per-kind type onto its per-kind factory; the volume cap goes only with TooLarge
                return leak switch
                {
                    CleanRoomOpeningsLeak openings => CleanRoomLeakMessagePack.CreateOpenings(openings.TotalOpeningCount),
                    CleanRoomNoEnclosureLeak _ => CleanRoomLeakMessagePack.CreateNoEnclosure(),
                    CleanRoomTooLargeLeak _ => CleanRoomLeakMessagePack.CreateTooLarge(CleanRoomCellSets.MaxRoomVolume),
                    CleanRoomDetectionPendingLeak _ => CleanRoomLeakMessagePack.CreateDetectionPending(),
                    _ => throw new ArgumentOutOfRangeException(nameof(leak), leak.GetType().Name, "未対応の漏れ種別 / Unsupported leak kind"),
                };
            }

            #endregion
        }

    }
}
