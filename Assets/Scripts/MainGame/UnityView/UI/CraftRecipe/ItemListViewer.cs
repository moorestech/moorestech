using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class ItemListViewer : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot inventoryItemSlotPrefab;
        
        [Inject]
        public void Construct(ItemImages itemImages)
        {
            for (int i = 0; i < itemImages.GetItemNum(); i++)
            {
                var g = Instantiate(inventoryItemSlotPrefab, transform, true);
                g.Construct(i);
                g.SetItem(itemImages.GetItemViewData(i),0);

                g.transform.localScale = new Vector3(1,1,1);
            }
        }
    }
}