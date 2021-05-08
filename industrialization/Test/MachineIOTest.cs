using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.GameSystem;
using industrialization.Installation;
using industrialization.Installation.Machine;
using industrialization.Item;
using industrialization.Test.Generate;
using NUnit.Framework;

namespace industrialization.Test
{
    public class MachineIOTest
    {
        [TestCase(true,new int[1]{1}, new int[1]{1})]
        [TestCase(true,new int[2]{100,101}, new int[2]{10,10})]
        [TestCase(true,new int[3]{10,11,15}, new int[3]{10,5,8})]
        [TestCase(false,new int[3]{0,5,1}, new int[3]{10,5,8})]
        [TestCase(false,new int[2]{1,0}, new int[2]{10,5})]
        [TestCase(false,new int[2]{0,0}, new int[2]{10,5})]
        public void MachineInputTest(bool isEquals,int[] id,int[] amount)
        {
            var machine = new NormalMachine(0,Guid.Empty,new DummyInstallationInventory());
            var items = new List<IItemStack>();
            for (int i = 0; i < id.Length; i++)
            {
                items.Add(ItemStackFactory.NewItemStack(id[i], amount[i]));
            }

            foreach (var item in items)
            {
                machine.MachineInventory.InsertItem(item);
            }

            if (isEquals)
            {
                Assert.AreEqual(items.ToArray(), machine.MachineInventory.InputSlot.ToArray());   
            }
            else
            {
                Assert.AreNotEqual(items.ToArray(), machine.MachineInventory.InputSlot.ToArray());
            }
            
        }
        [TestCase(new int[2]{1,1}, new int[2]{1,1}, new int[1]{1}, new int[1]{2})]
        [TestCase(new int[2]{3,1}, new int[2]{1,1}, new int[2]{1,3}, new int[2]{1,1})]
        [TestCase(new int[6]{1,3,1,5,5,0}, new int[6]{1,1,2,6,2,4}, new int[4]{0,1,3,5}, new int[4]{4,3,1,8})]
        public void MachineAddInputTest(int[] id,int[] amount,int[] ansid,int[] ansamount)
        {
            var machine = new NormalMachine(0,Guid.Empty,new DummyInstallationInventory());
            for (int i = 0; i < id.Length; i++)
            {
                machine.MachineInventory.InsertItem(ItemStackFactory.NewItemStack(id[i], amount[i]));
            }

            var ansItem = new List<IItemStack>();
            for (int i = 0; i < ansid.Length; i++)
            {
                ansItem.Add(new ItemStack(ansid[i],ansamount[i]));
            }

            for (int i = 0; i < ansItem.Count; i++)
            {
                var m = machine.MachineInventory.InputSlot;
                Assert.True(ansItem[i].Equals(m[i]));
            }
            
        }
        
        
        //アイテムが通常通り処理されるかのテスト
        [Test]
        public void ItemProcessingTest()
        {
            int seed = 2119350917;
            int recipeNum = 20;
            
            var r = RecipeGenerate.MakeRecipe(seed,recipeNum);
            foreach (var m in MachineIOGenerate.MachineIOTestCase(r, seed))
            {
                var conecct = new DummyInstallationInventory(m.output.Length);
                var machine = new NormalMachine(m.installtionId,Guid.Empty, conecct);

                foreach (var minput in m.input)
                {
                    machine.MachineInventory.InsertItem(new ItemStack(minput.Id,minput.Amount));
                }

                while (!DummyInstallationInventory.IsFinish)
                {
                    GameUpdate.Update();
                }
                
                var remainder = machine.MachineInventory.InputSlot;
                var output = machine.MachineInventory.OutpuutSlot;


                for (int i = 0; i < output.Length; i++)
                {
                    Assert.True(m.output[i].Equals(output[i]));
                }
                for (int i = 0; i < output.Length; i++)
                {
                    Assert.True(m.inputRemainder[i].Equals(remainder[i]));
                }
            }
        }
        //処理時間を短く判定したときに、失敗するかのテスト
        [Test]
        public void ItemProcessingFaildTest()
        {
            int seed = 2119350917;
            int recipeNum = 1;
            
            var r = RecipeGenerate.MakeRecipe(seed,recipeNum);
            foreach (var m in MachineIOGenerate.MachineIOTestCase(r, seed))
            {
                var conecct = new DummyInstallationInventory(m.output.Length);
                var machine = new NormalMachine(m.installtionId,Guid.Empty, conecct);

                foreach (var minput in m.input)
                {
                   machine.MachineInventory.InsertItem(new ItemStack(minput.Id,minput.Amount));
                }
                
                while (!DummyInstallationInventory.IsFinish)
                {
                    GameUpdate.Update();
                }
                
                var remainder = machine.MachineInventory.InputSlot;
                var output = machine.MachineInventory.OutpuutSlot;


                for (int i = 0; i < output.Length; i++)
                {
                    Assert.False(m.output[i].Equals(output[i]));
                }
                for (int i = 0; i < output.Length; i++)
                {
                    Assert.False(m.inputRemainder[i].Equals(remainder[i]));
                }
            }
        }
    }
}