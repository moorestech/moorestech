using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Config;
using industrialization.Config.Recipe;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class MachineInventory : IInstallationInventory
    {
        private MachineRunProcess _machineRunProcess;
        private IInstallationInventory _connectInventory;
        private List<IItemStack> _inputSlot;
        public List<IItemStack> InputSlot
        {
            get
            {
                var a = _inputSlot.Where(i => i.Id != NullItemStack.NullItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }

        private List<IItemStack> _outpuutSlot;
        public List<IItemStack> OutpuutSlot
        {
            get
            {
                var a = _outpuutSlot.Where(i => i.Id != NullItemStack.NullItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }
        private int installationId;

        //TODO インプット、アウトプットスロットを取得し実装
        public MachineInventory(int installationId, IInstallationInventory connectInventory)
        {
            this.installationId = installationId;
            _connectInventory = connectInventory;
            _inputSlot = CreateEmptyItemStacksList.Create(100);
            _outpuutSlot = CreateEmptyItemStacksList.Create(100);
        }
        
        /// <summary>
        /// アイテムが入る枠があるかどうか検索し、入るなら入れて返す、入らないならそのままもらったものを返す
        /// </summary>
        /// <param name="itemStack">入れたいアイテム</param>
        /// <returns>余り、枠が無かった等入れようとした結果余ったアイテム</returns>
        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (int i = 0; i < _inputSlot.Count; i++)
            {
                if (_inputSlot[i].IsAllowedToAdd(itemStack))
                {
                    var r = _inputSlot[i].AddItem(itemStack);
                    _inputSlot[i] = r.MineItemStack;
                    StartProcess();
                    return r.ReceiveItemStack;
                }
            }
            return itemStack;
        }

        void StartProcess()
        {
            //プロセスをスタートできるか判定
            if (_machineRunProcess != null && _machineRunProcess.IsProcessing()) return;
            
            var recipe = MachineRecipeConfig.GetRecipeData(installationId, InputSlot.ToList());
            var tmp = recipe.RecipeConfirmation(InputSlot);
            if(!tmp) return;
            
            
            //TODO アウトプットスロットに空きがあるかチェック

            //スタートできるなら加工をスタートし、アイテムを減らす
            _machineRunProcess = new MachineRunProcess(OutputEvent,recipe);
            foreach (var item in recipe.ItemInputs)
            {
                for (int i = 0; i < _inputSlot.Count; i++)
                {
                    if (_inputSlot[i].Id == item.Id &&  item.Amount <=_inputSlot[i].Amount)
                    {
                        _inputSlot[i] = _inputSlot[i].SubItem(item.Amount);
                        break;
                    }
                }
            }
        }

        void OutputEvent(ItemStack[] output)
        {
            //アウトプットスロットに受け取ったアイテムを入れる
            foreach (var outputItem in output)
            {
                for (var i = 0; i < _outpuutSlot.Count; i++)
                {
                    if (!_outpuutSlot[i].IsAllowedToAdd(outputItem)) continue;
                    //アイテムを出力スロットに加算
                    _outpuutSlot[i] = _outpuutSlot[i].AddItem(outputItem).MineItemStack;
                    //繋がってるインベントリに出力
                    _outpuutSlot[i] = _connectInventory.InsertItem(_outpuutSlot[i]);
                    break;
                }
            }
            StartProcess();
        }
    }
}