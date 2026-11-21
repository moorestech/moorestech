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
                var (nextClass, queriedAirFilter) = CreateQueriedFrom(room.QueriedFrom);
                return room.Class switch
                {
                    CleanRoomClassifiedStanding classified => CleanRoomSummaryMessagePack.CreateClassified(classified.ClassName, classified.ThresholdIndex, room.AirFilterCount, room.StoppedAirFilterCount, nextClass, queriedAirFilter),
                    CleanRoomOutStanding _ => CleanRoomSummaryMessagePack.CreateOut(room.AirFilterCount, room.StoppedAirFilterCount, nextClass, queriedAirFilter),
                    _ => throw new ArgumentOutOfRangeException(nameof(room.Class), room.Class.GetType().Name, "未対応のクラス種別 / Unsupported class standing"),
                };
            }

            // 照会の出どころを、次のクラスと照会した清浄機の状態の2つの欄へ1回の分岐で写す。清浄機以外からの照会は wire の NotCounted と NotAirFilter になる
            // Map the query source onto both the next-class and queried-purifier fields in one branch; a non-purifier query becomes NotCounted and NotAirFilter on the wire
            static (CleanRoomNextClassMessagePack nextClass, CleanRoomQueriedAirFilterMessagePack queriedAirFilter) CreateQueriedFrom(CleanRoomQueriedFrom queriedFrom)
            {
                return queriedFrom switch
                {
                    CleanRoomQueriedFromAirFilter airFilter => (CreateNextClass(airFilter.NextClass), CleanRoomQueriedAirFilterMessagePack.CreateAirFilter(airFilter.Status.PowerRatio, airFilter.Status.CurrentFilterRemainingRatio)),
                    CleanRoomQueriedFromNonAirFilter _ => (CleanRoomNextClassMessagePack.CreateNotCounted(), CleanRoomQueriedAirFilterMessagePack.CreateNotAirFilter()),
                    _ => throw new ArgumentOutOfRangeException(nameof(queriedFrom), queriedFrom.GetType().Name, "未対応の照会の出どころ / Unsupported query source"),
                };
            }

            // 見通しの種別ごとの型を種別ごとの factory へ写す
            // Map each per-kind outlook type onto its per-kind factory
            static CleanRoomNextClassMessagePack CreateNextClass(CleanRoomNextClassOutlook outlook)
            {
                return outlook switch
                {
                    CleanRoomAtBestClassOutlook _ => CleanRoomNextClassMessagePack.CreateAtBestClass(),
                    CleanRoomUnitsToNextClassOutlook units => CleanRoomNextClassMessagePack.CreateUnitsToNextClass(units.NextClassName, units.Units),
                    CleanRoomNextClassUnreachableOutlook unreachable => CleanRoomNextClassMessagePack.CreateUnreachable(unreachable.NextClassName),
                    _ => throw new ArgumentOutOfRangeException(nameof(outlook), outlook.GetType().Name, "未対応の次のクラスの見通し / Unsupported next-class outlook"),
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
