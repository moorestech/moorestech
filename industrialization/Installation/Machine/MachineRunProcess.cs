using System;
using System.Collections.Generic;
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
        private readonly DateTime endtime;
        private bool isFinish = false;
        private IMachineRecipeData recipeData;
        
        public MachineRunProcess(Output outputEvent,IMachineRecipeData recipeData)
        {
            OutputEvent += outputEvent;
            endtime = DateTime.Now.AddMilliseconds(recipeData.Time);
            this.recipeData = recipeData;
            GameUpdate.AddUpdate(this);
        }

        //終了時間よりも現在時間のほうが大きかったらプロセス終了
        public bool IsProcessing()
        {
            return DateTime.Now < endtime;
        }

        public void Update()
        {
            if (IsProcessing() || isFinish) return;
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