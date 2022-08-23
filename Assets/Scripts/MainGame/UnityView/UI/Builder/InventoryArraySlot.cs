using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UnityView.UI.Builder
{
    public class InventoryArraySlot : MonoBehaviour
    {
        public List<InventoryItemSlot> SetArraySlot(int height, int weight,int bottomBlank,InventoryItemSlot slotPrefab)
        {
            var slots = new List<InventoryItemSlot>();
            for (int i = 0; i < height * weight - bottomBlank; i++)
            {
                slots.Add(Instantiate(slotPrefab, transform));
            }

            return slots;
        }
    }
}