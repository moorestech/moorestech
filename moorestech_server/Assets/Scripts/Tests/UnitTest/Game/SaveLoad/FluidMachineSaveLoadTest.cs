using System.Reflection;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Game.PlayerInventory;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Core;
using Tests.Module.TestMod;
using UnityEngine;
using System;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class FluidMachineSaveLoadTest
    {
        //液体インベントリのあるブロックを追加した時のテスト
        //レシピやブロックが変わった時はテストコードを修正してください
        [Test]
        public void FluidInventoryBlockTest()
        {
            //機械の追加
            var (blockFactory, worldBlockDatastore, _, assembleSaveJsonText, _) = CreateBlockTestModule();
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, new Vector3Int(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            var machineInventory = machineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            
            //レシピ用のアイテムを追加
            machineInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 3));
            machineInventory.InsertItem(itemStackFactory.Create(new ItemId(2), 1));
            
            //液体をインプットタンクに追加
            var inputFluidContainers = GetInputFluidContainers(machineInventory);
            var fluidId1 = MachineFluidIOTest.FluidId1;
            var fluidId2 = MachineFluidIOTest.FluidId2;
            var fluidId3 = MachineFluidIOTest.FluidId3;
            
            inputFluidContainers[0].FluidId = fluidId1;
            inputFluidContainers[0].Amount = 25.5;
            inputFluidContainers[1].FluidId = fluidId2;
            inputFluidContainers[1].Amount = 30.0;
            
            //液体をアウトプットタンクに追加
            var outputFluidContainers = GetOutputFluidContainers(machineInventory);
            outputFluidContainers[0].FluidId = fluidId3;
            outputFluidContainers[0].Amount = 15.0;
            outputFluidContainers[1].FluidId = fluidId1;
            outputFluidContainers[1].Amount = 20.0;
            
            //処理を開始
            GameUpdater.UpdateWithWait();
            //別のアイテムを追加（機械は1スロットしかないので、追加のアイテムは既存のアイテムとマージされるか無視される）
            
            // リフレクションで機械の状態を設定
            // Set machine state via reflection
            var vanillaMachineProcessor = machineBlock.GetComponent<VanillaMachineProcessorComponent>();

            // 残りtick数を設定（0.3秒 = 6tick）
            // Set remaining ticks (0.3 seconds = 6 ticks)
            typeof(VanillaMachineProcessorComponent)
                .GetProperty("RemainingTicks")
                .SetValue(vanillaMachineProcessor, 6u);
            typeof(VanillaMachineProcessorComponent)
                .GetProperty("CurrentState")
                .SetValue(vanillaMachineProcessor, ProcessState.Processing);
            
            //機械のアウトプットスロットの設定
            var outputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(machineInventory);
            
            outputInventory.SetItem(0, itemStackFactory.Create(new ItemId(1), 1));
            
            //レシピIDを取得
            var recipeId = vanillaMachineProcessor.RecipeGuid;
            
            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log(json);
            //配置したブロックを削除
            worldBlockDatastore.RemoveBlock(new Vector3Int(0, 0), BlockRemoveReason.ManualRemove);
            
            
            //ロードした時に機械の状態が正しいことを確認
            var (_, loadWorldBlockDatastore, _, _, loadJsonFile) = CreateBlockTestModule();
            
            loadJsonFile.Load(json);
            
            var loadMachineBlock = loadWorldBlockDatastore.GetBlock(new Vector3Int(0, 0));
            
            //ブロックID、intIDが同じであることを確認
            Assert.AreEqual(machineBlock.BlockId, loadMachineBlock.BlockId);
            Assert.AreEqual(machineBlock.BlockInstanceId, loadMachineBlock.BlockInstanceId);
            
            
            // 機械のレシピの残りtick数のチェック（0.3秒 = 6tick）
            // Check the remaining ticks of the machine recipe (0.3 seconds = 6 ticks)
            var machineProcessor = loadMachineBlock.GetComponent<VanillaMachineProcessorComponent>();
            Assert.AreEqual(6u, machineProcessor.RemainingTicks);
            //レシピIDのチェック
            Assert.AreEqual(recipeId, machineProcessor.RecipeGuid);
            //機械のステータスのチェック
            Assert.AreEqual(ProcessState.Processing, machineProcessor.CurrentState);
            
            
            var loadMachineInventory = loadMachineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            //インプットスロットのチェック
            var inputInventoryField = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachineInventory);
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 2), inputInventoryField.InputSlot[0]);
            
            //アウトプットスロットのチェック
            var outputInventoryField = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachineInventory);
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 1), outputInventoryField.OutputSlot[0]);
            
            //インプット液体タンクのチェック
            var loadedInputFluidContainers = inputInventoryField.FluidInputSlot;
            Assert.AreEqual(fluidId1, loadedInputFluidContainers[0].FluidId);
            Assert.AreEqual(24.5, loadedInputFluidContainers[0].Amount, 0.01);  // 25.5 - 1 = 24.5
            Assert.AreEqual(fluidId2, loadedInputFluidContainers[1].FluidId);
            Assert.AreEqual(28.0, loadedInputFluidContainers[1].Amount, 0.01);  // 30.0 - 2 = 28.0
            Assert.AreEqual(FluidMaster.EmptyFluidId, loadedInputFluidContainers[2].FluidId);
            Assert.AreEqual(0, loadedInputFluidContainers[2].Amount);
            
            //アウトプット液体タンクのチェック
            var loadedOutputFluidContainers = outputInventoryField.FluidOutputSlot;
            Assert.AreEqual(fluidId3, loadedOutputFluidContainers[0].FluidId);
            Assert.AreEqual(15.0, loadedOutputFluidContainers[0].Amount, 0.01);
            Assert.AreEqual(fluidId1, loadedOutputFluidContainers[1].FluidId);
            Assert.AreEqual(20.0, loadedOutputFluidContainers[1].Amount, 0.01);
        }
        
        private (IBlockFactory, IWorldBlockDatastore, PlayerInventoryDataStore, AssembleSaveJsonText, WorldLoaderFromJson)
            CreateBlockTestModule()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockFactory = ServerContext.BlockFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var playerInventoryDataStore = serviceProvider.GetService<PlayerInventoryDataStore>();
            var loadJsonFile = serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            
            return (blockFactory, worldBlockDatastore, playerInventoryDataStore, assembleSaveJsonText, loadJsonFile);
        }
        
        private System.Collections.Generic.IReadOnlyList<FluidContainer> GetInputFluidContainers(VanillaMachineBlockInventoryComponent blockInventory)
        {
            var vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(blockInventory);
            
            return vanillaMachineInputInventory.FluidInputSlot;
        }
        
        private System.Collections.Generic.IReadOnlyList<FluidContainer> GetOutputFluidContainers(VanillaMachineBlockInventoryComponent blockInventory)
        {
            var vanillaMachineOutputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(blockInventory);
            
            return vanillaMachineOutputInventory.FluidOutputSlot;
        }
    }
}
