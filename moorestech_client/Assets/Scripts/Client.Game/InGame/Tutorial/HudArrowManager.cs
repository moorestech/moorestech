using System.Collections.Generic;
using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    [DefaultExecutionOrder(1000)]
    public class HudArrowManager : MonoBehaviour
    {
        [SerializeField] private HudArrow hudArrowImagePrefab;
        private RectTransform _canvasRect;
        
        private readonly Dictionary<GameObject, HudArrow> _hudArrows = new();
        public static HudArrowManager Instance { get; private set; }
        
        private void Awake()
        {
            Instance = this;
            _canvasRect = transform as RectTransform;
        }
        
        private void LateUpdate()
        {
            foreach (var (target, arrow) in _hudArrows)
            {
                if (!target)
                    continue;
                
                arrow.ManualUpdate();
                UpdateArrowTransform(target, arrow);
            }
        }
        
        public void RegisterHudArrowTarget(GameObject target, HudArrowOptions options = default)
        {
            var arrow = Instantiate(hudArrowImagePrefab, transform);
            arrow.Initialize(target, options);
            _hudArrows[target] = arrow;
        }
        
        public void UnregisterHudArrowTarget(GameObject target)
        {
            if (_hudArrows.TryGetValue(target, out var arrowTransform))
            {
                if (arrowTransform != null)
                    Destroy(arrowTransform.gameObject);
                _hudArrows.Remove(target);
            }
        }
        
        private void UpdateArrowTransform(GameObject target, HudArrow arrow)
        {
            if (arrow == null || target == null)
                return;
            
            var currentCamera = CameraManager.Instance.MainCamera.MainCamera;
            if (!currentCamera)
                return;
            
            var targetWorldPos = target.transform.position;
            var viewportPos = currentCamera.WorldToViewportPoint(targetWorldPos);
            
            // 画面内かどうかの判定
            var isOnScreen = viewportPos.z > 0 &&
                             viewportPos.x >= 0 && viewportPos.x <= 1 &&
                             viewportPos.y >= 0 && viewportPos.y <= 1;
            
            if (isOnScreen) OnScreenProcess();
            else OffScreenProcess();
            
            return;
            
            
            #region Internal
            
            void OnScreenProcess()
            {
                // 画面内の場合
                
                // Canvas のサイズ
                var canvasSize = _canvasRect.rect.size;
                
                // ビューポート座標をCanvas座標に変換（0-1を-0.5～0.5に変換してからCanvasサイズを掛ける）
                var targetCanvasPos = new Vector2(
                    (viewportPos.x - 0.5f) * canvasSize.x,
                    (viewportPos.y - 0.5f) * canvasSize.y
                );
                
                // 画面中央からターゲットへの方向を計算
                var direction = targetCanvasPos.normalized;
                var arrowRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                
                var rotation = Quaternion.Euler(0, 0, arrowRotation);
                // 矢印の位置をターゲット位置に設定
                arrow.SetArrowTransform(targetCanvasPos, rotation, true);
            }
            
            void OffScreenProcess()
            {
                // 画面外の場合
                
                // カメラからターゲットへの方向ベクトル（ワールド空間）
                var cameraToTarget = targetWorldPos - currentCamera.transform.position;
                
                // カメラの右方向と上方向
                var cameraRight = currentCamera.transform.right;
                var cameraUp = currentCamera.transform.up;
                
                // ターゲット方向をカメラのローカル空間に投影
                var localX = Vector3.Dot(cameraToTarget, cameraRight);
                var localY = Vector3.Dot(cameraToTarget, cameraUp);
                var localZ = Vector3.Dot(cameraToTarget, currentCamera.transform.forward);
                
                // カメラの後ろにある場合の処理
                // 特に反転処理は不要
                
                // 画面上での方向ベクトル
                var direction = new Vector2(localX, localY).normalized;
                
                // Canvas のサイズ
                var canvasSize = _canvasRect.rect.size;
                var margin = 0f;
                var maxX = canvasSize.x * 0.5f - margin;
                var maxY = canvasSize.y * 0.5f - margin;
                
                // 画面端への距離を計算
                var scaleX = Mathf.Abs(direction.x) > 0.001f ? maxX / Mathf.Abs(direction.x) : float.MaxValue;
                var scaleY = Mathf.Abs(direction.y) > 0.001f ? maxY / Mathf.Abs(direction.y) : float.MaxValue;
                var scale = Mathf.Min(scaleX, scaleY);
                
                // 矢印の位置
                var arrowPosition = direction * scale;
                
                // 矢印の回転（pivotが(1, 0.5)なので右向きが0度）
                var arrowRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                
                arrow.SetArrowTransform(arrowPosition, Quaternion.Euler(0, 0, arrowRotation), false);
            }
            
  #endregion
        }
        
        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }
    }
    
    public struct HudArrowOptions
    {
        public bool HideWhenTargetInactive;
        
        public HudArrowOptions(bool hideWhenTargetInactive = true)
        {
            HideWhenTargetInactive = hideWhenTargetInactive;
        }
    }
}