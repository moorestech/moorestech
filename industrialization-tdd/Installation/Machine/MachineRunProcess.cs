using System;
using industrialization.GameSystem;

namespace industrialization.Installation.Machine
{
    public class MachineRunProcess : IMachine
    {
        private MachineInventory inventory;

        public MachineRunProcess(MachineInventory inventory)
        {
            this.inventory = inventory;
            GameUpdate.UpdateEvent += Update;
        }

        public MachineState GetState()
        {
            throw new System.NotImplementedException();
        }

        public void SupplyPower(double power)
        {
            throw new System.NotImplementedException();
        }

        private void Update()
        {
            int ans = 0;
            for (int i = 0; i < 100000; i++)
            {
                ans += i;
            }
            Console.WriteLine(ans);
        }
    }
}