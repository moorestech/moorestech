using industrialization.Config.Recipe.Data;
using industrialization.GameSystem;

namespace industrialization.Installation.Machine
{
    public class NormalMachineRunProcess : IUpdate
    {
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;
        public NormalMachineRunProcess(NormalMachineOutputInventory normalMachineOutputInventory)
        {
            _normalMachineOutputInventory = normalMachineOutputInventory;
            GameUpdate.AddUpdate(this);
        }
        
        /// <summary>
        /// TODO 実行中かどうか、アウトプットスロットがいっぱいじゃないかを見る
        /// </summary>
        /// <returns></returns>
        public bool IsAllowedToStartProcess()
        {
            return _normalMachineOutputInventory.IsAllowedToOutputItem();
        }

        /// <summary>
        /// TODO 実際にプロセスを始めるための待機
        /// </summary>
        /// <param name="machineRecipeData"></param>
        public void StartProcess(IMachineRecipeData machineRecipeData)
        {
            
        }

        /// <summary>
        /// TODO アップデートをして実行できるか見る
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Update()
        {
            throw new System.NotImplementedException();
        }
    }
}