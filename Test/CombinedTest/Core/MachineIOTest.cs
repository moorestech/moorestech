using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Block.BlockFactory;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Machine;
using Core.Block.Blocks.Machine.Inventory;
using Core.Block.Blocks.Machine.InventoryController;
using Core.Block.RecipeConfig;
using Core.Electric;
using Core.Item;
using Core.Item.Config;
using Core.Update;
using Core.Util;
using NUnit.Framework;
using Test.CombinedTest.Core.Generate;
using Test.Module;
using Test.Module.TestConfig;

namespace Test.CombinedTest.Core
{
    public class MachineIoTest
    {
        private readonly ItemStackFactory _itemStackFactory = new(new TestItemConfig());
        private BlockFactory _blockFactory;
        private VanillaMachine CreateMachine(int id)
        {
            if (_blockFactory == null)
            {
                _blockFactory = new BlockFactory(new AllMachineBlockConfig(),new VanillaIBlockTemplates(new TestMachineRecipeConfig(_itemStackFactory),_itemStackFactory,null,null));
            }
            var machine = _blockFactory.Create(id, IntId.NewIntId()) as VanillaMachine;
            return machine;
        }
        private VanillaMachine CreateMachine(int id,IBlockInventory inventory)
        {
            var machine = CreateMachine(id);
            machine.AddOutputConnector(inventory);
            
            return machine;
        }
        
        [TestCase(new int[1]{1}, new int[1]{1},new int[1]{1}, new int[1]{1})]
        [TestCase(new int[2]{100,101}, new int[2]{10,10},new int[2]{100,101}, new int[2]{10,10})]
        [TestCase(new int[3]{10,11,15}, new int[3]{10,5,8},new int[3]{10,11,15}, new int[3]{10,5,8})]
        [TestCase(new int[2]{1,1}, new int[2]{1,1}, new int[1]{1}, new int[1]{2})]
        [TestCase(new int[2]{3,1}, new int[2]{1,1}, new int[2]{3,1}, new int[2]{1,1})]
        [TestCase( new int[3] {0, 5, 1}, new int[3] {10, 5, 8},new int[2]{5,1}, new int[2] { 5, 8})]
        [TestCase( new int[2] {1, 0}, new int[2] {10, 5},new int[1]{1}, new int[1] {10})]
        public void MachineAddInputTest(int[] id,int[] count,int[] ansid,int[] anscount)
        {
            
            var machine = CreateMachine(4);
            
            for (int i = 0; i < id.Length; i++)
            {
                machine.InsertItem(_itemStackFactory.Create(id[i], count[i]));
            }

            var ansItem = new List<IItemStack>();
            for (int i = 0; i < ansid.Length; i++)
            {
                ansItem.Add(_itemStackFactory.Create(ansid[i],anscount[i]));
            }

            
            var _vanillaMachineInventory = (VanillaMachineInventory)typeof(VanillaMachine).GetField("_vanillaMachineInventory",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(machine);
            var _vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineInventory)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_vanillaMachineInventory);

            for (int i = 0; i < ansItem.Count; i++)
            {
                Assert.AreEqual(ansItem[i], _vanillaMachineInputInventory.InputSlot[i]);  
            }
            
        }
        
