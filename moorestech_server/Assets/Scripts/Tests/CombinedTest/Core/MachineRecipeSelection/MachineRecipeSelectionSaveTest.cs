using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.MachineRecipe;
using UnityEngine;

namespace Tests.CombinedTest.Core.MachineRecipeSelection
{
    public class MachineRecipeSelectionSaveTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void Idleの選択GUIDを一つだけ保存して復元する()
        {
            var block = PlaceMachine();
            MachineRecipeSelectionTestUtil.SelectRecipe(block, ForUnitTestMachineRecipeId.MachineIdRecipe);

            var saveState = block.GetSaveState();
            var machineJson = JObject.Parse(saveState[typeof(VanillaMachineSaveComponent).FullName]);
            var recipeGuidProperties = machineJson.Descendants().OfType<JProperty>().Where(property => property.Name == "recipeGuid").ToList();
            var loadedBlock = LoadBlock(block, saveState, 701);

            Assert.AreEqual(1, recipeGuidProperties.Count);
            Assert.AreEqual(ForUnitTestMachineRecipeId.MachineIdRecipe,
                loadedBlock.GetComponent<VanillaMachineProcessorComponent>().RecipeGuid);
            Assert.AreEqual(ProcessState.Idle, loadedBlock.GetComponent<VanillaMachineProcessorComponent>().CurrentState);
        }

        [Test]
        public void Processing復元後も消費アイテムを返却できる()
        {
            var block = PlaceMachine();
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.MachineIdRecipe);
            var inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            MachineRecipeSelectionTestUtil.SelectRecipe(block, recipe.MachineRecipeGuid);
            foreach (var input in recipe.InputItems) inventory.InsertItem(ServerContext.ItemStackFactory.Create(input.ItemGuid, input.Count));
            GameUpdater.UpdateOneTick();

            // 復元後の返却用に消費後の入力を埋める
            // Fill consumed input to require a refund after restore
            var inputInventory = MachineRecipeSelectionTestUtil.GetInputInventory(block);
            inputInventory.InsertItem(ServerContext.ItemStackFactory.Create(new Guid("00000000-0000-0000-1234-000000000003"), 1));
            inputInventory.InsertItem(ServerContext.ItemStackFactory.Create(new Guid("00000000-0000-0000-1234-000000000004"), 1));
            var loadedBlock = LoadBlock(block, block.GetSaveState(), 702);
            var loadedProcessor = loadedBlock.GetComponent<VanillaMachineProcessorComponent>();
            var playerInventory = MachineRecipeSelectionTestUtil.CreatePlayerInventory(2);

            var result = loadedProcessor.TrySetRecipe(ForUnitTestMachineRecipeId.AlternativeMachineIdRecipe, playerInventory);

            Assert.AreEqual(MachineRecipeChangeResult.Success, result);
            Assert.AreEqual(4, playerInventory.InventoryItems.Sum(item => item.Count));
            Assert.AreEqual(ProcessState.Idle, loadedProcessor.CurrentState);
        }

        private static IBlock PlaceMachine()
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.one,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            return block;
        }

        private static IBlock LoadBlock(IBlock source, System.Collections.Generic.Dictionary<string, string> saveState, int instanceId)
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid;
            return ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(instanceId), saveState, source.BlockPositionInfo);
        }
    }
}
