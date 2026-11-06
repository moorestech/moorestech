using System;
using System.Collections.Generic;
using Client.Ui.State.BuildMenu.Logic;

namespace Client.Tests.Ui.State.BuildMenu
{
    /// <summary>
    /// Web buildMenuGrouping.test.ts と同じ GUID とラベルの固定データ
    /// Fixture data with the same GUIDs and labels as the Web buildMenuGrouping.test.ts
    /// </summary>
    public static class BuildMenuTestEntries
    {
        public static readonly Guid Mining = new("10000000-0000-4000-8000-000000000001");
        public static readonly Guid Logistics = new("10000000-0000-4000-8000-000000000002");
        public static readonly Guid Building = new("10000000-0000-4000-8000-000000000003");
        public static readonly Guid MinerSub = new("20000000-0000-4000-8000-000000000001");
        public static readonly Guid LiquidSub = new("20000000-0000-4000-8000-000000000002");
        public static readonly Guid ChestSub = new("20000000-0000-4000-8000-000000000003");
        public static readonly Guid ConveyorSub = new("20000000-0000-4000-8000-000000000004");
        public static readonly Guid FoundationSub = new("20000000-0000-4000-8000-000000000005");

        public static readonly IReadOnlyList<BuildMenuCategory> Categories = new[]
        {
            new BuildMenuCategory(Mining, new[] { MinerSub, LiquidSub }),
            new BuildMenuCategory(Logistics, new[] { ChestSub, ConveyorSub }),
            new BuildMenuCategory(Building, new[] { FoundationSub }),
        };

        // 配信順はチェスト→採掘機→コンベア
        // Delivery order is chest, miner, conveyor
        public static readonly IReadOnlyList<BuildMenuCatalogEntry> Entries = new[]
        {
            Entry("30000000-0000-4000-8000-000000000001", Logistics, ChestSub, "木のチェスト"),
            Entry("30000000-0000-4000-8000-000000000002", Mining, MinerSub, "鉄の採掘機"),
            Entry("30000000-0000-4000-8000-000000000003", Logistics, ConveyorSub, "ベルトコンベア"),
        };

        public static BuildMenuCatalogEntry Entry(string targetId, Guid categoryGuid, Guid subCategoryGuid, string displayLabel)
        {
            return new BuildMenuCatalogEntry(new Guid(targetId), categoryGuid, subCategoryGuid, displayLabel, Array.Empty<BuildMenuRequiredItem>(), false);
        }
    }
}
