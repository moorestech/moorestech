using MainGame.GameLogic.Inventory;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using UnityEngine.Assertions;

namespace Test.PlayModeTest.UI
{
    public class InventoryViewTestModule : MonoBehaviour
    {
        [SerializeField] private MainInventoryItemView mainInventoryItemView;
        [SerializeField] private ItemImages itemImages;

        private void Start()
        {
            var update = new InventoryUpdateEvent();
            mainInventoryItemView.Construct(update,itemImages);
            
            //アイテムの設定
            update.OnOnInventoryUpdate(0,1,5);
            update.OnOnInventoryUpdate(5,2,10);
            update.OnOnInventoryUpdate(10,2,1);
            update.OnOnInventoryUpdate(40,2,1);
            update.OnOnInventoryUpdate(44,2,1);
            
            
            Assert.IsTrue(false);
        }
    }
}