using System;
using System.Linq;
using Client.Game.InGame.Block;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom
{
    /// <summary>
    ///     部屋照会の種別ごとの型と機械の停止理由から、webui に送る部屋欄を組み立てる
    ///     Builds the room section for the webui from the per-kind room query view and the machine's halt reason
    /// </summary>
    public static class CleanRoomDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, BlockNetworkInfoCache cache)
        {
            // 未取得・ブロック消失の間は欄を出さない（消失と通信失敗の警告はポーラーが出している）
            // Omit the section while not fetched or vanished; the poller already warns about vanishing and transport failures
            if (!cache.TryGetCleanRoom(out var view)) return;

            // 口はキャッシュの判定（CleanRoomTarget）で選び、ここで param を見直さない
            // Pick the entry point from the cache's decision (CleanRoomTarget) instead of re-checking the param here
            switch (cache.CleanRoomTarget)
            {
                case CleanRoomDetailTarget.AirFilter:
                    dto.CleanRoom = BuildForAirFilter(view);
                    return;
                case CleanRoomDetailTarget.Machine:
                    // silent-ok: 機械の状態がまだ届いていない間は欄を出さない（前例 PumpDetailDtoBuilder）/ 状態イベントの到着で再配信されるので、欄が出続けなければ開いた画面で見える
                    var operability = block.GetStateDetail<CleanRoomOperabilityStateDetail>(CleanRoomOperabilityStateDetail.BlockStateDetailKey);
                    if (operability == null) return;
                    if (!Enum.IsDefined(typeof(CleanRoomOperability), operability.Operability))
                    {
                        UnityEngine.Debug.LogWarning($"Unreadable clean-room operability: {operability.Operability}");
                        dto.CleanRoom = null;
                        return;
                    }
                    dto.CleanRoom = BuildForMachine(view, operability.Operability);
                    return;
                default:
                    // 部屋の照会を追うのは室内機械と清浄機だけなので、それ以外で値があるのは配線の誤り
                    // Only interior machines and purifiers follow the room query, so a value for any other target is a wiring bug
                    throw new InvalidOperationException($"部屋の照会を追わない対象に部屋の状態がある / Room state exists for a target that does not follow the room query: {cache.CleanRoomTarget}");
            }
        }

        // 室内機械用: 停止理由を必ず受け取る
        // For interior machines: the halt reason is always given
        public static CleanRoomDetailDto BuildForMachine(CleanRoomInfoView view, CleanRoomOperability operability)
        {
            var dto = BuildBody(view);
            dto.HaltReason = ToHaltReason(operability);
            return dto;

            #region Internal

            static string ToHaltReason(CleanRoomOperability operability)
            {
                return operability switch
                {
                    CleanRoomOperability.NotEnclosed => "notEnclosed",
                    CleanRoomOperability.ClassOut => "classOut",
                    CleanRoomOperability.Operable => null,
                    _ => throw new ArgumentOutOfRangeException(nameof(operability), operability, "未対応の稼働可否 / Unsupported operability"),
                };
            }

            #endregion
        }

        // 清浄機用: 停止理由の状態を持たないので引数に取らない
        // For purifiers: they carry no halt-reason state, so none is taken
        public static CleanRoomDetailDto BuildForAirFilter(CleanRoomInfoView view)
        {
            return BuildBody(view);
        }

        private static CleanRoomDetailDto BuildBody(CleanRoomInfoView view)
        {
            return view switch
            {
                CleanRoomEnclosedView enclosed => new CleanRoomDetailDto { Kind = "enclosed", Room = BuildRoom(enclosed.Rooms[0]) },
                CleanRoomNotEnclosedView notEnclosed => BuildLeak(notEnclosed.Leak),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view.GetType().Name, "未対応の部屋照会の型 / Unsupported room query view"),
            };

            #region Internal

            static CleanRoomDetailDto BuildLeak(CleanRoomLeakView leak)
            {
                // 漏れの型ごとに、その型だけが持つ件数か上限体積を添える
                // Attach the opening count or the volume cap that only that leak type carries
                return leak switch
                {
                    CleanRoomOpeningsLeakView openings => new CleanRoomDetailDto { Kind = "openings", OpeningCount = openings.TotalOpeningCount },
                    CleanRoomNoEnclosureLeakView _ => new CleanRoomDetailDto { Kind = "noEnclosure" },
                    CleanRoomTooLargeLeakView tooLarge => new CleanRoomDetailDto { Kind = "tooLarge", MaxRoomVolume = tooLarge.MaxRoomVolume },
                    CleanRoomDetectionPendingLeakView _ => new CleanRoomDetailDto { Kind = "detecting" },
                    _ => throw new ArgumentOutOfRangeException(nameof(leak), leak.GetType().Name, "未対応の漏れ種別 / Unsupported leak kind"),
                };
            }

            static CleanRoomSummaryDto BuildRoom(CleanRoomSummaryMessagePack room)
            {
                // 見出しの種別はサーバーのクラス種別をそのまま写し、クラス名は分類済みのときだけ載せる
                // The headline kind copies the server's class kind, and the class name is attached only when classified
                var classified = room.TryGetClass(out var className, out _);
                return new CleanRoomSummaryDto
                {
                    Headline = classified ? "class" : "classOut",
                    ClassName = classified ? className : null,
                    Concentration = room.Concentration,
                    AirChangeRate = room.AirChangeRate,
                    Volume = room.Volume,
                    AirFilters = room.AirFilters.Select(filter => new CleanRoomAirFilterDto
                    {
                        X = filter.Position.X,
                        Y = filter.Position.Y,
                        Z = filter.Position.Z,
                        PowerRatio = filter.PowerRatio,
                        FilterItemCount = filter.FilterItemCount,
                        FilterRemainingRatio = filter.FilterRemainingRatio,
                    }).ToList(),
                };
            }

            #endregion
        }

    }
}
