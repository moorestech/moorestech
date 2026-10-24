using System;
using System.Collections.Generic;
using System.Linq;
using Game.CleanRoom.Info;
using Game.CleanRoom.Util;
using Server.Util.MessagePack;

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
                CleanRoomEnclosedInfo enclosed => GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateEnclosed(enclosed.Rooms.Select(CreateSummary).ToList()),
                CleanRoomNotEnclosedInfo notEnclosed => GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateNotEnclosed(CreateLeak(notEnclosed.Leak)),
                _ => throw new ArgumentOutOfRangeException(nameof(info), info.GetType().Name, "未対応の部屋照会結果 / Unsupported room query result"),
            };

            #region Internal

            static CleanRoomSummaryMessagePack CreateSummary(CleanRoomRoomSummary room)
            {
                var filters = room.AirFilters.Select(filter => new CleanRoomAirFilterMessagePack(
                    new Vector3IntMessagePack(filter.Position),
                    filter.Status.PowerRatio,
                    filter.Status.RemovalVolumePerSecond,
                    filter.Status.FilterItemCount,
                    filter.Status.CurrentFilterRemainingRatio)).ToList();
                return room.Class switch
                {
                    CleanRoomClassifiedStanding classified => CleanRoomSummaryMessagePack.CreateClassified(classified.ClassName, classified.ThresholdIndex, room.Concentration, room.AirChangeRate, room.Volume, filters),
                    CleanRoomOutStanding _ => CleanRoomSummaryMessagePack.CreateOut(room.Concentration, room.AirChangeRate, room.Volume, filters),
                    _ => throw new ArgumentOutOfRangeException(nameof(room.Class), room.Class.GetType().Name, "未対応のクラス種別 / Unsupported class standing"),
                };
            }

            static CleanRoomLeakMessagePack CreateLeak(CleanRoomLeakReport leak)
            {
                // 種別ごとの型を種別ごとの factory へ写す。上限体積は TooLarge のときだけ載せる
                // Map each per-kind type onto its per-kind factory; the volume cap goes only with TooLarge
                return leak switch
                {
                    CleanRoomOpeningsLeak openings => CleanRoomLeakMessagePack.CreateOpenings(openings.OpeningCells.Select(cell => new Vector3IntMessagePack(cell)).ToList(), openings.TotalOpeningCount),
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
