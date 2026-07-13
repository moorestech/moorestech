using System;
using System.Text;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Machine.RecipeSelection;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Blueprint;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    /// <summary>
    /// ・選択中レシピをBP抽出
    /// ・別位置への設置で再現確認
    /// Verifies the selected recipe round-trips through blueprint extraction and placement.
    /// </summary>
    public class BlueprintMachineRecipeSelectionTest
    {
        [Test]
        public void SelectedRecipeRoundTripThroughBlueprintTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 元の機械にレシピを選択する
            // Select a recipe on the source machine
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var source);
            Assert.IsTrue(source.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var sourceSelector));
            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, sourceSelector.SetSelectedRecipe(recipe, overflow));

            // 範囲抽出でBP作成、JSON存在確認
            // Extract a blueprint and verify settings JSON is captured
            var created = BlueprintCreateService.TryCreateFromArea("machine", new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 0), out var blueprint);
            Assert.IsTrue(created);
            var settingsJson = blueprint.Blocks[0].Settings[MachineRecipeBlueprintSettingsComponent.SettingsKey];
            Assert.IsNotNull(settingsJson);

            // JSON付きで別座標へ設置し再現確認
            // Place a new machine with the settings param and verify reproduction
            var createParams = new[] { new BlockCreateParam(MachineRecipeBlueprintSettingsComponent.SettingsKey, Encoding.UTF8.GetBytes(settingsJson)) };
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, new Vector3Int(10, 0, 10), BlockDirection.North, createParams, out var pasted);
            Assert.IsTrue(pasted.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var pastedSelector));

            Assert.AreEqual(recipe.MachineRecipeGuid, pastedSelector.SelectedRecipeGuid);
        }

        [Test]
        public void OtherBlockRecipeGuidIsIgnoredOnApplyTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 他ブロック用レシピGuidのJSONを適用
            // Place block A and directly apply a settings JSON referencing block B's recipe
            var recipeA = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            MachineRecipeMasterElement recipeB = null;
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (r.BlockGuid != recipeA.BlockGuid) { recipeB = r; break; }
            }
            Assert.IsNotNull(recipeB, "テストモッドに2種類以上の機械ブロックのレシピが必要");

            var blockId = MasterHolder.BlockMaster.GetBlockId(recipeA.BlockGuid);
            var settingsJson = $"{{\"selectedRecipeGuid\":\"{recipeB.MachineRecipeGuid}\"}}";
            var createParams = new[] { new BlockCreateParam(MachineRecipeBlueprintSettingsComponent.SettingsKey, Encoding.UTF8.GetBytes(settingsJson)) };
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, new Vector3Int(0, 0, 0), BlockDirection.North, createParams, out var block);

            Assert.IsTrue(block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector));
            Assert.AreEqual(Guid.Empty, selector.SelectedRecipeGuid);
        }
    }
}
