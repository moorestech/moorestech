#if NET6_0
using System;
using System.Reflection;
using Core.Item;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.InventoryController;
using Game.Block.Interface;
using Game.Save.Interface;
using Game.Save.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.UnitTest.Game.SaveLoad
{
    public class MachineSaveLoadTest
    {
        
        
        [Test]
        public void InventoryBlockTest()
        {
            
            var (itemStackFactory, blockFactory, worldBlockDatastore, _, assembleSaveJsonText, _) =
                CreateBlockTestModule();
            var machine = (VanillaMachineBase)blockFactory.Create(1, 10);
            worldBlockDatastore.AddBlock(machine, 0, 0, BlockDirection.North);


            
            machine.InsertItem(itemStackFactory.Create(1, 3));
            machine.InsertItem(itemStackFactory.Create(2, 1));
            
            GameUpdater.Update();
            
            machine.InsertItem(itemStackFactory.Create(5, 6));
            machine.InsertItem(itemStackFactory.Create(2, 4));

            
            
            var vanillaMachineRunProcess = (VanillaMachineRunProcess)typeof(VanillaMachineBase)
                .GetField("_vanillaMachineRunProcess", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(machine);
            typeof(VanillaMachineRunProcess)
                .GetField("_remainingMillSecond", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vanillaMachineRunProcess, 300);
            
            typeof(VanillaMachineRunProcess)
                .GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vanillaMachineRunProcess, ProcessState.Processing);

            
            var _vanillaMachineInventory = (VanillaMachineBlockInventory)typeof(VanillaMachineBase)
                .GetField("_vanillaMachineBlockInventory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(machine);

            var outputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_vanillaMachineInventory);

            outputInventory.SetItem(1, itemStackFactory.Create(1, 1));
            outputInventory.SetItem(2, itemStackFactory.Create(3, 2));

            //ID
            var recipeId = vanillaMachineRunProcess.RecipeDataId;

            var json = assembleSaveJsonText.AssembleSaveJson();
            Console.WriteLine(json);
            
            worldBlockDatastore.RemoveBlock(0, 0);


            
            var (_, _, loadWorldBlockDatastore, _, _, loadJsonFile) = CreateBlockTestModule();

            loadJsonFile.Load(json);

            var loadMachine = (VanillaMachineBase)loadWorldBlockDatastore.GetBlock(0, 0);
            Console.WriteLine(machine.GetHashCode());
            Console.WriteLine(loadMachine.GetHashCode());
            //IDintID
            Assert.AreEqual(machine.BlockId, loadMachine.BlockId);
            Assert.AreEqual(machine.EntityId, loadMachine.EntityId);


            
            var loadVanillaMachineRunProcess = (VanillaMachineRunProcess)typeof(VanillaMachineBase)
                .GetField("_vanillaMachineRunProcess", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachine);
            Assert.AreEqual((double)300, loadVanillaMachineRunProcess.RemainingMillSecond);
            //ID
            Assert.AreEqual(recipeId, loadVanillaMachineRunProcess.RecipeDataId);
            
            Assert.AreEqual(ProcessState.Processing, loadVanillaMachineRunProcess.CurrentState);


            var loadMachineInventory = (VanillaMachineBlockInventory)typeof(VanillaMachineBase)
                .GetField("_vanillaMachineBlockInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachine);
            
            var inputInventoryField = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachineInventory);
            Assert.AreEqual(itemStackFactory.Create(5, 6), inputInventoryField.InputSlot[0]);
            Assert.AreEqual(itemStackFactory.Create(2, 4), inputInventoryField.InputSlot[1]);

            
            var outputInventoryField = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachineInventory);
            Assert.AreEqual(itemStackFactory.CreatEmpty(), outputInventoryField.OutputSlot[0]);
            Assert.AreEqual(itemStackFactory.Create(1, 1), outputInventoryField.OutputSlot[1]);
            Assert.AreEqual(itemStackFactory.Create(3, 2), outputInventoryField.OutputSlot[2]);
        }

        private (ItemStackFactory, IBlockFactory, IWorldBlockDatastore, PlayerInventoryDataStore, AssembleSaveJsonText, WorldLoaderFromJson)
            CreateBlockTestModule()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var playerInventoryDataStore = serviceProvider.GetService<PlayerInventoryDataStore>();
            var loadJsonFile = serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;

            return (itemStackFactory, blockFactory, worldBlockDatastore, playerInventoryDataStore, assembleSaveJsonText, loadJsonFile);
        }
    }
}
#endif