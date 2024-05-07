using Cinemachine;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Control
{
    public class InGameCameraController : MonoBehaviour
    {
        [SerializeField] private Camera mainCamera;
        
        [SerializeField] private CinemachineVirtualCamera virtualCamera;
        [SerializeField] private Vector2 sensitivity = Vector2.one;
        [SerializeField] private float lerpSpeed = 5.0f; // Adjust this to change the lerp speed
        [SerializeField] public bool updateCameraAngle;
        
        private CinemachineFramingTransposer _cinemachineFraming;
        private Quaternion _targetRotation; // The rotation to smoothly rotate towards
        
        private void Awake()
        {
            _cinemachineFraming = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
            _targetRotation = transform.rotation; // Initialize target rotation to current rotation
        }
        
        private void Update()
        {
            var distance = _cinemachineFraming.m_CameraDistance + InputManager.UI.SwitchHotBar.ReadValue<float>() / -200f;
            _cinemachineFraming.m_CameraDistance = Mathf.Clamp(distance, 0.6f, 10);
            
            if (!updateCameraAngle) return;
            
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
            
            void LeapCameraRotation()
            {
                var resultRotation = Quaternion.Lerp(transform.rotation, _targetRotation, lerpSpeed * Time.deltaTime);
                resultRotation = Quaternion.Euler(resultRotation.eulerAngles.x, resultRotation.eulerAngles.y, 0);
                transform.rotation = resultRotation;
            }
            
            #endregion
        }
        public void SetActive(bool enable)
        {
            enabled = enable;
            mainCamera.gameObject.SetActive(enable);
        }
    }
}