using System;
using System.Collections.Generic;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    /// <summary>
    /// 部屋欄の各行のキーと引数を作る。赤（不足）は稼働を妨げている行だけに付ける（Web cleanRoomLogic.ts の写し）
    /// Builds keys and params for each room row; red (insufficient) marks only rows that block operation (port of Web cleanRoomLogic.ts)
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
                // 清浄機ごとの行。番号は 1 始まり、割合は四捨五入した百分率
                // One row per purifier, numbered from 1, ratios as rounded percentages
                var filters = new List<UiText>();
                for (var i = 0; i < room.AirFilters.Count; i++)
                {
                    var filter = room.AirFilters[i];
                    filters.Add(new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomFilter, new[]
                    {
                        new LocalizationTextParam("index", UiNumberFormat.Integer(i + 1)),
                        new LocalizationTextParam("power", RoundedPercent(filter.PowerRatio)),
                        new LocalizationTextParam("count", UiNumberFormat.Integer(filter.FilterItemCount)),
                        new LocalizationTextParam("remaining", RoundedPercent(filter.FilterRemainingRatio)),
                    }));
                }

                // 見出しはサーバーが決めた種別から選ぶ（Out の判定をここで持たない）
                // Pick the headline from the server-decided kind; no Out decision is made here
                var headline = room.Headline is ClassCleanRoomHeadline classHeadline
                    ? Line(LocalizationKeys.Ui.BlockInventory.CleanRoomClass, "className", classHeadline.ClassName)
                    : new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomClassOut);
                var air = new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomAir, new[]
                {
                    new LocalizationTextParam("concentration", UiNumberFormat.Fixed(room.Concentration, 3)),
                    new LocalizationTextParam("airChangeRate", UiNumberFormat.Fixed(room.AirChangeRate, 3)),
                });
                return new EnclosedCleanRoomDisplay(headline, room.Headline is ClassOutCleanRoomHeadline, air, filters, haltReason);
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
