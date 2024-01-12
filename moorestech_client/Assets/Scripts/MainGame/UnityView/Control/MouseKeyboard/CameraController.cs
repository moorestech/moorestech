using System;
using Cinemachine;
using UnityEngine;

namespace MainGame.UnityView.Control.MouseKeyboard
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineVirtualCamera virtualCamera;
        [SerializeField] private Vector2 sensitivity = Vector2.one;
        [SerializeField] private float lerpSpeed = 5.0f; // Adjust this to change the lerp speed
        
        private CinemachineFramingTransposer _cinemachineFraming;
        private Quaternion _targetRotation; // The rotation to smoothly rotate towards

        private void Awake()
        {
            _cinemachineFraming = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
            _targetRotation = transform.rotation; // Initialize target rotation to current rotation
        }

        Vector2 _lastClickedPosition;
        private void Update()
        {
            _cinemachineFraming.m_CameraDistance += InputManager.UI.SwitchHotBar.ReadValue<float>() / -100;

            _cinemachineFraming.m_CameraDistance = Mathf.Clamp(_cinemachineFraming.m_CameraDistance, 1, 75);
            
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

                var rotation = _targetRotation.eulerAngles;
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
                _targetRotation = Quaternion.Euler(rotation);
            }

            var resultRotation = Quaternion.Lerp(transform.rotation, _targetRotation, lerpSpeed * Time.deltaTime);
            resultRotation = Quaternion.Euler(resultRotation.eulerAngles.x, resultRotation.eulerAngles.y, 0);
            transform.rotation = resultRotation;
        }
    }
}
