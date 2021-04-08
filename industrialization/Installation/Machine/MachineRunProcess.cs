using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using industrialization.Config;
using industrialization.Config.Recipe.Data;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class MachineRunProcess
    {
        public delegate void Output(ItemStack[] item);
        private event Output OutputEvent;
        private readonly long endtime;
        
        //TODO プロセス実行のロジック実装
        public MachineRunProcess(Output outputEvent,IMachineRecipeData recipeData)
        {
            OutputEvent += outputEvent;
            endtime = UnixTime.GetNowUnixTime() + recipeData.Time;
            Task.Run(() => {
                WaitProcessEnd(recipeData.Time,recipeData.ItemOutputs);
            }); 
        }

        private void WaitProcessEnd(int time,ItemOutput[] outputs)
        {
            Thread.Sleep(time);
            var outputItem = new List<ItemStack>();
            foreach (var output in outputs)
            {
                if (!ProbabilityCalculator.DetectFromPercent(output.Percent)) continue;
                outputItem.Add(new ItemStack(output.OutputItem.Id,output.OutputItem.Amount));
            }
            OutputEvent(outputItem.ToArray());
        }

        //終了時間よりも現在時間のほうが大きかったらプロセス終了
        public bool IsProcessing()
        {
            return UnixTime.GetNowUnixTime() < endtime;
        }
    }
}