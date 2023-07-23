using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using MainGame.UnityView.UI.Builder.BluePrint;
using MainGame.UnityView.UI.Builder.Element;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
using UnityEngine;

namespace MainGame.UnityView.UI.UIState.UIObject
{
    /// <summary>
    /// ブロックのインベントリを動的に構築するシステム
    /// </summary>
    public class BlockInventoryObjectCreator : MonoBehaviour
    {
        [SerializeField] private PlayerInventorySlots playerInventorySlots;

        public void SetOneSlotInventory(string blockName,int slot,Vector2Int blockPos)
        {
            var arraySlot = new List<UIBluePrintItemSlotArray>();
            arraySlot.Add(CreateArraySlot(0,272,10,slot,PlayerInventoryConst.MainInventoryColumns));
            
            var text = new List<UIBluePrintText>(){new(0,436,0,blockName,30)};
            
            var blockInventory = new SubInventoryViewBluePrint(){ArraySlots = arraySlot,TextElements = text};
            
            playerInventorySlots.SetSubSlots(blockInventory,new SubInventoryOptions(){IsBlock = true,BlockPosition = blockPos});
        }
        
        public void SetIOSlotInventory(string blockName,int input,int output,Vector2Int blockPos)
        {
            const int maxSlotColumns = 5;
            var arraySlot = new List<UIBluePrintItemSlotArray>();
            arraySlot.Add(CreateArraySlot(-330,272,10,input,maxSlotColumns));
            arraySlot.Add(CreateArraySlot(330,272,10,output,maxSlotColumns));
            
            var text = new List<UIBluePrintText>(){new(0,436,0,blockName,30)};
            
            var blockInventory = new SubInventoryViewBluePrint(){ArraySlots = arraySlot,TextElements = text};
            
            playerInventorySlots.SetSubSlots(blockInventory,new SubInventoryOptions(){IsBlock = true,BlockPosition = blockPos});
        }


        private UIBluePrintItemSlotArray CreateArraySlot(int x,int y,int priority,int slot,int maxSlotColumns)
        {
            if (slot < maxSlotColumns)
            {
                return new UIBluePrintItemSlotArray(x, y, priority, 1, slot);
            }

            var height = 1 + slot / maxSlotColumns;
            var width = maxSlotColumns;
            var blank = maxSlotColumns - slot % maxSlotColumns;
                
            return new UIBluePrintItemSlotArray(x, y, priority, height, maxSlotColumns,blank);

        }
        
        
    }
}