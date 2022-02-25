using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class CraftingInventoryItemViewTest : MonoBehaviour
    {
        [SerializeField] private CraftingInventoryItemView craftingInventoryItemView;
        [SerializeField] private ItemImages itemImages;
        void Start()
        {
            craftingInventoryItemView.Construct(itemImages);
            craftingInventoryItemView.GetInventoryItemSlots();
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
