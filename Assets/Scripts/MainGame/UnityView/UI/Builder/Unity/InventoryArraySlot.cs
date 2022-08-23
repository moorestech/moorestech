using System.Collections.Generic;
using UnityEngine;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public class InventoryArraySlot : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot inventoryItemSlot;
        public List<InventoryItemSlot> SetArraySlot(int height, int weight,int bottomBlank)
        {
            var slots = new List<InventoryItemSlot>();
            for (int i = 0; i < height * weight - bottomBlank; i++)
            {
                slots.Add(Instantiate(inventoryItemSlot, transform));
            }

            return slots;
        }
    }
}