using System;
using System.Collections.Generic;
using Core.Master.Validator.Block.Screen;
using Mooresmaster.Model.BlocksModule;
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

namespace Client.Ui.State.BlockInventory.Logic.Panel
{
    // ブロック在庫の本体ビューの種類。種類→ビューの組み立ては S4-alpha の画面側が持つ
    // Kinds of block-inventory body view; S4-alpha's screen maps each kind to a view
    public enum BlockInventoryBodyKind
    {
        SectionStack,
        FilterSplitter,
        ElectricToGear,
        TrainPlatform,
        ElectricPole,
        SpaceElevator,
        RocketLaunchPad,
    }

    public enum BlockInventoryTitleActionKind
    {
        None,
        RocketLaunchPadCommand,
    }

    public readonly struct BlockInventoryComponents
    {
        public BlockInventoryBodyKind Body { get; }
        public BlockInventoryTitleActionKind TitleAction { get; }

        public BlockInventoryComponents(BlockInventoryBodyKind body, BlockInventoryTitleActionKind titleAction)
        {
            Body = body;
            TitleAction = titleAction;
        }
    }

    /// <summary>
    /// マスタのブロック画面ID → 本文の種類と題の操作。None（開けない）と未登録は引けない
    /// Master block screen ID → body kind and title action; None (not openable) and unregistered IDs never resolve
    /// </summary>
    public static class BlockComponentRegistry
    {
        private static readonly IReadOnlyDictionary<string, BlockInventoryComponents> ByScreen = new Dictionary<string, BlockInventoryComponents>
        {
            [S.Generic] = Body(BlockInventoryBodyKind.SectionStack),
            [S.Chest] = Body(BlockInventoryBodyKind.SectionStack),
            [S.Machine] = Body(BlockInventoryBodyKind.SectionStack),
            [S.ElectricGenerator] = Body(BlockInventoryBodyKind.SectionStack),
            [S.FuelGearGenerator] = Body(BlockInventoryBodyKind.SectionStack),
            [S.GearToElectricGenerator] = Body(BlockInventoryBodyKind.SectionStack),
            [S.ElectricMiner] = Body(BlockInventoryBodyKind.SectionStack),
            [S.GearMiner] = Body(BlockInventoryBodyKind.SectionStack),
            [S.Pump] = Body(BlockInventoryBodyKind.SectionStack),
            [S.CleanRoomAirFilter] = Body(BlockInventoryBodyKind.SectionStack),
            [S.FluidPipe] = Body(BlockInventoryBodyKind.SectionStack),
            [S.FilterSplitter] = Body(BlockInventoryBodyKind.FilterSplitter),
            [S.ElectricToGearGenerator] = Body(BlockInventoryBodyKind.ElectricToGear),
            [S.TrainPlatform] = Body(BlockInventoryBodyKind.TrainPlatform),
            [S.ElectricPole] = Body(BlockInventoryBodyKind.ElectricPole),
            [S.SpaceElevatorBase] = Body(BlockInventoryBodyKind.SpaceElevator),
            [S.RocketLaunchPad] = new BlockInventoryComponents(BlockInventoryBodyKind.RocketLaunchPad, BlockInventoryTitleActionKind.RocketLaunchPadCommand),
        };

        public static bool TryResolveOpenable(BlockMasterElement master, out BlockInventoryComponents components)
        {
            // None は閉じる通常状態。未登録の画面は Resolve が診断する
            // None closes the panel normally, while Resolve diagnoses missing registrations
            if (BlockScreens.IsOpenable(master))
            {
                components = Resolve(master.BlockScreen);
                return true;
            }
            components = default;
            return false;
        }

        public static BlockInventoryComponents Resolve(string blockScreen)
        {
            // 未知の値はマスタ検証が止め、None は開けないので、ここへ来たら登録漏れ
            // Unknown values are stopped by master validation and None never opens, so reaching here is a missing registration
            if (blockScreen != null && ByScreen.TryGetValue(blockScreen, out var components)) return components;
            throw new InvalidOperationException($"ブロック画面ID {blockScreen} の本文が登録されていない");
        }

        private static BlockInventoryComponents Body(BlockInventoryBodyKind body)
        {
            return new BlockInventoryComponents(body, BlockInventoryTitleActionKind.None);
        }
    }
}
