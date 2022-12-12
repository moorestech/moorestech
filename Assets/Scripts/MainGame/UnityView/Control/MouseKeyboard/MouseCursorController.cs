using System;
using UnityEngine;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public class MouseCursorController : MonoBehaviour
    {
        [SerializeField] CinemachineController cinemachineController;

        private void Update()
        {
            SetCursor(!cinemachineController.IsCameraControlling);
        }


        private void SetCursor(bool enable)
        {
            Cursor.visible = enable;
            Cursor.lockState = enable ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }
}