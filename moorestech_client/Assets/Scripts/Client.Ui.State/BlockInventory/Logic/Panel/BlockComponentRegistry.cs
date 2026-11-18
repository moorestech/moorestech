using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;

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
    /// マスタの BlockType（PascalCase）→ ビューの種類の静的な表（Web blockComponentRegistry.ts の写し）
    /// Static table from the master BlockType (PascalCase) to the view kind (port of Web blockComponentRegistry.ts)
    /// </summary>
    public static class BlockComponentRegistry
    {
        // 未登録の種類は共通ビューへ戻す（専用 UI の無い液体ブロック等が落ちないように）
        // Unknown types fall back to the generic view (so blocks without a dedicated UI never break)
        private static readonly BlockInventoryComponents Default = new BlockInventoryComponents(BlockInventoryBodyKind.SectionStack, BlockInventoryTitleActionKind.None);

        public static readonly IReadOnlyDictionary<string, BlockInventoryComponents> Registered = new Dictionary<string, BlockInventoryComponents>
        {
            [BlockMasterElement.BlockTypeConst.FilterSplitter] = Body(BlockInventoryBodyKind.FilterSplitter),
            [BlockMasterElement.BlockTypeConst.Shaft] = Body(BlockInventoryBodyKind.SectionStack),
            [BlockMasterElement.BlockTypeConst.Gear] = Body(BlockInventoryBodyKind.SectionStack),
            [BlockMasterElement.BlockTypeConst.GearBeltConveyor] = Body(BlockInventoryBodyKind.SectionStack),
            [BlockMasterElement.BlockTypeConst.ElectricToGearGenerator] = Body(BlockInventoryBodyKind.ElectricToGear),
            [BlockMasterElement.BlockTypeConst.TrainStation] = Body(BlockInventoryBodyKind.TrainPlatform),
            [BlockMasterElement.BlockTypeConst.TrainItemPlatform] = Body(BlockInventoryBodyKind.TrainPlatform),
            [BlockMasterElement.BlockTypeConst.TrainFluidPlatform] = Body(BlockInventoryBodyKind.TrainPlatform),
            [BlockMasterElement.BlockTypeConst.ElectricPole] = Body(BlockInventoryBodyKind.ElectricPole),
            [BlockMasterElement.BlockTypeConst.SpaceElevatorBase] = Body(BlockInventoryBodyKind.SpaceElevator),
            [BlockMasterElement.BlockTypeConst.RocketLaunchPad] = new BlockInventoryComponents(BlockInventoryBodyKind.RocketLaunchPad, BlockInventoryTitleActionKind.RocketLaunchPadCommand),
        };

        public static BlockInventoryComponents Resolve(string blockType)
        {
            // silent-ok: 未登録は Chest・ElectricMachine 等の標準ビューの正規の経路で縮退ではない / 種類の取りこぼしは registeredBlockTypes.ts 一致テストが CI で知らせ、画面の欠けは S4-alpha の録画で見える
            // silent-ok: unregistered is the regular path for standard views (Chest, ElectricMachine...), not a degradation / missed kinds surface in the CI parity test and S4-alpha recordings
            return Registered.TryGetValue(blockType, out var components) ? components : Default;
        }

        private static BlockInventoryComponents Body(BlockInventoryBodyKind body)
        {
            return new BlockInventoryComponents(body, BlockInventoryTitleActionKind.None);
        }
    }
}
