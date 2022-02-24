using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class CraftingInventoryItemViewTest : MonoBehaviour
    {
        [SerializeField] private CraftingSlotItemView craftingSlotItemView;
        [SerializeField] private ItemImages itemImages;
        void Start()
        {
            craftingSlotItemView.Construct(itemImages);
            craftingSlotItemView.GetInventoryItemSlots();
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
