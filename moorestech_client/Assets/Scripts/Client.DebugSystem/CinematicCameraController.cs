using Client.Common;
using Client.Game.InGame.UI.UIState;
using UnityEngine;

namespace Client.DebugSystem
{
    /// <summary>
    /// トレイラー撮影のためのスムーズに動くカメラ
    /// デバッグコマンドから使用する
    /// </summary>
    public class CinematicCameraController : MonoBehaviour, IGameCamera
    {
        private const float StartVerticalRotationAngle = 70;
        private const float RockVerticalRotationAngle = 88;
        
        private UIStateControl _uiStateControl;
        
        [SerializeField] private Transform cameraRootTransform;
        [SerializeField] private Transform cameraXTransform;
        [SerializeField] private Transform cameraYTransform;
        [SerializeField] private float mouseSpeed = 1f;
        [SerializeField] private float cameraSpeed = 0.05f;
        
        [SerializeField] private float positionMoveSpeed = 0.05f;
        [SerializeField] private float positionLerpSpeed = 0.05f;
        
        [SerializeField] private float sprintMagnitude = 1.5f;
        
        public Camera Camera => camera;
        [SerializeField] private Camera camera;
        
        [SerializeField] private float targetFOV = 60f;
        [SerializeField] private float fovSpeed = 0.1f;
        [SerializeField] private float fovScrollSpeed = 5f;
        [SerializeField] private float minFOV = 20f;
        [SerializeField] private float maxFOV = 90f;
        
        
        /// <summary>
        ///     キーボードの操作に対してカメラをゆっくりと動かすために、目標の位置を保持する
        /// </summary>
        private Vector3 _targetPosition;
        
        private float lastXmouse;
        
        /// <summary>
        ///     マウスの操作に対してカメラをゆっくりと動かすために、目標の回転角度を保持する
        /// </summary>
        public Quaternion TargetCameraYRot { get; private set; }
        
        public Quaternion TargetCameraXRot { get; private set; }
        
        //カメラの最初の向きを飛行機の向きと同じにする
        private void Awake()
        {
            _targetPosition = cameraRootTransform.position;
            TargetCameraXRot = cameraXTransform.localRotation;
            TargetCameraYRot = cameraYTransform.localRotation;
            targetFOV = camera.fieldOfView;
            
            // UIStateControlを検索
            _uiStateControl = FindObjectOfType<UIStateControl>();
        }
        
        private void Update()
        {
            // UIStateがGameScreenでない場合はカメラコントロールを無効化
            if (_uiStateControl != null && _uiStateControl.CurrentState != UIStateEnum.GameScreen)
            {
                return;
            }
            
            // マウスホイールでFOVを変更
            var scrollDelta = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (scrollDelta != 0)
            {
                targetFOV -= scrollDelta * fovScrollSpeed;
                targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
            }
            
            // FOVをスムーズに変更
            camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, targetFOV, fovSpeed);
            
            float sensi;
            sensi = mouseSpeed;
            var xMouseRot = UnityEngine.Input.GetAxis("Mouse X") * sensi;
            var yMouseRot = UnityEngine.Input.GetAxis("Mouse Y") * sensi;
            
            // マウスの上下方向の動きはカメラのX軸に、マウスの左右方向の動きはカメラのY軸に対して回転させる
            TargetCameraXRot *= Quaternion.Euler(-yMouseRot, 0, 0);
            
            //X軸を-85〜85度に制限する
            var xRotEuler = TargetCameraXRot.eulerAngles.x;
            //角度を-180〜180度に変換する
            xRotEuler = xRotEuler > 180 ? xRotEuler - 360 : xRotEuler;
            
