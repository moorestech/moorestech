using MainGame.Control.UI.UIState;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class UIStateControlTest : MonoBehaviour
    {
        [SerializeField]private UIStateControl uiStateControl;
        [SerializeField] private BlockClickDetectTest blockClickDetectTest;

        private void Start()
        {
            uiStateControl.Construct(blockClickDetectTest);
        }
    }
}