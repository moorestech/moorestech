using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
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
    /// 駅・プラットフォームの駅名・移送モード・スロットの有無の唯一の判定（UITK の TrainPlatformBodyModel が呼ぶ）
    /// The single decision on a station/platform's name, transfer mode and slot presence (called by the UITK TrainPlatformBodyModel)
    /// </summary>
    public static class TrainPlatformDetailReader
    {
        public static OpenTrainPlatformBody Read(BlockMasterElement master, TrainStationNameStateDetail stationName, TrainPlatformTransferStateDetail transfer, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids)
        {
            // 駅名・移送モードはサーバーの StateDetail。届く前は名前は空・モードの切替は出さない
            // Name and mode are server StateDetails; before they arrive the name is empty and no mode switch shows
            // silent-ok: 駅名の StateDetail が届く前は空名の欄を出す / 届かないままなら欄が空のままで、録画 uitk/stage4/block/uitk-train-inventory 手順 2 の駅名の待ちが落ちて開発者が気づく
            var namePart = master.BlockType == BlockTypeConst.TrainStation
                ? SectionContent<string>.Present(stationName?.StationName ?? string.Empty)
                : SectionContent<string>.Absent;
            var mode = transfer == null ? SectionContent<TrainPlatformTransferMode>.Absent : SectionContent<TrainPlatformTransferMode>.Present(TrainPlatformTransferModeConversion.FromServer(transfer.Mode));

            // スロットは移送の状態があり、かつ品物の PF・駅のときだけ
            // Item slots only with a transfer state on an item platform or station
            var hasItemSlots = transfer != null && HasItemSlots(master.BlockParam);
            var slotsPart = hasItemSlots ? SectionContent<IReadOnlyList<SlotItem>>.Present(itemSlots) : SectionContent<IReadOnlyList<SlotItem>>.Absent;
            // ホームや液体欄の初回受信待ちと、表示する機能がないブロックを区別する
            // Distinguish platforms and fluid slots awaiting their first update from blocks with no displayable feature
            var needsPlatform = !namePart.IsPresent && transfer == null && fluids.Count == 0 &&
                                !HasItemSlots(master.BlockParam) && !HasFluidSlots(master.BlockParam);
            var unavailable = needsPlatform
                ? new UiText(LocalizationKeys.Ui.BlockInventory.ScreenRequiresTrainPlatform).WithLeadingParam("target", $"{master.Name} / {master.BlockScreen}")
                : null;
            return new OpenTrainPlatformBody(namePart, slotsPart, fluids, mode, unavailable);
        }

        private static bool HasFluidSlots(IBlockParam param)
        {
            // 配管は複製ノード、それ以外はサーバーのタンク情報が届くまで待つ
            // Pipes await a replica node, while tank-bearing blocks await server inventory details
            return param is FluidPipeBlockParam or CleanRoomPipeHatchBlockParam or IPumpParam or TrainFluidPlatformBlockParam ||
                   param is IMachineParam machine && (0 < machine.InputTankCount || 0 < machine.OutputTankCount) ||
                   param is ElectricGeneratorBlockParam generator && 0 < generator.FuelFluidTankCapacity;
        }

        // スロットを持つ型の唯一の列挙。品物 PF と駅だけが持つ
        // The single list of slot-bearing types; only item platforms and stations hold slots
        private static bool HasItemSlots(IBlockParam param)
        {
            return param is TrainItemPlatformBlockParam || param is TrainStationBlockParam;
        }
    }
}
