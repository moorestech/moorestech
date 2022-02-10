using System.Collections;
using MainGame.Control.UI.Inventory;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;

namespace Test.TestModule.UI
{
    //TODO ここのテストコードも修正する
    public class MouseInventoryInputTest : MonoBehaviour
    {
        [SerializeField] private MouseInventoryInput mouseInventoryInput;

        [SerializeField] private PlayerInventoryItemView playerInventoryItem;

        private void Start()
        {
            mouseInventoryInput.Construct(playerInventoryItem);

            StartCoroutine(PostStart());
        }

        private IEnumerator PostStart()
        {
            yield return new WaitForSeconds(0.1f);
            mouseInventoryInput.PostStart();
        }
    }
}