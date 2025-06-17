using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Tutorial
{
    public class HudArrowManager : MonoBehaviour
    {
        [SerializeField] private RectTransform hudArrowImagePrefab;
        private readonly Dictionary<GameObject, RectTransform> _hudArrowImages = new();
        public static HudArrowManager Instance { get; private set; }
        
        private void Awake()
        {
            Instance = this;
            
            RegisterHudArrowTarget(new GameObject("hogehogefoo"));
        }
        
        private void Update()
        {
            foreach (var (target, imageTransform) in _hudArrowImages)
            {
                if (target == null)
                    continue;
                    
                UpdateArrowTransform(target, imageTransform);
            }
        }
        
        public void RegisterHudArrowTarget(GameObject target)
        {
            _hudArrowImages[target] = Instantiate(hudArrowImagePrefab, transform);
        }
        
        public void UnregisterHudArrowTarget(GameObject target)
        {
            if (_hudArrowImages.TryGetValue(target, out var arrowTransform))
            {
                if (arrowTransform != null)
                    Destroy(arrowTransform.gameObject);
                _hudArrowImages.Remove(target);
            }
        }
        
        #region Internal
        
        private void UpdateArrowTransform(GameObject target, RectTransform arrowTransform)
        {
            if (arrowTransform == null || target == null)
                return;
                
            var camera = Camera.main;
            if (camera == null)
                return;
                
            var canvasRect = transform as RectTransform;
            if (canvasRect == null)
                return;
            
            var targetWorldPos = target.transform.position;
            var viewportPos = camera.WorldToViewportPoint(targetWorldPos);
            
            // 画面内かどうかの判定
            var isOnScreen = viewportPos.z > 0 && 
                             viewportPos.x >= 0 && viewportPos.x <= 1 && 
                             viewportPos.y >= 0 && viewportPos.y <= 1;
            
            if (isOnScreen)
            {
                // 画面内の場合
                arrowTransform.gameObject.SetActive(true);
                
                // ターゲットのスクリーン座標を取得
                var targetScreenPos = camera.WorldToScreenPoint(targetWorldPos);
                
                // スクリーン座標をCanvas座標に変換
                Vector2 targetCanvasPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, targetScreenPos, camera, out targetCanvasPos);
                
                // 矢印の位置をターゲット位置に設定
                arrowTransform.anchoredPosition = targetCanvasPos;
                
                // 画面中央からターゲットへの方向を計算
                var direction = targetCanvasPos.normalized;
                var arrowRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                
                arrowTransform.rotation = Quaternion.Euler(0, 0, arrowRotation);
                return;
            }
            
            // 画面外の場合
            arrowTransform.gameObject.SetActive(true);
            
            // カメラからターゲットへの方向ベクトル（ワールド空間）
            var cameraToTarget = targetWorldPos - camera.transform.position;
            
            // カメラの右方向と上方向
            var cameraRight = camera.transform.right;
            var cameraUp = camera.transform.up;
            
            // ターゲット方向をカメラのローカル空間に投影
            var localX = Vector3.Dot(cameraToTarget, cameraRight);
            var localY = Vector3.Dot(cameraToTarget, cameraUp);
            var localZ = Vector3.Dot(cameraToTarget, camera.transform.forward);
            
            // カメラの後ろにある場合の処理
            // 特に反転処理は不要
            
            // 画面上での方向ベクトル
            var direction = new Vector2(localX, localY).normalized;
            
            // Canvas のサイズ
            var canvasSize = canvasRect.rect.size;
            var margin = 100f;
            var maxX = (canvasSize.x * 0.5f) - margin;
            var maxY = (canvasSize.y * 0.5f) - margin;
            
            // 画面端への距離を計算
            var scaleX = Mathf.Abs(direction.x) > 0.001f ? maxX / Mathf.Abs(direction.x) : float.MaxValue;
            var scaleY = Mathf.Abs(direction.y) > 0.001f ? maxY / Mathf.Abs(direction.y) : float.MaxValue;
            var scale = Mathf.Min(scaleX, scaleY);
            
            // 矢印の位置
            var arrowPosition = direction * scale;
            
            // 矢印の回転（pivotが(1, 0.5)なので右向きが0度）
            var arrowRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            arrowTransform.anchoredPosition = arrowPosition;
            arrowTransform.rotation = Quaternion.Euler(0, 0, arrowRotation);
        }
        
        #endregion
    }
}