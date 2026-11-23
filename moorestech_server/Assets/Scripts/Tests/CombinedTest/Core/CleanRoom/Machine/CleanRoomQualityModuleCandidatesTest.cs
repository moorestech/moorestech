using System.Linq;
using Core.Master;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.ItemsModule;
using NUnit.Framework;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomProcessMachineTestHelpers;

namespace Tests.CombinedTest.Core.CleanRoom.Machine
{
    /// <summary>
    ///     差分に並べる品質モジュール（刺さっている最高 tier を増やす・1つ上を刺す）が装着と空きに従うことを固定する
    ///     Pins that the listed quality modules (more of the highest equipped tier, one of the next tier) follow the loadout and free space
    /// </summary>
    public class CleanRoomQualityModuleCandidatesTest
    {
        // 工程用テスト機械はモジュール3枠・品質可。上限は研究前の3
        // The process test machine has 3 module slots and allows quality; the limit is 3 before research
        private const int ModuleSlotCount = 3;

        [Test]
        public void CandidatesFollowTheHighestEquippedTierAndFreeSpaceTest()
        {
            CleanRoomHatchTest.CreateServer();
            var machine = PlaceProcessMachine();
            var tier1 = QualityModule(1);
            var tier2 = QualityModule(2);

            // 何も刺さっていなければ tier1 を1個刺す候補だけ
            // With nothing equipped the only candidate is one tier-1 module
            Assert.That(CandidateTiers(machine), Is.EqualTo(new[] { 1 }));

            // tier1 が1個なら tier1 を増やす候補と tier2 を刺す候補
            // With one tier-1 module: more tier 1, then one tier 2
            SetModule(machine, 0, tier1, 1);
            Assert.That(CandidateTiers(machine), Is.EqualTo(new[] { 1, 2 }));

            // tier2 が最高なら tier2 を増やす候補だけ（テストマスタに tier3 は無い）
            // With tier 2 the highest, only more tier 2 remains (the test master has no tier 3)
            SetModule(machine, 0, tier2, 1);
            Assert.That(CandidateTiers(machine), Is.EqualTo(new[] { 2 }));

            // 3枠とも tier1×3 なら増やせず空きも無いので候補なし
            // With all three slots at tier1 x3 nothing can be added and no slot is free
            for (var slot = 0; slot < ModuleSlotCount; slot++) SetModule(machine, slot, tier1, 3);
            Assert.IsEmpty(CandidateTiers(machine));

            // 3枠とも tier1×1 なら tier1 は増やせるが、tier2 を刺す空きは無い
            // With all three slots at tier1 x1, tier 1 can grow but no slot is free for tier 2
            for (var slot = 0; slot < ModuleSlotCount; slot++) SetModule(machine, slot, tier1, 1);
            Assert.That(CandidateTiers(machine), Is.EqualTo(new[] { 1 }));
        }

        private static int[] CandidateTiers(IBlock machine)
        {
            var effectComponent = machine.GetComponent<MachineModuleEffectComponent>();
            var highestTier = MachineModuleEffect.Aggregate(effectComponent.CollectEffectiveModules()).HighestQualityModuleTier;
            return CleanRoomQualityModuleCandidates.Select(effectComponent, highestTier).Select(module => module.Tier).ToArray();
        }

        private static void SetModule(IBlock machine, int slot, ModuleMasterElement module, int count)
        {
            machine.GetComponent<IOpenableBlockInventoryComponent>().SetItem(FirstModuleSlot + slot, MasterHolder.ItemMaster.GetItemId(module.ItemGuid), count);
        }

        private static ModuleMasterElement QualityModule(int tier)
        {
            return MasterHolder.ItemMaster.Items.Modules.Single(module => module.EffectAxis == ModuleMasterElement.EffectAxisConst.Quality && module.Tier == tier);
        }
    }
}
