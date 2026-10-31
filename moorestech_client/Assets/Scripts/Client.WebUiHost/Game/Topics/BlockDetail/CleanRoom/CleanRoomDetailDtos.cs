using System.Collections.Generic;
using Client.Game.InGame.BlockInfoPolling;
using Client.WebUiHost.Game.Topics.BlockDetail.FetchState;

namespace Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom
{
    /// <summary>
    ///     室内機械・清浄機が見せる部屋の状態。組み立ては種別ごとの factory だけで、null のフィールドはキーごと省かれる
    ///     Room state shown by interior machines and purifiers; built only through per-kind factories, and null fields are omitted with their keys
    /// </summary>
    public class CleanRoomDetailDto
    {
        // fetched / notFetched / transportFailed / unreadable / unsupportedTarget / vanished
        public readonly string FetchState;

        // fetched のときだけ: enclosed / openings / noEnclosure / tooLarge / detecting
        // Only when fetched: enclosed / openings / noEnclosure / tooLarge / detecting
        public readonly string Kind;
        public readonly CleanRoomSummaryDto Room;
        public readonly int? OpeningCount;
        public readonly int? MaxRoomVolume;

        // 停止中の室内機械だけ: notEnclosed / classOut
        // Only for a halted interior machine: notEnclosed / classOut
        public readonly string HaltReason;

        private CleanRoomDetailDto(string fetchState, string kind, CleanRoomSummaryDto room, int? openingCount, int? maxRoomVolume, string haltReason)
        {
            FetchState = fetchState;
            Kind = kind;
            Room = room;
            OpeningCount = openingCount;
            MaxRoomVolume = maxRoomVolume;
            HaltReason = haltReason;
        }

        public static CleanRoomDetailDto CreateEnclosed(CleanRoomSummaryDto room, string haltReason)
        {
            return new CleanRoomDetailDto(BlockInfoFetchStateWireName.Fetched, "enclosed", room, null, null, haltReason);
        }

        public static CleanRoomDetailDto CreateOpenings(int openingCount, string haltReason)
        {
            return new CleanRoomDetailDto(BlockInfoFetchStateWireName.Fetched, "openings", null, openingCount, null, haltReason);
        }

        public static CleanRoomDetailDto CreateNoEnclosure(string haltReason)
        {
            return new CleanRoomDetailDto(BlockInfoFetchStateWireName.Fetched, "noEnclosure", null, null, null, haltReason);
        }

        public static CleanRoomDetailDto CreateTooLarge(int maxRoomVolume, string haltReason)
        {
            return new CleanRoomDetailDto(BlockInfoFetchStateWireName.Fetched, "tooLarge", null, null, maxRoomVolume, haltReason);
        }

        public static CleanRoomDetailDto CreateDetecting(string haltReason)
        {
            return new CleanRoomDetailDto(BlockInfoFetchStateWireName.Fetched, "detecting", null, null, null, haltReason);
        }

        // 取得できていない間は取得状態だけを載せ、部屋の値を作らない
        // While not fetched, carry only the fetch state and make up no room values
        public static CleanRoomDetailDto CreateUnavailable(BlockInfoFetchState fetchState)
        {
            return new CleanRoomDetailDto(BlockInfoFetchStateWireName.ToUnavailableWireName(fetchState), null, null, null, null, null);
        }
    }

    public class CleanRoomSummaryDto
    {
        // 見出しの種別 class / classOut。サーバーのクラス種別を写したもので、webui もツールチップもこれから見出しを選ぶ
        // Headline kind class / classOut, copied from the server's class kind; both the webui and the tooltip pick their headline from it
        public readonly string Headline;

        // Headline が class のときだけ
        // Only when Headline is class
        public readonly string ClassName;
        public readonly double Concentration;
        public readonly double AirChangeRate;
        public readonly int Volume;
        public readonly IReadOnlyList<CleanRoomAirFilterDto> AirFilters;

        private CleanRoomSummaryDto(string headline, string className, double concentration, double airChangeRate, int volume, IReadOnlyList<CleanRoomAirFilterDto> airFilters)
        {
            Headline = headline;
            ClassName = className;
            Concentration = concentration;
            AirChangeRate = airChangeRate;
            Volume = volume;
            AirFilters = airFilters;
        }

        public static CleanRoomSummaryDto CreateClass(string className, double concentration, double airChangeRate, int volume, IReadOnlyList<CleanRoomAirFilterDto> airFilters)
        {
            return new CleanRoomSummaryDto("class", className, concentration, airChangeRate, volume, airFilters);
        }

        public static CleanRoomSummaryDto CreateClassOut(double concentration, double airChangeRate, int volume, IReadOnlyList<CleanRoomAirFilterDto> airFilters)
        {
            return new CleanRoomSummaryDto("classOut", null, concentration, airChangeRate, volume, airFilters);
        }
    }

    public class CleanRoomAirFilterDto
    {
        public int X;
        public int Y;
        public int Z;
        public double PowerRatio;
        public int FilterItemCount;
        public double FilterRemainingRatio;
    }
}
