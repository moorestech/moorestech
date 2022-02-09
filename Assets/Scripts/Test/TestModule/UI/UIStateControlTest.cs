using System;
using MainGame.Control.UI.Control;
using UnityEngine;
using UnityEngine.Serialization;

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