using System;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockInfoPolling;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Server.Protocol.PacketResponse;

namespace Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom
{
    /// <summary>
    ///     - 部屋ビューと停止理由を結合
    ///     - Web表示用の部屋欄を生成
    ///     - Combines the room view and halt reason
    ///     - builds the web room section
    /// </summary>
    public static class CleanRoomDetailDtoBuilder
    {
        public static void Apply(BlockInventoryDto dto, BlockGameObject block, BlockNetworkInfoCache cache)
        {
            // 部屋の照会を追わないブロックは欄を持たない
            // Blocks that do not follow the room query have no section
            if (cache.CleanRoomTarget == CleanRoomDetailTarget.None) return;

            // 取得できていない間は取得状態だけを出す（警告はポーラーが出している）
            // While not fetched, show only the fetch state; the poller already logs the warnings
            var fetchState = cache.GetCleanRoom(out var view);
            if (fetchState != BlockInfoFetchState.Fetched)
            {
                dto.CleanRoom = CleanRoomDetailDto.CreateUnavailable(fetchState);
                return;
            }

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
                    throw new InvalidOperationException($"未対応の部屋欄の口 / Unsupported room section target: {cache.CleanRoomTarget}");
            }
        }

        // 室内機械用: 停止理由を必ず受け取る
        // For interior machines: the halt reason is always given
        public static CleanRoomDetailDto BuildForMachine(CleanRoomInfoView view, CleanRoomOperability operability)
        {
            return BuildBody(view, ToHaltReason(operability));

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
            return BuildBody(view, null);
        }

        private static CleanRoomDetailDto BuildBody(CleanRoomInfoView view, string haltReason)
        {
            return view switch
            {
                CleanRoomEnclosedView enclosed => CleanRoomDetailDto.CreateEnclosed(BuildRoom(enclosed.Rooms[0]), haltReason),
                CleanRoomNotEnclosedView notEnclosed => BuildLeak(notEnclosed.Leak),
                _ => throw new ArgumentOutOfRangeException(nameof(view), view.GetType().Name, "未対応の部屋照会の型 / Unsupported room query view"),
            };

            #region Internal

            CleanRoomDetailDto BuildLeak(CleanRoomLeakView leak)
            {
                // 漏れの型ごとに、その型だけが持つ件数か上限体積を添える
                // Attach the opening count or the volume cap that only that leak type carries
                return leak switch
                {
                    CleanRoomOpeningsLeakView openings => CleanRoomDetailDto.CreateOpenings(openings.TotalOpeningCount, haltReason),
                    CleanRoomNoEnclosureLeakView _ => CleanRoomDetailDto.CreateNoEnclosure(haltReason),
                    CleanRoomTooLargeLeakView tooLarge => CleanRoomDetailDto.CreateTooLarge(tooLarge.MaxRoomVolume, haltReason),
                    CleanRoomDetectionPendingLeakView _ => CleanRoomDetailDto.CreateDetecting(haltReason),
                    _ => throw new ArgumentOutOfRangeException(nameof(leak), leak.GetType().Name, "未対応の漏れ種別 / Unsupported leak kind"),
                };
            }

            static CleanRoomSummaryDto BuildRoom(CleanRoomSummaryMessagePack room)
            {
                // 見出しの種別はサーバーのクラス種別をそのまま写し、クラス名は分類済みのときだけ載せる
                // The headline kind copies the server's class kind, and the class name is attached only when classified
                var airFilters = room.AirFilters.Select(filter => new CleanRoomAirFilterDto
                {
                    X = filter.Position.X,
                    Y = filter.Position.Y,
                    Z = filter.Position.Z,
                    PowerRatio = filter.PowerRatio,
                    FilterItemCount = filter.FilterItemCount,
                    FilterRemainingRatio = filter.FilterRemainingRatio,
                }).ToList();
                return room.TryGetClass(out var className, out _)
                    ? CleanRoomSummaryDto.CreateClass(className, room.Concentration, room.AirChangeRate, room.Volume, airFilters)
                    : CleanRoomSummaryDto.CreateClassOut(room.Concentration, room.AirChangeRate, room.Volume, airFilters);
            }

            #endregion
        }
    }
}
