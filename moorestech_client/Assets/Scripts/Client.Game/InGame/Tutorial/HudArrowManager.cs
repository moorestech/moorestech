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
                // 画面内の場合は一旦非表示
                arrowTransform.gameObject.SetActive(false);
                return;
            }
            
            // 画面外の場合
            arrowTransform.gameObject.SetActive(true);
            
            // ターゲットのスクリーン座標を取得（画面外でも計算される）
            var targetScreenPos = camera.WorldToScreenPoint(targetWorldPos);
            
            // スクリーン中央の座標
            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            
            // 中央からターゲットへの方向
            var directionFromCenter = new Vector2(targetScreenPos.x - screenCenter.x, targetScreenPos.y - screenCenter.y);
            
            // カメラの後ろにある場合は方向を反転
            if (targetScreenPos.z < 0)
            {
                directionFromCenter = -directionFromCenter;
            }
            
            directionFromCenter = directionFromCenter.normalized;
            
            // Canvasのスクリーン座標での矩形を取得
            var canvasCorners = new Vector3[4];
            canvasRect.GetWorldCorners(canvasCorners);
            
            // ワールド座標をスクリーン座標に変換
            for (int i = 0; i < 4; i++)
            {
                canvasCorners[i] = RectTransformUtility.WorldToScreenPoint(camera, canvasCorners[i]);
            }
            
            // Canvas の境界（スクリーン座標）
            var minX = canvasCorners[0].x + 100f; // 左端 + マージン
            var maxX = canvasCorners[2].x - 100f; // 右端 - マージン
            var minY = canvasCorners[0].y + 100f; // 下端 + マージン
            var maxY = canvasCorners[2].y - 100f; // 上端 - マージン
            
            // レイキャスト的なアプローチで画面端を見つける
            var t = float.MaxValue;
            
            // 各辺との交点を計算
            if (directionFromCenter.x > 0) // 右方向
                t = Mathf.Min(t, (maxX - screenCenter.x) / directionFromCenter.x);
            else if (directionFromCenter.x < 0) // 左方向
                t = Mathf.Min(t, (minX - screenCenter.x) / directionFromCenter.x);
                
            if (directionFromCenter.y > 0) // 上方向
                t = Mathf.Min(t, (maxY - screenCenter.y) / directionFromCenter.y);
            else if (directionFromCenter.y < 0) // 下方向
                t = Mathf.Min(t, (minY - screenCenter.y) / directionFromCenter.y);
            
            // 画面端の位置（スクリーン座標）
            var edgeScreenPos = screenCenter + directionFromCenter * t;
            
            // スクリーン座標をCanvas座標に変換
            Vector2 arrowPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, edgeScreenPos, camera, out arrowPosition);
            
            // 矢印の回転
            var arrowRotation = Mathf.Atan2(directionFromCenter.y, directionFromCenter.x) * Mathf.Rad2Deg;
            
            arrowTransform.anchoredPosition = arrowPosition;
            arrowTransform.rotation = Quaternion.Euler(0, 0, arrowRotation);
        }
        
        #endregion
    }
}