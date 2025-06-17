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
            Debug.Log($"[HudArrow] Canvas Anchor: {canvasRect.anchorMin} - {canvasRect.anchorMax}");
            Debug.Log($"[HudArrow] Canvas Pivot: {canvasRect.pivot}");
            
            Vector2 arrowPosition;
            float arrowRotation;
            
            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            
            if (isTargetVisible)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, targetScreenPos, camera, out var targetLocalPos);
                
                var screenCenterLocal = Vector2.zero;
                var directionToTarget = (targetLocalPos - screenCenterLocal).normalized;
                var distanceToTarget = Vector2.Distance(targetLocalPos, screenCenterLocal);
                
                var arrowDistance = Mathf.Min(distanceToTarget * 0.8f, 150f);
                arrowPosition = screenCenterLocal + directionToTarget * arrowDistance;
                arrowRotation = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;
                
                Debug.Log($"[HudArrow] Visible - TargetLocalPos: {targetLocalPos}");
                Debug.Log($"[HudArrow] Visible - Direction: {directionToTarget}, Distance: {distanceToTarget}");
                Debug.Log($"[HudArrow] Visible - ArrowDistance: {arrowDistance}, ArrowPos: {arrowPosition}, Rotation: {arrowRotation}");
            }
            else
            {
                Vector2 directionToTarget;
                
                if (targetScreenPos.z < 0)
                {
                    var cameraToTarget = (targetWorldPos - camera.transform.position).normalized;
                    var cameraForward = camera.transform.forward;
                    var cameraRight = camera.transform.right;
                    var cameraUp = camera.transform.up;
                    
                    var screenX = Vector3.Dot(cameraToTarget, cameraRight);
                    var screenY = Vector3.Dot(cameraToTarget, cameraUp);
                    
                    directionToTarget = new Vector2(-screenX, -screenY).normalized;
                    
                    Debug.Log($"[HudArrow] Behind Camera - CameraToTarget: {cameraToTarget}, ScreenDir: {directionToTarget}");
                }
                else
                {
                    directionToTarget = (new Vector2(targetScreenPos.x, targetScreenPos.y) - screenCenter).normalized;
                }
                
                var canvasHalfWidth = canvasRect.rect.width * 0.5f;
                var canvasHalfHeight = canvasRect.rect.height * 0.5f;
                var margin = 150f;
                
                var maxX = canvasHalfWidth - margin;
                var maxY = canvasHalfHeight - margin;
                
                var scaleX = Mathf.Abs(directionToTarget.x) > 0.001f ? maxX / Mathf.Abs(directionToTarget.x) : float.MaxValue;
                var scaleY = Mathf.Abs(directionToTarget.y) > 0.001f ? maxY / Mathf.Abs(directionToTarget.y) : float.MaxValue;
                var scale = Mathf.Min(scaleX, scaleY);
                
                arrowPosition = directionToTarget * scale;
                arrowRotation = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;
                
                Debug.Log($"[HudArrow] Hidden - Direction: {directionToTarget}");
                Debug.Log($"[HudArrow] Hidden - CanvasSize: {canvasRect.rect.width}x{canvasRect.rect.height}");
                Debug.Log($"[HudArrow] Hidden - MaxX: {maxX}, MaxY: {maxY}, Scale: {scale}");
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
            var margin = 100f;
            screenBounds -= Vector2.one * margin;
            
            var normalizedDirection = direction.normalized;
            
            if (Mathf.Abs(normalizedDirection.x) < 0.001f && Mathf.Abs(normalizedDirection.y) < 0.001f)
                return screenCenter;
            
            var scaleX = Mathf.Abs(normalizedDirection.x) > 0.001f ? screenBounds.x / Mathf.Abs(normalizedDirection.x) : float.MaxValue;
            var scaleY = Mathf.Abs(normalizedDirection.y) > 0.001f ? screenBounds.y / Mathf.Abs(normalizedDirection.y) : float.MaxValue;
            var scaleFactor = Mathf.Min(scaleX, scaleY);
            
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