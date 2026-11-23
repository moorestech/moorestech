using System;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    /// <summary>
    /// 部屋欄の各行のキーと引数を作る。赤（不足）は稼働を妨げている行だけに付ける
    /// Builds keys and params for each room row; red (insufficient) marks only rows that block operation
    /// </summary>
    public static class CleanRoomLogic
    {
        public static CleanRoomDisplay Display(CleanRoomFetchedDetail cleanRoom)
        {
            var haltReason = cleanRoom.HaltReason;
            switch (cleanRoom)
            {
                case EnclosedCleanRoomDetail enclosed:
                    return EnclosedRows(enclosed.Room, haltReason);
                case OpeningsCleanRoomDetail openings:
                    return new HeadlineOnlyCleanRoomDisplay(Line(LocalizationKeys.Ui.BlockInventory.CleanRoomNotSealed, "count", UiNumberFormat.Integer(openings.OpeningCount)), true, haltReason);
                case NoEnclosureCleanRoomDetail _:
                    return new HeadlineOnlyCleanRoomDisplay(new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomNoEnclosure), true, haltReason);
                case TooLargeCleanRoomDetail tooLarge:
                    return new HeadlineOnlyCleanRoomDisplay(Line(LocalizationKeys.Ui.BlockInventory.CleanRoomTooLarge, "max", UiNumberFormat.Integer(tooLarge.MaxRoomVolume)), true, haltReason);
                case DetectingCleanRoomDetail _:
                    // 検出待ちは数 tick で解消するので赤にしない
                    // Pending detection resolves within ticks, so it is not red
                    return new HeadlineOnlyCleanRoomDisplay(new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomDetecting), false, haltReason);
                default:
                    throw new ArgumentOutOfRangeException(nameof(cleanRoom), cleanRoom.GetType().Name, null);
            }

            #region Internal

            CleanRoomDisplay EnclosedRows(CleanRoomSummary room, CleanRoomHaltReason haltReason)
            {
                // Out の判定はサーバーが決めた見出しの種別だけで行う
                // Out is read only from the server-decided headline kind
                var isOut = room.Headline is ClassOutCleanRoomHeadline;

                switch (room)
                {
                    // 室内機械は部屋クラスの1行だけ（ADR 0105 §1）
                    // An interior machine shows only the room-class line (ADR 0105 §1)
                    case MachineCleanRoomSummary _:
                        return new HeadlineOnlyCleanRoomDisplay(MachineHeadline(room.Headline), isOut, haltReason);

                    // 清浄機は台数つきの見出し・台数・この清浄機の3行（ADR 0105 §5）
                    // A purifier shows the headline with units, the count and its own status (ADR 0105 §5)
                    case AirFilterCleanRoomSummary airFilterRoom:
                        var airFilter = airFilterRoom.AirFilter;
                        return new AirFilterCleanRoomDisplay(PurifierHeadline(room.Headline, airFilter), isOut, CountLine(airFilter), QueriedLine(airFilter), haltReason);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(room), room.GetType().Name, null);
                }
            }

            UiText MachineHeadline(CleanRoomHeadline headline)
            {
                if (headline is ClassCleanRoomHeadline classHeadline) return Line(LocalizationKeys.Ui.BlockInventory.CleanRoomClass, "className", classHeadline.ClassName);
                return new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomClassOut);
            }

            // 台数で次のクラスへ届くときだけ「あとN台」を添える
            // Add "N more" only when units reach the next class
            UiText PurifierHeadline(CleanRoomHeadline headline, CleanRoomAirFilterRoomStatus airFilter)
            {
                var hasUnits = airFilter.TryGetUnitsToNextClass(out var nextClassName, out var units);
                if (headline is ClassCleanRoomHeadline classHeadline)
                {
                    if (!hasUnits) return Line(LocalizationKeys.Ui.BlockInventory.CleanRoomPurifierClass, "className", classHeadline.ClassName);
                    return new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomPurifierClassUnitsToNext, new[] { new LocalizationTextParam("className", classHeadline.ClassName), NextClassParam(nextClassName), UnitsParam(units) });
                }
                if (!hasUnits) return new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomClassOut);
                return new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomPurifierClassOutUnitsToNext, new[] { NextClassParam(nextClassName), UnitsParam(units) });
            }

            LocalizationTextParam NextClassParam(string nextClassName)
            {
                return new LocalizationTextParam("nextClassName", nextClassName);
            }

            LocalizationTextParam UnitsParam(int units)
            {
                return new LocalizationTextParam("units", UiNumberFormat.Integer(units));
            }

            // 止まった清浄機があるときだけ「うちN台」を添える。稼働を妨げる行ではないので赤にしない
            // Add "N stopped" only when some are stopped; it does not block operation, so it is not red
            UiText CountLine(CleanRoomAirFilterRoomStatus airFilter)
            {
                var count = new LocalizationTextParam("count", UiNumberFormat.Integer(airFilter.AirFilterCount));
                if (airFilter.StoppedAirFilterCount == 0) return new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomAirFilterCount, new[] { count });
                return new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomAirFilterCountStopped, new[] { count, new LocalizationTextParam("stopped", UiNumberFormat.Integer(airFilter.StoppedAirFilterCount)) });
            }

            UiText QueriedLine(CleanRoomAirFilterRoomStatus airFilter)
            {
                return new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomQueriedAirFilter, new[]
                {
                    new LocalizationTextParam("power", RoundedPercent(airFilter.PowerRatio)),
                    new LocalizationTextParam("remaining", RoundedPercent(airFilter.FilterRemainingRatio)),
                });
            }

            // 0..1 の割合を四捨五入した整数の百分率にする（Web の Math.round(x * 100)）
            // A 0..1 ratio as a rounded whole percentage (Web Math.round(x * 100))
            string RoundedPercent(double ratio)
            {
                return UiNumberFormat.Integer(UiNumberFormat.RoundHalfUp(ratio * 100));
            }

            #endregion
        }

        private static UiText Line(LocalizationKey key, string paramName, string paramValue)
        {
            return new UiText(key, new[] { new LocalizationTextParam(paramName, paramValue) });
        }
    }
}
