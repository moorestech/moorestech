using System;
using System.Collections.Generic;
using System.Globalization;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.CleanRoom
{
    /// <summary>
    ///     部屋照会の種別ごとの型をカーソルツールチップの行にする（密閉済みは部屋ごとに3行、未密閉は理由1行）
    ///     Turns the per-kind room query view into cursor tooltip lines: three per sealed room, one reason line otherwise
    /// </summary>
    public static class CleanRoomTooltipLines
    {
        public static TooltipLine[] Build(CleanRoomInfoView view)
        {
            return view switch
            {
                CleanRoomEnclosedView enclosed => BuildEnclosed(enclosed.Rooms),
                CleanRoomNotEnclosedView notEnclosed => new[] { BuildLeak(notEnclosed.Leak) },
                _ => throw new ArgumentOutOfRangeException(nameof(view), view.GetType().Name, "未対応の部屋照会の型 / Unsupported room query view"),
            };

            #region Internal

            static TooltipLine[] BuildEnclosed(IReadOnlyList<CleanRoomSummaryMessagePack> rooms)
            {
                // 共有壁は両側の部屋を順に並べる。見出しはサーバーのクラス種別から選ぶ
                // A shared wall lists both rooms in order; the headline is picked from the server's class kind
                var lines = new List<TooltipLine>();
                foreach (var room in rooms)
                {
                    lines.Add(room.TryGetClass(out var className, out _) ? new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClass, new[] { className }) : new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClassOut));
                    lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomAir, new[] { Format3(room.Concentration), Format3(room.AirChangeRate) }));
                    lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomFilters, new[] { room.AirFilters.Count.ToString(CultureInfo.InvariantCulture) }));
                }

                return lines.ToArray();

                #region Internal

                static string Format3(double value)
                {
                    return value.ToString("F3", CultureInfo.InvariantCulture);
                }

                #endregion
            }

            static TooltipLine BuildLeak(CleanRoomLeakView leak)
            {
                // 漏れの型ごとに、その型だけが持つ件数か上限体積を文言へ渡す
                // Pass the opening count or the volume cap that only that leak type carries into the text
                return leak switch
                {
                    CleanRoomOpeningsLeakView openings => new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomNotSealed, new[] { openings.TotalOpeningCount.ToString(CultureInfo.InvariantCulture) }),
                    CleanRoomNoEnclosureLeakView _ => new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomNoEnclosure),
                    CleanRoomTooLargeLeakView tooLarge => new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomTooLarge, new[] { tooLarge.MaxRoomVolume.ToString(CultureInfo.InvariantCulture) }),
                    CleanRoomDetectionPendingLeakView _ => new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomDetecting),
                    _ => throw new ArgumentOutOfRangeException(nameof(leak), leak.GetType().Name, "未対応の漏れ種別 / Unsupported leak kind"),
                };
            }

            #endregion
        }

    }
}