        //アイテムが通常通り処理されるかのテスト
        [Test]
        public void ItemProcessingAndChangeConnectorTest()
        {
            int seed = 2119350917;
            int recipeNum = 20;
            var recipes = MachineIoGenerate.MachineIoTestCase(RecipeGenerate.MakeRecipe(seed,recipeNum), seed);
            
            
            var machineList = new List<VanillaMachine>();
            var MaxDateTime = DateTime.Now;
            
            //機械の作成とアイテムの挿入
            foreach (var m in recipes)
            {
                var machine = CreateMachine(m.installtionId);
                

                foreach (var minput in m.input)
                {
                    machine.InsertItem(_itemStackFactory.Create(minput.Id,minput.Count));
                }

                var electrical = new ElectricSegment();
                electrical.AddBlockElectric(machine);
                electrical.AddGenerator(new TestPowerGenerator(1000,0));
                
                machineList.Add(machine);
                
                DateTime endTime = DateTime.Now.AddMilliseconds(m.time*m.CraftCnt);
                if (endTime.CompareTo(MaxDateTime) == 1)
                {
                    MaxDateTime = endTime;
                }
            }
            
            //最大クラフト時間を超過するまでクラフトする
            while (MaxDateTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                GameUpdate.Update();
            }
            
            //検証その1
            for (int i = 0; i < machineList.Count; i++)
            {
                Console.WriteLine(i);
                var machine = machineList[i];
                var machineIoTest = recipes[i];

                var (inputSlot, outputSlot) = GetInputOutputSlot(machine);
                
                Assert.True(0 < outputSlot.Count);

                for (int j = 0; j < outputSlot.Count; j++)
                {
                    Assert.AreEqual(machineIoTest.output[j],outputSlot[j]);
                }
                
                var inputRemainder = machineIoTest.inputRemainder.Where(i => i.Count != 0).ToList();
                inputRemainder.Sort((a, b) => a.Id - b.Id);
                for (int j = 0; j < inputSlot.Count; j++)
                {
                    Assert.True(inputRemainder[j].Equals(inputSlot[j]));
                }
            }
            
            var dummyBlockList = new List<DummyBlockInventory>();
            //コネクターを変える
            for (int i = 0; i < recipes.Length; i++)
            {
                var dummy = new DummyBlockInventory(recipes[i].output.Count);
                machineList[i].AddOutputConnector(dummy);
                dummyBlockList.Add(dummy);
            }
            GameUpdate.Update();
            //検証その2
            for (int i = 0; i < machineList.Count; i++)
            {
                Console.WriteLine(i);
                var machine = machineList[i];
                var dummy = dummyBlockList[i];
                var machineIoTest = recipes[i];
                
                var (inputSlot, outputSlot) = GetInputOutputSlot(machine);
                
                Assert.True(outputSlot.Count == 0);

                for (int j = 0; j < machineIoTest.output.Count; j++)
                {
                    Assert.True(machineIoTest.output[j].Equals(dummy.InsertedItems[j]));
                }
                
                var inputRemainder = machineIoTest.inputRemainder.Where(i => i.Count != 0).ToList();
                inputRemainder.Sort((a, b) => a.Id - b.Id);
                for (int j = 0; j < inputRemainder.Count; j++)
                {
                    Assert.True(inputRemainder[j].Equals(inputSlot[j]));
                }
            }
        }
        
        //アイテムが通常通り処理されるかのテスト
        [Test]
        public void ItemProcessingOutputTest()
        {
            int seed = 2119350917;
            int recipeNum = 20;
            var recipes = MachineIoGenerate.MachineIoTestCase(RecipeGenerate.MakeRecipe(seed,recipeNum), seed);
            
            
            var machineList = new List<VanillaMachine>();
            var dummyBlockList = new List<DummyBlockInventory>();
            var MaxDateTime = DateTime.Now;
            
            //機械の作成とアイテムの挿入
            foreach (var m in recipes)
            {
                var connect = new DummyBlockInventory(m.output.Count);
                var machine = CreateMachine(m.installtionId,connect);

                foreach (var minput in m.input)
                {
                    machine.InsertItem(_itemStackFactory.Create(minput.Id,minput.Count));
                }

                var electrical = new ElectricSegment();
                electrical.AddBlockElectric(machine);
                electrical.AddGenerator(new TestPowerGenerator(1000,0));
                
                dummyBlockList.Add(connect);
                machineList.Add(machine);
                
                DateTime endTime = DateTime.Now.AddMilliseconds(m.time*m.CraftCnt);
                if (endTime.CompareTo(MaxDateTime) == 1)
                {
                    MaxDateTime = endTime;
                }
            }
            
            //最大クラフト時間を超過するまでクラフトする
            while (MaxDateTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                GameUpdate.Update();
            }
            
            //検証
            for (int i = 0; i < machineList.Count; i++)
            {
                Console.WriteLine(i);
                var machine = machineList[i];
                var connect = dummyBlockList[i];
                var machineIoTest = recipes[i];
                
                var output = connect.InsertedItems;
                Assert.False(output.Count <= 0);

                for (int j = 0; j < output.Count; j++)
                {
                    Assert.True(machineIoTest.output[j].Equals(output[j]));
                }
                
                
                var (inputSlot, outputSlot) = GetInputOutputSlot(machine);
                
                var inputRemainder = machineIoTest.inputRemainder.Where(i => i.Count != 0).ToList();
                inputRemainder.Sort((a, b) => a.Id - b.Id);
                for (int j = 0; j < inputSlot.Count; j++)
                {
                    Assert.True(inputRemainder[j].Equals(inputSlot[j]));
                }
            }
        }
        
        public (List<IItemStack> , List<IItemStack>) GetInputOutputSlot(VanillaMachine machine)
        {
            var _vanillaMachineInventory = (VanillaMachineInventory)typeof(VanillaMachine).GetField("_vanillaMachineInventory",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(machine);
            var _vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineInventory)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_vanillaMachineInventory);
            var _vanillaMachineOutputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineInventory)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_vanillaMachineInventory);

            var inputSlot = _vanillaMachineInputInventory.InputSlot.Where(i => i.Count != 0).ToList();
            inputSlot.Sort((a, b) => a.Id - b.Id);
                
            var outputSlot = _vanillaMachineOutputInventory.OutputSlot.Where(i => i.Count != 0).ToList();
            outputSlot.Sort((a, b) => a.Id - b.Id);
            
            return (inputSlot,outputSlot);
        }
    }
}