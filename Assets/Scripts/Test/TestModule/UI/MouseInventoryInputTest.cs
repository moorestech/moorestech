using System;
using MainGame.GameLogic.Inventory;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class MouseInventoryInputTest : MonoBehaviour
    {
        [SerializeField] private EquippedItemViewControl equippedItemViewControl;
        [SerializeField] private MouseInventoryInput mouseInventoryInput;

        [SerializeField] private MainInventoryItemView mainInventoryItem;
        [SerializeField] private Camera camera;

        private void Start()
        {
            equippedItemViewControl.Construct(camera);
            mouseInventoryInput.Construct(mainInventoryItem,new PlayerInventoryItemMoveTest(),new InventoryUpdateEvent());
        }
    }
}