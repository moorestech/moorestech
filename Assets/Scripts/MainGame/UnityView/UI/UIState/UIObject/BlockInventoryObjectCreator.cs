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
            
            var text = new List<UIBluePrintText>(){new(0,blockName,30,new Vector2(0,436),Vector3.zero, new Vector2(100,500))};
            
            var blockInventory = new SubInventoryViewBluePrint(){ArraySlots = arraySlot,TextElements = text};
            
            playerInventorySlots.SetSubSlots(blockInventory,new SubInventoryOptions(){IsBlock = true,BlockPosition = blockPos});
        }
        
        public void SetIOSlotInventory(string blockName,int input,int output,Vector2Int blockPos)
        {
            const int maxSlotColumns = 5;
            var arraySlot = new List<UIBluePrintItemSlotArray>();
            arraySlot.Add(CreateArraySlot(-330,272,10,input,maxSlotColumns));
            arraySlot.Add(CreateArraySlot(330,272,10,output,maxSlotColumns));
            
            var text = new List<UIBluePrintText>(){new(0,blockName,30,new Vector2(0,436),Vector3.zero, new Vector2(100,500))};
            var arrow = new List<UIBluePrintProgressArrow>() {new(1, "progressArrow",new Vector2(0,272),Vector3.zero, Vector2.one)};
            
            playerInventorySlots.SetSubSlots(
                new SubInventoryViewBluePrint(){ArraySlots = arraySlot,TextElements = text,ProgressArrows = arrow},
                new SubInventoryOptions(){IsBlock = true,BlockPosition = blockPos});
        }


        private UIBluePrintItemSlotArray CreateArraySlot(int x,int y,int priority,int slot,int column)
        {
            Vector2 deltaSize;
            if (slot < column)
            {
                deltaSize = UIBluePrintItemSlot.DefaultItemSlotRectSize * new Vector2(slot, 1);
                return new UIBluePrintItemSlotArray(priority, 1, slot,new Vector2(x, y), Vector3.zero, deltaSize);
            }

            var row = 1 + slot / column;
            var width = column;
            var blank = column - slot % column;
            
            deltaSize = UIBluePrintItemSlot.DefaultItemSlotRectSize * new Vector2(width, row);
                
            return new UIBluePrintItemSlotArray(priority, row, column,new Vector2(x,y),Vector3.zero,deltaSize,blank);

        }
        
        
    }
}