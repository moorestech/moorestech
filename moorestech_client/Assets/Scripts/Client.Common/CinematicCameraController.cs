using UnityEngine;

namespace Client.Common
{
    /// <summary>
    /// トレイラー撮影のためのスムーズに動くカメラ
    /// デバッグコマンドから使用する
    /// </summary>
    public class CinematicCameraController : MonoBehaviour, IGameCamera
    {
        private const float StartVerticalRotationAngle = 70;
        private const float RockVerticalRotationAngle = 88;
        [SerializeField] private Transform cameraRootTransform;
        [SerializeField] private Transform cameraXTransform;
        [SerializeField] private Transform cameraYTransform;
        [SerializeField] private float mouseSpeed = 1f;
        [SerializeField] private float cameraSpeed = 0.05f;
        
        [SerializeField] private float positionMoveSpeed = 0.05f;
        [SerializeField] private float positionLerpSpeed = 0.05f;
        
        public Camera Camera => camera;
        [SerializeField] private Camera camera;
        
        
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
        }
        
        private void Update()
        {
            //カーソルを消す
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            
            float sensi;
            sensi = mouseSpeed;
            var xMouseRot = Input.GetAxis("Mouse X") * sensi;
            var yMouseRot = Input.GetAxis("Mouse Y") * sensi;
            
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
                Input.GetKey(KeyCode.W) ? 1 :
                Input.GetKey(KeyCode.S) ? -1 : 0,
                Input.GetKey(KeyCode.LeftShift) ? -1 :
                Input.GetKey(KeyCode.Space) ? 1 : 0,
                Input.GetKey(KeyCode.A) ? -1 :
                Input.GetKey(KeyCode.D) ? 1 : 0
            );
            
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
        
        public void SetCameraRotation(Quaternion rotation)
        {
            var euler = rotation.eulerAngles;
            TargetCameraXRot = Quaternion.Euler(euler.x, 0, 0);
            TargetCameraYRot = Quaternion.Euler(0, euler.y, 0);
            cameraXTransform.localRotation = TargetCameraXRot;
            cameraYTransform.localRotation = TargetCameraYRot;
        }
        
        public void SetEnabled(bool cameraEnabled)
        {
            enabled = cameraEnabled;
            camera.enabled = cameraEnabled;
            camera.GetComponent<AudioListener>().enabled = cameraEnabled;
        }
    }
}