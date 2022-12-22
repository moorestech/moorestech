using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public class MouseCursorController : MonoBehaviour
    {
        [SerializeField] CameraController cameraController;

        private void Update()
        {
            SetCursor(!cameraController.IsCameraControlling);
        }


        private void SetCursor(bool enable)
        {
            Cursor.visible = enable;
            Cursor.lockState = enable ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }
}