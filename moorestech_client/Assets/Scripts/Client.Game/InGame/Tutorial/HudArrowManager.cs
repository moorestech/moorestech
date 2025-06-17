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
                
            var targetWorldPos = target.transform.position;
            var targetScreenPos = camera.WorldToScreenPoint(targetWorldPos);
            
            var canvasRect = transform as RectTransform;
            if (canvasRect == null)
                return;
                
            var isTargetVisible = IsTargetVisible(targetScreenPos, camera);
            
            Debug.Log($"[HudArrow] Target: {target.name}");
            Debug.Log($"[HudArrow] WorldPos: {targetWorldPos}");
            Debug.Log($"[HudArrow] ScreenPos: {targetScreenPos}");
            Debug.Log($"[HudArrow] Screen Size: {Screen.width}x{Screen.height}");
            Debug.Log($"[HudArrow] IsVisible: {isTargetVisible}");
            Debug.Log($"[HudArrow] Canvas Rect Size: {canvasRect.rect.size}");
            
            Vector2 arrowPosition;
            float arrowRotation;
            
            if (isTargetVisible)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, targetScreenPos, camera, out arrowPosition);
                arrowRotation = CalculateArrowRotationToTarget(Vector2.zero, arrowPosition);
                
                Debug.Log($"[HudArrow] Visible - ArrowPos: {arrowPosition}, Rotation: {arrowRotation}");
            }
            else
            {
                var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var directionToTarget = (new Vector2(targetScreenPos.x, targetScreenPos.y) - screenCenter).normalized;
                var clampedScreenPos = ClampToScreenEdge(screenCenter, directionToTarget);
                
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, clampedScreenPos, camera, out arrowPosition);
                arrowRotation = CalculateArrowRotationToTarget(Vector2.zero, directionToTarget);
                
                Debug.Log($"[HudArrow] Hidden - ScreenCenter: {screenCenter}");
                Debug.Log($"[HudArrow] Hidden - Direction: {directionToTarget}");
                Debug.Log($"[HudArrow] Hidden - ClampedPos: {clampedScreenPos}");
                Debug.Log($"[HudArrow] Hidden - ArrowPos: {arrowPosition}, Rotation: {arrowRotation}");
            }
            
            arrowTransform.anchoredPosition = arrowPosition;
            arrowTransform.rotation = Quaternion.Euler(0, 0, arrowRotation);
        }
        
        private bool IsTargetVisible(Vector3 targetScreenPos, Camera camera)
        {
            return targetScreenPos.z > 0 &&
                   targetScreenPos.x >= 0 && targetScreenPos.x <= Screen.width &&
                   targetScreenPos.y >= 0 && targetScreenPos.y <= Screen.height;
        }
        
        private Vector2 ClampToScreenEdge(Vector2 screenCenter, Vector2 direction)
        {
            var screenBounds = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var margin = 50f;
            screenBounds -= Vector2.one * margin;
            
            var normalizedDirection = direction.normalized;
            var scaleFactor = Mathf.Min(
                Mathf.Abs(screenBounds.x / normalizedDirection.x),
                Mathf.Abs(screenBounds.y / normalizedDirection.y)
            );
            
            return screenCenter + normalizedDirection * scaleFactor;
        }
        
        private float CalculateArrowRotationToTarget(Vector2 from, Vector2 to)
        {
            var direction = (to - from).normalized;
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }
        
        #endregion
    }
}