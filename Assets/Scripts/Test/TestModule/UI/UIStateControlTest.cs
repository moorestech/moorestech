using MainGame.Control.UI.UIState;
using MainGame.Network;
using MainGame.Network.Send;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class UIStateControlTest : MonoBehaviour
    {
        [SerializeField]private UIStateControl uiStateControl;
        [SerializeField] private BlockClickDetectTest blockClickDetectTest;

        private void Start()
        {
            var inventory = new RequestPlayerInventoryProtocol(new TestSocketModule(), new PlayerConnectionSetting(0));
            uiStateControl.Construct(blockClickDetectTest,inventory);
        }
    }
}