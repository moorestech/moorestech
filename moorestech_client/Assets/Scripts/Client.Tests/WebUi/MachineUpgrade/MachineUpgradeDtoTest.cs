using System.Collections.Generic;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Client.WebUiHost.Game.Topics.MachineUpgrade;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi.MachineUpgrade
{
    /// <summary>
    ///     継承レシピのDTO（種類・継承元/先の変種・受け付ける最高レベル）と、スロット束縛の受け付けIDを固定する
    ///     Pins the inheritance recipe DTO (kind, source/target variants, max accepted level) and the slot binding's accepted IDs
    /// </summary>
    public class MachineUpgradeDtoTest
    {
        private ServiceProvider _serviceProvider;

        [SetUp]
        public void SetUp()
        {
            (_, _serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void CappedRecipeFollowsCraftCapTest()
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.CappedInheritRecipeGuid);
            var craftCapLookup = _serviceProvider.GetService<ILevelInheritanceCraftCapLookup>();

            // 未研究の上限つきレシピは Lv1 まで
            // An unresearched capped recipe accepts up to Lv1
            var before = MachineRecipeLevelInheritanceDtoBuilder.Build(recipe, craftCapLookup);
            Assert.AreEqual(MachineRecipeLevelInheritanceDtoBuilder.KindInheritUpToUnlocked, before.Kind);
            Assert.AreEqual(0, before.SourceInputIndex);
            Assert.AreEqual(0, before.TargetOutputIndex);
            CollectionAssert.AreEqual(Ids(ForUnitTestLevelInheritanceIds.Source(1), ForUnitTestLevelInheritanceIds.Source(2), ForUnitTestLevelInheritanceIds.Source(3)), before.SourceItemIds);
            CollectionAssert.AreEqual(Ids(ForUnitTestLevelInheritanceIds.CappedTarget(1), ForUnitTestLevelInheritanceIds.CappedTarget(2), ForUnitTestLevelInheritanceIds.CappedTarget(3)), before.TargetItemIds);
            Assert.AreEqual(1, before.MaxCraftableLevel);

            // 上限を2へ上げると最高レベルも2になる
            // Raising the cap to 2 raises the max level to 2
            _serviceProvider.GetService<ILevelInheritanceCraftCapUnlocker>().UnlockCraftCap(ForUnitTestLevelInheritanceIds.CappedTargetBaseGuid, 2);
            Assert.AreEqual(2, MachineRecipeLevelInheritanceDtoBuilder.Build(recipe, craftCapLookup).MaxCraftableLevel);
        }

        [Test]
        public void UncappedAndPlainRecipesTest()
        {
            var craftCapLookup = _serviceProvider.GetService<ILevelInheritanceCraftCapLookup>();

            // 上限なしはファミリー全長まで受け付ける
            // An uncapped recipe accepts the whole family length
            var inherit = MachineRecipeLevelInheritanceDtoBuilder.Build(MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.InheritRecipeGuid), craftCapLookup);
            Assert.AreEqual(MachineRecipeLevelInheritanceDtoBuilder.KindInherit, inherit.Kind);
            Assert.AreEqual(3, inherit.MaxCraftableLevel);

            // 継承しないレシピは -1・空・0
            // A non-inheritance recipe is -1, empty and 0
            var plain = MachineRecipeLevelInheritanceDtoBuilder.Build(MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0], craftCapLookup);
            Assert.AreEqual(MachineRecipeLevelInheritanceDtoBuilder.KindNone, plain.Kind);
            Assert.AreEqual(-1, plain.SourceInputIndex);
            Assert.AreEqual(-1, plain.TargetOutputIndex);
            Assert.IsEmpty(plain.SourceItemIds);
            Assert.IsEmpty(plain.TargetItemIds);
            Assert.AreEqual(0, plain.MaxCraftableLevel);
        }

        [Test]
        public void SourceSlotAcceptsEveryVariantTest()
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.InheritRecipeGuid);
            var machineParam = (IMachineParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestLevelInheritanceIds.MachineId).BlockParam;
            var bindings = MachineSlotBindingDtoBuilder.BuildSlotBindings(recipe, machineParam);

            // 継承元（入力0）は全変種、固定素材（入力1）は自分のIDだけ、出力はサーバーの出力束縛と同じく継承先ファミリーの全変種
            // The source (input 0) accepts every variant, the fixed input (input 1) only its own ID, and the output the whole target family as the server's output binding does
            CollectionAssert.AreEqual(Ids(ForUnitTestLevelInheritanceIds.Source(1), ForUnitTestLevelInheritanceIds.Source(2), ForUnitTestLevelInheritanceIds.Source(3)), bindings[0].AcceptedItemIds);
            CollectionAssert.AreEqual(new List<int> { bindings[1].ItemId }, bindings[1].AcceptedItemIds);
            CollectionAssert.AreEqual(Ids(ForUnitTestLevelInheritanceIds.Target(1), ForUnitTestLevelInheritanceIds.Target(2), ForUnitTestLevelInheritanceIds.Target(3)), bindings[2].AcceptedItemIds);
        }

        [Test]
        public void SubstituteInputAcceptsHigherVariantsTest()
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid);
            var machineParam = (IMachineParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestLevelInheritanceIds.MachineId).BlockParam;
            var bindings = MachineSlotBindingDtoBuilder.BuildSlotBindings(recipe, machineParam);

            // 継承しないレシピでも、基準を要求する入力は上位の変種を宛先に含む（C11 のサーバー規則と同じ）
            // Even in a non-inheritance recipe, an input requiring a base lists the higher variants (same as C11's server rule)
            CollectionAssert.AreEqual(Ids(ForUnitTestLevelInheritanceIds.Source(1), ForUnitTestLevelInheritanceIds.Source(2), ForUnitTestLevelInheritanceIds.Source(3)), bindings[0].AcceptedItemIds);
            CollectionAssert.AreEqual(new List<int> { bindings[1].ItemId }, bindings[1].AcceptedItemIds);
        }

        private static List<int> Ids(params ItemId[] itemIds)
        {
            var ids = new List<int>();
            foreach (var itemId in itemIds) ids.Add(itemId.AsPrimitive());
            return ids;
        }
    }
}
