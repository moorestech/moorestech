using System.Collections;
using MainGame.Control.UI.Inventory;
using MainGame.GameLogic.Event;
using MainGame.GameLogic.Inventory;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using UnityEngine.Serialization;

namespace Test.TestModule.UI
{
    public class MouseInventoryInputTest : MonoBehaviour
    {
        [SerializeField] private EquippedItemViewControl equippedItemViewControl;
        [SerializeField] private MouseInventoryInput mouseInventoryInput;

        [FormerlySerializedAs("mainInventoryItem")] [SerializeField] private PlayerInventoryItemView playerInventoryItem;

        private void Start()
        {
            equippedItemViewControl.Construct();
            mouseInventoryInput.Construct(playerInventoryItem,new PlayerInventoryItemMoveTest(),new InventoryUpdateEvent());

            StartCoroutine(PostStart());
        }

        private IEnumerator PostStart()
        {
            yield return new WaitForSeconds(0.1f);
            mouseInventoryInput.PostStart();
        }
    }
}