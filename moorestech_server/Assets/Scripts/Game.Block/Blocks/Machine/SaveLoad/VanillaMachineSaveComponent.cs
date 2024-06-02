using System.Text;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.Machine.SaveLoad
{
    public class VanillaMachineSaveComponent : IBlockSaveState
    {
        public bool IsDestroy { get; private set; }
        
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;
        
        public VanillaMachineSaveComponent(
            VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory,
            VanillaMachineProcessorComponent vanillaMachineProcessorComponent)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
        }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public string GetSaveState()
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            //フォーマット
            //inputSlot,item1 id,item1 count,item2 id,item2 count,outputSlot,item1 id,item1 count,item2 id,item2 count,state,0 or 1,remainingTime,500
            var saveState = new StringBuilder("inputSlot,");
            //インプットスロットを保存
            foreach (var item in _vanillaMachineInputInventory.InputSlot)
                saveState.Append(item.Id + "," + item.Count + ",");
            
            saveState.Append("outputSlot,");
            //アウトプットスロットを保存
            foreach (var item in _vanillaMachineOutputInventory.OutputSlot)
                saveState.Append(item.Id + "," + item.Count + ",");
            
            //状態を保存
            saveState.Append("state," + (int)_vanillaMachineProcessorComponent.CurrentState + ",");
            //現在の残り時間を保存
            saveState.Append("remainingTime," + _vanillaMachineProcessorComponent.RemainingMillSecond + ",");
            //レシピIDを保存
            saveState.Append("recipeId," + _vanillaMachineProcessorComponent.RecipeDataId);
            
            return saveState.ToString();
        }
    }
}