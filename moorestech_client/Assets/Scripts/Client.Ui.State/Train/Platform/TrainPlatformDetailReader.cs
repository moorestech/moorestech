using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;
using Game.Block.Blocks.TrainRail;
using Mooresmaster.Model.BlocksModule;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 駅・プラットフォームの描画値を組む（Web ホスト TrainStationDetailDtoBuilder・TrainPlatformDetailDtoBuilder と同じ規則。D-2）
    /// Builds station and platform draw values (same rules as the Web host TrainStationDetailDtoBuilder and TrainPlatformDetailDtoBuilder; D-2)
    /// </summary>
    public static class TrainPlatformDetailReader
    {
        public static OpenTrainPlatformBody Read(string blockType, IBlockParam param, TrainStationNameStateDetail stationName, TrainPlatformTransferStateDetail transfer, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids)
        {
            // 駅名・移送モードはサーバーの StateDetail。届く前は名前は空・モードの切替は出さない（Web ホストと同じ）
            // Name and mode are server StateDetails; before they arrive the name is empty and no mode switch shows (same as the Web host)
            // silent-ok: 駅名の StateDetail が届く前は空名の欄を出す（Web ホストと同じ）/ 届かないままなら欄が空のままで、録画 uitk/uitk-train-inventory 手順 2 の駅名の待ちが落ちて開発者が気づく
            var namePart = blockType == BlockTypeConst.TrainStation
                ? SectionContent<string>.Present(stationName?.StationName ?? string.Empty)
                : SectionContent<string>.Absent;
            var mode = transfer == null ? SectionContent<TrainPlatformTransferMode>.Absent : SectionContent<TrainPlatformTransferMode>.Present(TrainPlatformTransferModeConversion.FromServer(transfer.Mode));

            // スロットは移送の状態があり、かつ品物の PF・駅のときだけ（Web ホストは状態が無ければ詳細ごと出さない）
            // Item slots only with a transfer state on an item platform or station (the Web host omits the detail without a state)
            var hasItemSlots = transfer != null && param is TrainItemPlatformBlockParam or TrainStationBlockParam;
            var slotsPart = hasItemSlots ? SectionContent<IReadOnlyList<SlotItem>>.Present(itemSlots) : SectionContent<IReadOnlyList<SlotItem>>.Absent;
            return new OpenTrainPlatformBody(namePart, slotsPart, fluids, mode);
        }
    }
}
