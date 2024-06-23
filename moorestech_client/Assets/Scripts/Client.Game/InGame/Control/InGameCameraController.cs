﻿using Cinemachine;
using Client.Input;
using DG.Tweening;
using UnityEngine;

namespace Client.Game.InGame.Control
{
    public class InGameCameraController : MonoBehaviour
    {
        public Vector3 CameraEulerAngle => transform.rotation.eulerAngles;
        public float CameraDistance => _cinemachineFraming.m_CameraDistance;
        
        [SerializeField] private Camera mainCamera;
        
        [SerializeField] private CinemachineVirtualCamera virtualCamera;
        [SerializeField] private Vector2 sensitivity = Vector2.one;
        [SerializeField] private float lerpSpeed = 5.0f; // Adjust this to change the lerp speed
        
        private CinemachineFramingTransposer _cinemachineFraming;
        private Quaternion _targetRotation; // The rotation to smoothly rotate towards
        
        private DG.Tweening.Sequence _currentSequence;
        
        private bool _updateCameraAngle;
        
        private void Awake()
        {
            _cinemachineFraming = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
            _targetRotation = transform.rotation; // Initialize target rotation to current rotation
        }
        
        private void Update()
        {
            var distance = _cinemachineFraming.m_CameraDistance + InputManager.UI.SwitchHotBar.ReadValue<float>() / -200f;
            _cinemachineFraming.m_CameraDistance = Mathf.Clamp(distance, 0.6f, 10);
            
            if (!_updateCameraAngle && _currentSequence == null) return;
            
            //マウスのインプットによって向きを変える
            UpdateCameraRotation();
            LeapCameraRotation();
            
            #region Internal
            
            void UpdateCameraRotation()
            {
                var delta = InputManager.Player.Look.ReadValue<Vector2>();
                
                var rotation = _targetRotation.eulerAngles;
                rotation.x -= delta.y * sensitivity.y;
                if (90 < rotation.x && rotation.x < 180)
                    rotation.x = 90;
                else if (180 < rotation.x && rotation.x < 270) rotation.x = 270;
                
                rotation.y += delta.x * sensitivity.x;
                rotation.z = 0;
                
                var rotationDiff = rotation - _targetRotation.eulerAngles;
                if (0.1f < rotationDiff.magnitude)
                {
                    _currentSequence?.Kill();
                    _currentSequence = null;
                    _targetRotation = Quaternion.Euler(rotation);
                }
            }
            
            void LeapCameraRotation()
            {
                var resultRotation = Quaternion.Lerp(transform.rotation, _targetRotation, lerpSpeed * Time.deltaTime);
                resultRotation = Quaternion.Euler(resultRotation.eulerAngles.x, resultRotation.eulerAngles.y, 0);
                transform.rotation = resultRotation;
                
                if (_currentSequence != null && _currentSequence.IsComplete() && 
                    Quaternion.Angle(transform.rotation, _targetRotation) < 0.1f)
                {
                    _currentSequence?.Kill();
                    _currentSequence = null;
                }
            }
            
            #endregion
        }
        
        public void SetActive(bool enable)
        {
            enabled = enable;
            mainCamera.gameObject.SetActive(enable);
        }
        
        public void SetUpdateCameraAngle(bool enable)
        {
            _updateCameraAngle = enable;
        }
        
        public void StartTweenCamera(Vector3 targetRotation, float targetDistance, float duration)
        {
            // DoTweenでカメラの向きを変える
            _currentSequence?.Kill();
            _currentSequence = DOTween.Sequence()
                .Append(DOTween.To(() => _targetRotation, x => _targetRotation = x, targetRotation, duration).SetEase(Ease.InOutQuad))
                .Join(DOTween.To(() => _cinemachineFraming.m_CameraDistance, x => _cinemachineFraming.m_CameraDistance = x, targetDistance, duration).SetEase(Ease.InOutQuad));
        }
    }
}