using System;
using System.Threading;
using industrialization.Config;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    public class MachineRunProcess
    {
        public delegate void Output(ItemStack item);
        private event Output OutputEvent;
        
        //TODO プロセス実行のロジック実装
        public MachineRunProcess(Output outputEvent)
        {
            OutputEvent += outputEvent;
        }
    }
}