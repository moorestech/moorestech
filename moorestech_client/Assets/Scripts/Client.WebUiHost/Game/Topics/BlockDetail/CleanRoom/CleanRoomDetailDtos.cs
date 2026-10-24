using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom
{
    /// <summary>
    ///     室内機械・清浄機が見せる部屋の状態。null のフィールドはキーごと省かれる（種別は TS の zod が閉じる）
    ///     Room state shown by interior machines and purifiers; null fields are omitted with their keys (the TS zod schema closes the kinds)
    /// </summary>
    public class CleanRoomDetailDto
    {
        // enclosed / openings / noEnclosure / tooLarge / detecting
        public string Kind;
        public CleanRoomSummaryDto Room;
        public int? OpeningCount;
        public int? MaxRoomVolume;

        // 停止中の室内機械だけ: notEnclosed / classOut
        // Only for a halted interior machine: notEnclosed / classOut
        public string HaltReason;
    }

    public class CleanRoomSummaryDto
    {
        // 見出しの種別 class / classOut。サーバーのクラス種別を写したもので、webui もツールチップもこれから見出しを選ぶ
        // Headline kind class / classOut, copied from the server's class kind; both the webui and the tooltip pick their headline from it
        public string Headline;

        // Headline が class のときだけ
        // Only when Headline is class
        public string ClassName;
        public double Concentration;
        public double AirChangeRate;
        public int Volume;
        public List<CleanRoomAirFilterDto> AirFilters;
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
