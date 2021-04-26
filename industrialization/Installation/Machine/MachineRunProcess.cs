using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using industrialization.Config;
using industrialization.Config.Recipe.Data;
using industrialization.GameSystem;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class MachineRunProcess : IUpdate
    {
        public delegate void Output(ItemStack[] item);
        private event Output OutputEvent;
        private readonly long endtime;
        private bool isFinish = false;
        private IMachineRecipeData recipeData;
        
        public MachineRunProcess(Output outputEvent,IMachineRecipeData recipeData)
        {
            OutputEvent += outputEvent;
            endtime = UnixTime.GetNowUnixTime() + recipeData.Time;
            this.recipeData = recipeData;
            GameUpdate.AddUpdate(this);
        }

        //終了時間よりも現在時間のほうが大きかったらプロセス終了
        public bool IsProcessing()
        {
            return UnixTime.GetNowUnixTime() < endtime;
        }

        public void Update()
        {
            if (!IsProcessing() && !isFinish)
            {
                isFinish = true;
                var outputItem = new List<ItemStack>();
                foreach (var output in recipeData.ItemOutputs)
                {
                    if (!ProbabilityCalculator.DetectFromPercent(output.Percent)) continue;
                    outputItem.Add(new ItemStack(output.OutputItem.Id,output.OutputItem.Amount));
                }
                OutputEvent(outputItem.ToArray());
            }
        }
    }
}