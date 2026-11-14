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
    /// 駅・プラットフォームの駅名・移送モード・スロットの有無の唯一の判定（UITK の TrainPlatformBodyModel と Web ホストの TrainPlatformDetailDtoBuilder が呼ぶ。DC-23 A）
    /// The single decision on a station/platform's name, transfer mode and slot presence (called by the UITK TrainPlatformBodyModel and the Web host TrainPlatformDetailDtoBuilder; DC-23 A)
    /// </summary>
    public static class TrainPlatformDetailReader
    {
        public static OpenTrainPlatformBody Read(string blockType, IBlockParam param, TrainStationNameStateDetail stationName, TrainPlatformTransferStateDetail transfer, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids)
        {
            // 駅名・移送モードはサーバーの StateDetail。届く前は名前は空・モードの切替は出さない
            // Name and mode are server StateDetails; before they arrive the name is empty and no mode switch shows
            // silent-ok: 駅名の StateDetail が届く前は空名の欄を出す / 届かないままなら欄が空のままで、録画 uitk/stage4/uitk-train-inventory 手順 2 の駅名の待ちが落ちて開発者が気づく
            var namePart = blockType == BlockTypeConst.TrainStation
                ? SectionContent<string>.Present(stationName?.StationName ?? string.Empty)
                : SectionContent<string>.Absent;
            var mode = transfer == null ? SectionContent<TrainPlatformTransferMode>.Absent : SectionContent<TrainPlatformTransferMode>.Present(TrainPlatformTransferModeConversion.FromServer(transfer.Mode));

            // スロットは移送の状態があり、かつ品物の PF・駅のときだけ
            // Item slots only with a transfer state on an item platform or station
            var hasItemSlots = transfer != null && param is TrainItemPlatformBlockParam or TrainStationBlockParam;
            var slotsPart = hasItemSlots ? SectionContent<IReadOnlyList<SlotItem>>.Present(itemSlots) : SectionContent<IReadOnlyList<SlotItem>>.Absent;
            return new OpenTrainPlatformBody(namePart, slotsPart, fluids, mode);
        }
    }
}
