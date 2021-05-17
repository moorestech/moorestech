using System;
using industrialization.Config.Recipe.Data;
using industrialization.GameSystem;

namespace industrialization.Installation.Machine
{
    public class NormalMachineRunProcess : IUpdate
    {
        private IMachineRecipeData _machineRecipeData;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;
        private DateTime _processEndTime;
        public NormalMachineRunProcess(NormalMachineOutputInventory normalMachineOutputInventory)
        {
            _machineRecipeData = new NullMachineRecipeData();
            _normalMachineOutputInventory = normalMachineOutputInventory;
            GameUpdate.AddUpdate(this);
        }
        
        /// <summary>
        /// 実行中かどうか、アウトプットスロットがいっぱいじゃないかを見る
        /// </summary>
        /// <returns></returns>
        public bool IsAllowedToStartProcess()
        {
            return !IsProcessing && _normalMachineOutputInventory.IsAllowedToOutputItem(_machineRecipeData);
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
            _processEndTime = DateTime.MaxValue;;
            _normalMachineOutputInventory.InsertOutputSlot(_machineRecipeData);
            throw new System.NotImplementedException();
        }

        private bool IsProcessing => DateTime.Now < _processEndTime;
    }
}