using Cinemachine;
using Client.Common;
using Client.Input;
using DG.Tweening;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Control
{
    public class InGameCameraController : MonoBehaviour, IGameCamera, IInitializable
    {
        public Vector3 Position => transform.position;
        public float CameraDistance => _cinemachineFraming.m_CameraDistance;

        public Camera Camera => mainCamera;
        [SerializeField] private Camera mainCamera;

        [SerializeField] private CinemachineVirtualCamera virtualCamera;
        [SerializeField] private Vector2 sensitivity = Vector2.one;
        [SerializeField] private float lerpSpeed = 5.0f; // Adjust this to change the lerp speed

        private CinemachineFramingTransposer _cinemachineFraming;
        private Quaternion _targetRotation; // The rotation to smoothly rotate towards

        private bool _isControllable;

        private const float FirstPersonCameraDistance = 0.15f;
        private const float FirstPersonTweenDuration = 0.25f;
        private const float DefaultThirdPersonCameraDistance = 5f;
        private static readonly Vector3 FirstPersonTrackedOffset = new(0f, 1.6f, 0f);

        private bool _isFirstPerson;
        private Vector3 _storedTrackedObjectOffset;
        private float _storedThirdPersonCameraDistance = DefaultThirdPersonCameraDistance;
        private Tweener _offsetTweener;
        private Tweener _distanceTweener;

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
            // FPS中と視点モード切替Tween中は距離クランプが目標距離を上書きするためズーム処理ごと止める
            // Skip zoom during FPS and while the view-mode tween runs, because the clamp would override the target distance
            if (!_isFirstPerson && !IsDistanceTweening())
            {
                var distance = _cinemachineFraming.m_CameraDistance;
                if (UnityEngine.Input.GetKey(KeyCode.F1)) distance -= Time.deltaTime * 3f; // TODO InputManagerに移動
                if (UnityEngine.Input.GetKey(KeyCode.F2)) distance += Time.deltaTime * 3f; // TODO InputManagerに移動
                _cinemachineFraming.m_CameraDistance = Mathf.Clamp(distance, 0.6f, 10);
            }

            if (!_isControllable) return;

            //マウスのインプットによって向きを変える
            GetMouseInput();
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

        // 視点モードの切替。カメラの向きには触れず、追従オフセットと距離だけをTweenする
        // Switches the view mode; the look direction is untouched and only the tracked offset and distance are tweened
        public void SetFirstPersonMode(bool enabled)
        {
            if (_isFirstPerson == enabled) return;
            _isFirstPerson = enabled;

            _offsetTweener?.Kill();
            _distanceTweener?.Kill();

            // 三人称の追従オフセットと距離を保存し、頭部高さ・最小距離へ寄せる（TPS復帰時はそれを戻す）
            // Store the third-person tracked offset and distance, then move to head height and the minimum distance (restored when returning to TPS)
            if (enabled)
            {
                _storedTrackedObjectOffset = _cinemachineFraming.m_TrackedObjectOffset;
                _storedThirdPersonCameraDistance = _cinemachineFraming.m_CameraDistance;
            }

            var targetOffset = enabled ? FirstPersonTrackedOffset : _storedTrackedObjectOffset;
            var targetDistance = enabled ? FirstPersonCameraDistance : _storedThirdPersonCameraDistance;

            _offsetTweener = DOTween.To(() => _cinemachineFraming.m_TrackedObjectOffset, x => _cinemachineFraming.m_TrackedObjectOffset = x, targetOffset, FirstPersonTweenDuration);
            _distanceTweener = DOTween.To(() => _cinemachineFraming.m_CameraDistance, x => _cinemachineFraming.m_CameraDistance = x, targetDistance, FirstPersonTweenDuration).SetEase(Ease.InOutQuad);
        }

        private bool IsDistanceTweening()
        {
            return _distanceTweener != null && _distanceTweener.IsActive() && _distanceTweener.IsPlaying();
        }
    }
}
