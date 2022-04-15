using System.Collections.Generic;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.View.SubInventory
{
    public class InventoryArraySlot : MonoBehaviour
    {
        public List<InventoryItemSlot> SetArraySlot(int height, int weight,InventoryItemSlot slotPrefab)
        {
            var slots = new List<InventoryItemSlot>();
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < weight; j++)
                {
                    slots.Add(Instantiate(slotPrefab, transform));
                }
            }

            return slots;
        }
    }
}