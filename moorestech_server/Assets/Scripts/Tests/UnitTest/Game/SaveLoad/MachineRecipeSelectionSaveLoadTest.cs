using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    // 選択中レシピのセーブ/ロード往復を検証する
    // Verifies the save/load round trip of the selected recipe
    public class MachineRecipeSelectionSaveLoadTest
    {
        [Test]
        public void SelectedRecipeIsRestoredTest()
        {
            var (worldBlockDatastore, assembleSaveJsonText, _) = CreateBlockTestModule();

            // 機械を設置してレシピだけ選択する（加工はさせない）
            // Place the machine and select a recipe without letting it process
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            worldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);

            Assert.IsTrue(machineBlock.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector));
            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, selector.SetSelectedRecipe(recipe, overflow));

            var json = assembleSaveJsonText.AssembleSaveJson();
            worldBlockDatastore.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);

            var (loadWorldBlockDatastore, _, loadJsonFile) = CreateBlockTestModule();
            loadJsonFile.Load(json);

            var loadedBlock = loadWorldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.IsTrue(loadedBlock.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var loadedSelector));
            Assert.AreEqual(recipe.MachineRecipeGuid, loadedSelector.SelectedRecipeGuid);
        }

        [Test]
        public void MissingSelectedRecipeGuidFallsBackToUnselectedTest()
        {
            var (worldBlockDatastore, assembleSaveJsonText, _) = CreateBlockTestModule();

            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            worldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);

            Assert.IsTrue(machineBlock.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector));
            var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
            selector.SetSelectedRecipe(recipe, overflow);

            var json = assembleSaveJsonText.AssembleSaveJson();
            worldBlockDatastore.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);

            // セーブJSON中のselectedRecipeGuidをマスタに存在しないGUIDへ書き換える
            // componentStatesはネストしたJSON文字列のため引用符はエスケープされている
            // Rewrite selectedRecipeGuid in the save JSON to a GUID absent from the master
            // componentStates is a nested JSON string, so quotes appear escaped
            var brokenGuid = Guid.NewGuid();
            while (MasterHolder.MachineRecipesMaster.GetRecipeElement(brokenGuid) != null) brokenGuid = Guid.NewGuid();
            json = json.Replace($"\\\"selectedRecipeGuid\\\":\\\"{recipe.MachineRecipeGuid}\\\"", $"\\\"selectedRecipeGuid\\\":\\\"{brokenGuid}\\\"");

            var (loadWorldBlockDatastore, _, loadJsonFile) = CreateBlockTestModule();
            Assert.DoesNotThrow(() => loadJsonFile.Load(json));

            var loadedBlock = loadWorldBlockDatastore.GetBlock(Vector3Int.zero);
            Assert.IsTrue(loadedBlock.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var loadedSelector));
            Assert.AreEqual(Guid.Empty, loadedSelector.SelectedRecipeGuid);
        }

        private (IWorldBlockDatastore, AssembleSaveJsonText, WorldLoaderFromJson) CreateBlockTestModule()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var loadJsonFile = serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;

            return (worldBlockDatastore, assembleSaveJsonText, loadJsonFile);
        }
    }
}
