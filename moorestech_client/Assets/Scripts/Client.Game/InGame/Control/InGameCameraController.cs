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
        private static readonly Vector3 FirstPersonTrackedOffset = new(0f, 1.6f, 0f);

        private bool _isFirstPerson;

        // プレイヤーが選んだ三人称の距離・追従オフセット。表示中の値はFPSのTween中間値になりうるため別に持つ
        // The player's chosen third-person distance and tracked offset; the live values can be mid-FPS-tween, so keep them apart
        private Vector3 _thirdPersonTrackedObjectOffset;
        private float _thirdPersonCameraDistance;

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

            _thirdPersonTrackedObjectOffset = _cinemachineFraming.m_TrackedObjectOffset;
            _thirdPersonCameraDistance = _cinemachineFraming.m_CameraDistance;
        }

        private void Update()
        {
            // FPS中はズームを止め、切替Tween中は表示距離をTweenに任せる（選択距離自体はここが唯一の書き手）
            // Zoom is off during FPS, and the tween owns the live distance while it runs (this is the sole writer of the chosen distance)
            if (!_isFirstPerson)
            {
                var distance = _thirdPersonCameraDistance;
                if (UnityEngine.Input.GetKey(KeyCode.F1)) distance -= Time.deltaTime * 3f; // TODO InputManagerに移動
                if (UnityEngine.Input.GetKey(KeyCode.F2)) distance += Time.deltaTime * 3f; // TODO InputManagerに移動
                _thirdPersonCameraDistance = Mathf.Clamp(distance, 0.6f, 10);

                if (!IsDistanceTweening()) _cinemachineFraming.m_CameraDistance = _thirdPersonCameraDistance;
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

            bool IsDistanceTweening()
            {
                return _distanceTweener != null && _distanceTweener.IsActive() && _distanceTweener.IsPlaying();
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

            // 目標は常にプレイヤーの選択値から取る（表示中の値を読むと連打時にTween中間値が記憶される）
            // Targets always come from the chosen values; reading the live ones would memorize a mid-tween value on rapid toggles
            var targetOffset = enabled ? FirstPersonTrackedOffset : _thirdPersonTrackedObjectOffset;
            var targetDistance = enabled ? FirstPersonCameraDistance : _thirdPersonCameraDistance;

            _offsetTweener = DOTween.To(() => _cinemachineFraming.m_TrackedObjectOffset, x => _cinemachineFraming.m_TrackedObjectOffset = x, targetOffset, FirstPersonTweenDuration);
            _distanceTweener = DOTween.To(() => _cinemachineFraming.m_CameraDistance, x => _cinemachineFraming.m_CameraDistance = x, targetDistance, FirstPersonTweenDuration).SetEase(Ease.InOutQuad);
        }
    }
}
