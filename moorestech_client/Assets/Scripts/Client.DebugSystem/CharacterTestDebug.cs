using System;
using Client.Game.InGame.Control;
using UnityEngine;

namespace Client.DebugSystem
{
    public class CharacterTestDebug : MonoBehaviour
    {
        [SerializeField] private InGameCameraController _cameraController;
        
        private void Start()
        {
            _cameraController.SetControllable(true);
        }
    }
}