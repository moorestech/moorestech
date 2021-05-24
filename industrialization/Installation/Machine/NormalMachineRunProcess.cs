using System;
using industrialization.Config.Recipe.Data;
using industrialization.GameSystem;

namespace industrialization.Installation.Machine
{
    public class NormalMachineRunProcess : IUpdate
    {
        private IMachineRecipeData _machineRecipeData;
        public readonly NormalMachineOutputInventory NormalMachineOutputInventory;
        private DateTime _processEndTime;
        public NormalMachineRunProcess(NormalMachineOutputInventory normalMachineOutputInventory)
        {
            _processEndTime = DateTime.MaxValue;
            _machineRecipeData = new NullMachineRecipeData();
            NormalMachineOutputInventory = normalMachineOutputInventory;
            GameUpdate.AddUpdateObject(this);
        }
        
        /// <summary>
        /// 実行中かどうか、アウトプットスロットがいっぱいじゃないかを見る
        /// </summary>
        /// <returns></returns>
        public bool IsAllowedToStartProcess()
        {
            return !IsProcessing && NormalMachineOutputInventory.IsAllowedToOutputItem(_machineRecipeData);
        }

        /// <summary>
        /// 実際にプロセスを開始する
        /// </summary>
        /// <param name="machineRecipeData"></param>
        public void StartProcess(IMachineRecipeData machineRecipeData)
        {
            _machineRecipeData = machineRecipeData;
            _processEndTime = DateTime.Now.AddMilliseconds(machineRecipeData.Time);
        }

        /// <summary>
        /// TODO アップデートをして実行できるか見る
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Update()
        {
            if (!IsProcessing) return;
            _processEndTime = DateTime.MaxValue;
            NormalMachineOutputInventory.InsertOutputSlot(_machineRecipeData);
        }

        private bool IsProcessing => _processEndTime < DateTime.Now;
    }
}