            //もしX軸が-85度から85どの範囲外の場合はX軸のオブジェクトの動きを加算する
            if (xRotEuler is <= -StartVerticalRotationAngle or >= StartVerticalRotationAngle)
            {
                var addXRate = (Mathf.Abs(xRotEuler) - StartVerticalRotationAngle) / (RockVerticalRotationAngle - StartVerticalRotationAngle);
                addXRate *= 0.7f;
                if (-0.2f < xMouseRot && xMouseRot < 0.2)
                    // +-1の範囲は前回と同じ方向に回転する
                    addXRate *= lastXmouse < 0 ? -1 : 1;
                else
                    // +-1の範囲外はマウスの動きに合わせて回転する
                    addXRate *= xMouseRot < 0 ? -1 : 1;
                
                TargetCameraYRot *= Quaternion.Euler(0, xMouseRot + yMouseRot * addXRate, 0);
            }
            else
            {
                TargetCameraYRot *= Quaternion.Euler(0, xMouseRot, 0);
            }
            
            xRotEuler = Mathf.Clamp(xRotEuler, -RockVerticalRotationAngle, RockVerticalRotationAngle);
            TargetCameraXRot = Quaternion.Euler(xRotEuler, 0, 0);
            
            
            cameraXTransform.localRotation = Quaternion.Lerp(cameraXTransform.localRotation, TargetCameraXRot, cameraSpeed);
            cameraYTransform.localRotation = Quaternion.Lerp(cameraYTransform.localRotation, TargetCameraYRot, cameraSpeed);
            
            
            //キーボード入力をとり、ターゲット位置を更新する
            var move = new Vector3(
                UnityEngine.Input.GetKey(KeyCode.W) ? 1 :
                UnityEngine.Input.GetKey(KeyCode.S) ? -1 : 0,
                UnityEngine.Input.GetKey(KeyCode.Q) ? -1 :
                UnityEngine.Input.GetKey(KeyCode.E) ? 1 : 0,
                UnityEngine.Input.GetKey(KeyCode.A) ? -1 :
                UnityEngine.Input.GetKey(KeyCode.D) ? 1 : 0
            );
            move *= UnityEngine.Input.GetKey(KeyCode.LeftShift) ? sprintMagnitude : 1;
            
            //カメラの向きに合わせてXとZのみ移動方向を変更する
            var cameraForward = transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();
            var cameraRight = transform.right;
            cameraRight.y = 0;
            cameraRight.Normalize();
            _targetPosition += cameraForward * move.x * positionMoveSpeed;
            _targetPosition += cameraRight * move.z * positionMoveSpeed;
            _targetPosition.y += move.y * positionMoveSpeed;
            
            
            //線形補完でカメラの位置を更新する
            cameraRootTransform.position = Vector3.Lerp(cameraRootTransform.position, _targetPosition, positionLerpSpeed);
            
            
            lastXmouse = xMouseRot;
        }
        
        public void SetCameraTransform(Vector3 position, Quaternion rotation)
        {
            var euler = rotation.eulerAngles;
            TargetCameraXRot = Quaternion.Euler(euler.x, 0, 0);
            TargetCameraYRot = Quaternion.Euler(0, euler.y, 0);
            cameraXTransform.localRotation = TargetCameraXRot;
            cameraYTransform.localRotation = TargetCameraYRot;
            
            // positionのセット
            _targetPosition = position;
            cameraRootTransform.position = position;
        }
        
        public void SetEnabled(bool cameraEnabled)
        {
            enabled = cameraEnabled;
            camera.enabled = cameraEnabled;
            camera.GetComponent<AudioListener>().enabled = cameraEnabled;
        }
        
        #region 調整用メソッド
        
        public void SetCameraSpeed(float speed)
        {
            cameraSpeed = speed;
        }
        
        public void SetPositionMoveSpeed(float speed)
        {
            positionMoveSpeed = speed;
        }
        
        public void SetMouseSpeed(float speed)
        {
            mouseSpeed = speed;
        }
        
        public void SetTargetFOV(float fov)
        {
            targetFOV = Mathf.Clamp(fov, minFOV, maxFOV);
        }
        
        public void SetFOVScrollSpeed(float speed)
        {
            fovScrollSpeed = speed;
        }
        
        #endregion
    }
}