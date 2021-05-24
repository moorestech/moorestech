using System;
using industrialization.Config.Recipe.Data;
using industrialization.Electric;
using industrialization.GameSystem;

namespace industrialization.Installation.Machine
{
    public class NormalMachineRunProcess : IUpdate,IInstallationElectric
    {
        //TODO コンフィグに必要電力量を追加
        private const int requestPower = 100;
        
        private IMachineRecipeData _machineRecipeData;
        public readonly NormalMachineOutputInventory NormalMachineOutputInventory;
        private DateTime _processEndTime;
        private int nowPower = 0;
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
            if (!IsProcessing)
            {
                nowPower = 0;
                return;
            }
            _processEndTime = DateTime.MaxValue;
            NormalMachineOutputInventory.InsertOutputSlot(_machineRecipeData);
            
            nowPower = 0;
        }

        //TODO ここの実装を考える
        //電力量が需要量より少なかったらプロセス終了までの時間を遅くする
        //処理時間/(供給量/必要量) - 処理時間 でどれくらい延長すべきかを求める
        private bool IsProcessing
        {
            get
            {
                //終了時刻が最大でなければとりあえず何かしら作業はしている
                if (_processEndTime != DateTime.MaxValue)
                {
                    //作業中でも電力が0なら作業は進まない
                    if (nowPower <= 0)
                    {
                        return true;
                    }
                    else
                    {
                        int offset = _machineRecipeData.Time / (nowPower / requestPower)-_machineRecipeData.Time;
                        return _processEndTime.AddMilliseconds(offset) < DateTime.Now;
                    }
                }
            }
        }

        public int RequestPower()
        {
            return requestPower;
        }

        public void SupplyPower(double power)
        {
            nowPower = (int)power;
        }
    }
}