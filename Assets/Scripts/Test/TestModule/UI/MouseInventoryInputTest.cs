using System;
using System.Collections;
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

        private void Start()
        {
            equippedItemViewControl.Construct();
            mouseInventoryInput.Construct(mainInventoryItem,new PlayerInventoryItemMoveTest(),new InventoryUpdateEvent());

            StartCoroutine(PostStart());
        }

        private IEnumerator PostStart()
        {
            yield return new WaitForSeconds(0.1f);
            mouseInventoryInput.PostStart();
        }
    }
}