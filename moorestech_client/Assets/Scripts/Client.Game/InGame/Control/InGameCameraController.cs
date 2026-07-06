using Cinemachine;
using Client.Common;
using Client.Game.InGame.UI.UIState.Input;
using Client.Input;
using DG.Tweening;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Control
{
    public class InGameCameraController : MonoBehaviour, IGameCamera, IInitializable
    {
        public Vector3 Position => transform.position;
        public Vector3 CameraEulerAngle => transform.rotation.eulerAngles;
        public float CameraDistance => _cinemachineFraming.m_CameraDistance;
        
        public Camera Camera => mainCamera;
        [SerializeField] private Camera mainCamera;
        
        [SerializeField] private CinemachineVirtualCamera virtualCamera;
        [SerializeField] private Vector2 sensitivity = Vector2.one;
        [SerializeField] private float lerpSpeed = 5.0f; // Adjust this to change the lerp speed
        
        private CinemachineFramingTransposer _cinemachineFraming;
        private Quaternion _targetRotation; // The rotation to smoothly rotate towards
        
        private DG.Tweening.Sequence _currentSequence;
        
        private bool _isControllable;
        
        public void Initialize()
        {
            CameraManager.RegisterCamera(this);
        }
        
        private void Awake()
        {
            _cinemachineFraming = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
            _targetRotation = transform.rotation; // Initialize target rotation to current rotation
        }
        
        private void Update()
        {
            var distance = _cinemachineFraming.m_CameraDistance;
            if (UnityEngine.Input.GetKey(KeyCode.F1)) distance -= Time.deltaTime * 3f; // TODO InputManagerに移動
            if (UnityEngine.Input.GetKey(KeyCode.F2)) distance += Time.deltaTime * 3f; // TODO InputManagerに移動
            _cinemachineFraming.m_CameraDistance = Mathf.Clamp(distance, 0.6f, 10);
            
            if (!_isControllable && _currentSequence == null) return;
            
            //マウスのインプットによって向きを変える
            if (_isControllable)
            {
                GetMouseInput();
            }
            LeapCameraRotation();
            
            #region Internal
            
            void GetMouseInput()
            {
                var delta = InputManager.Player.Look.ReadValue<Vector2>();
                
                var rotation = _targetRotation.eulerAngles;
                rotation.x -= delta.y * sensitivity.y;
                if (90 < rotation.x && rotation.x < 180)
                    rotation.x = 90;
                else if (180 < rotation.x && rotation.x < 270) rotation.x = 270;
                
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
        
        public void SetControllable(bool enable)
        {
            _isControllable = enable;
        }
        
        public void SetEnabled(bool cameraEnabled)
        {
            enabled = cameraEnabled;
            mainCamera.enabled = cameraEnabled;
            mainCamera.GetComponent<AudioListener>().enabled = cameraEnabled;
        }
        
        public void StartTweenCamera(Vector3 targetRotation, float targetDistance, float duration)
        {
            // DoTweenでカメラの向きを変える
            _currentSequence?.Kill();
            _currentSequence = DOTween.Sequence()
                .Append(DOTween.To(() => _targetRotation, x => _targetRotation = x, targetRotation, duration).SetEase(Ease.InOutQuad))
                .Join(DOTween.To(() => _cinemachineFraming.m_CameraDistance, x => _cinemachineFraming.m_CameraDistance = x, targetDistance, duration).SetEase(Ease.InOutQuad));
        }
        public void StartTweenCamera(TweenCameraInfo target)
        {
            StartTweenCamera(target.Rotation, target.Distance, target.TweenDuration);
        }
    }
}