using System;
using System.Threading;
using industrialization.Installation;
using industrialization.Installation.Machine;
using industrialization.Item;
using industrialization.Test.Generate;
using NUnit.Framework;

namespace industrialization.Test
{
    public class MachineIOTest
    {
        [Test]
        public void ItemProcessingTest()
        {
            int seed = 2119350917;
            int recipeNum = 200;
            
            var r = RecipeGenerate.MakeRecipe(seed,recipeNum);
            foreach (var m in MachineIOGenerate.MachineIOTestCase(r, seed))
            {
                var conecct = new DummyInstallationInventory();
                var machine = new MachineInstallation(m.installtionId,Guid.Empty, conecct);

                foreach (var minput in m.input)
                {
                    machine.MachineInventory.InsertItem(new ItemStack(minput.Id,minput.Amount));
                }
                Thread.Sleep((int)(m.time * 1.2f));
                var reminder = machine.MachineInventory.InputSlot;
                var output = machine.MachineInventory.OutpuutSlot;
            }
        }
    }
}