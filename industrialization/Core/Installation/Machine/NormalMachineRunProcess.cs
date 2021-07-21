using System;
using industrialization.Core.Config.Recipe.Data;
using industrialization.Core.Electric;
using industrialization.Core.GameSystem;

namespace industrialization.Core.Installation.Machine
{
    public class NormalMachineRunProcess : IUpdate,IInstallationElectric
    {
        //TODO コンフィグに必要電力量を追加
        private const int requestPower = 100;
        
        private IMachineRecipeData _machineRecipeData;
        public readonly NormalMachineOutputInventory NormalMachineOutputInventory;
        private DateTime _processStartTime;
        private int _nowPower = 0;
        public NormalMachineRunProcess(NormalMachineOutputInventory normalMachineOutputInventory)
        {
            _processStartTime = DateTime.MaxValue;
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
            return !IsWaiting && NormalMachineOutputInventory.IsAllowedToOutputItem(_machineRecipeData);
        }

        /// <summary>
        /// 実際にプロセスを開始する
        /// </summary>
        /// <param name="machineRecipeData"></param>
        public void StartProcess(IMachineRecipeData machineRecipeData)
        {
            _machineRecipeData = machineRecipeData;
            _processStartTime = DateTime.Now;
        }

        /// <summary>
        /// アップデートをして実行できるか見る
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Update()
        {
            if (!IsWaiting) return;
            if (IsAllowedToStartProcess())
            {
                StartProcess(_machineRecipeData);
            }
            _processStartTime = DateTime.MaxValue;
            NormalMachineOutputInventory.InsertOutputSlot(_machineRecipeData);
            _nowPower = 0;
        }


        //電力量が需要量より少なかったらプロセス終了までの時間を遅くする
        private bool IsWaiting
        {
            get
            {
                //電力が0の時は現在時間を更新し続ける
                if (_nowPower <= 0)
                {
                    _processStartTime = DateTime.Now;
                }
                try
                {
                    int tmp = _machineRecipeData.Time / (_nowPower / requestPower);
                    return _processStartTime.AddMilliseconds(tmp) < DateTime.Now;
                }
                catch (Exception e)
                {
                    return false;
                }
            }
        }
        
        public int RequestPower()
        {
            return requestPower;
        }

        public void SupplyPower(int power)
        {
            _nowPower = power;
        }
    }
}