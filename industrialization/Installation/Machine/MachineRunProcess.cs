using System;
using System.Threading;
using industrialization.Config;
using industrialization.Config.Recipe.Data;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class MachineRunProcess
    {
        public delegate void Output(ItemStack item);
        private event Output OutputEvent;
        private long endtime;
        
        //TODO プロセス実行のロジック実装
        public MachineRunProcess(Output outputEvent,IMachineRecipeData RecipeData)
        {
            OutputEvent += outputEvent;
            var a = UnixTime.GetNowUnixTime();
        }

        public bool IsProcessing()
        {
            return false;
        }
    }
}