using System;
using Cinemachine;
using UnityEngine;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineVirtualCamera virtualCamera;
        [SerializeField] private Vector2 sensitivity = Vector2.one;
        
        [SerializeField] private float cameraLerpSpeed = 0.1f;
        
        private CinemachineFramingTransposer cinemachineFraming;

        private void Awake()
        {
            cinemachineFraming = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        }

        Vector2 _lastClickedPosition;
        Vector3 _targetRotation;
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
                _targetRotation = transform.rotation.eulerAngles;
                var currentClickedPosition = InputManager.Playable.ClickPosition.ReadValue<Vector2>();
                var delta = currentClickedPosition - _lastClickedPosition;
                _lastClickedPosition = currentClickedPosition;

                _targetRotation.x -= delta.y * sensitivity.y;
                if (90 < _targetRotation.x && _targetRotation.x < 180)
                {
                    _targetRotation.x = 90;
                }
                else if (180 < _targetRotation.x && _targetRotation.x < 270)
                {
                    _targetRotation.x = 270;
                }
                
                _targetRotation.y += delta.x * sensitivity.x;
                _targetRotation.z = 0;
            }
            transform.rotation = Quaternion.Euler(Vector3.Lerp(transform.rotation.eulerAngles,_targetRotation,cameraLerpSpeed));
        }
    }
}