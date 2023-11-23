using System;
using Cinemachine;
using UnityEngine;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineVirtualCamera virtualCamera;
        [SerializeField] private Vector2 sensitivity = Vector2.one;
        private CinemachineFramingTransposer cinemachineFraming;

        private void Awake()
        {
            cinemachineFraming = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        }

        Vector2 _lastClickedPosition;
        private void Update()
        {
            cinemachineFraming.m_CameraDistance += InputManager.UI.SwitchHotBar.ReadValue<float>() / -100;

            cinemachineFraming.m_CameraDistance = Mathf.Clamp(cinemachineFraming.m_CameraDistance, 1, 75);
            
            //マウスのインプットによって向きを変える
            if (InputManager.Playable.ScreenRightClick.GetKeyDown)
            {
                _lastClickedPosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
            }
            
            if (InputManager.Playable.ScreenRightClick.GetKey)
            {
                var currentClickedPosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
                var delta = currentClickedPosition - _lastClickedPosition;
                _lastClickedPosition = currentClickedPosition;

                var rotation = transform.rotation.eulerAngles;
                rotation.x -= delta.y * sensitivity.y;
                if (90 < rotation.x && rotation.x < 180)
                {
                    rotation.x = 90;
                }
                else if (180 < rotation.x && rotation.x < 270)
                {
                    rotation.x = 270;
                }
                
                rotation.y += delta.x * sensitivity.x;
                rotation.z = 0;
                transform.rotation = Quaternion.Euler(rotation);
            }
        }
    }
}