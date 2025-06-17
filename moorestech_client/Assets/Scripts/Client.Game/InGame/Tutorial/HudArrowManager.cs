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
            
            Vector2 targetLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, targetScreenPos, camera, out targetLocalPos);
            
            var isTargetVisible = IsTargetVisible(targetScreenPos, camera);
            
            Vector2 arrowPosition;
            float arrowRotation;
            
            if (isTargetVisible)
            {
                arrowPosition = targetLocalPos;
                var direction = targetLocalPos.normalized;
                arrowRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            }
            else
            {
                Vector2 directionFromCenter;
                
                if (targetScreenPos.z < 0)
                {
                    directionFromCenter = -targetLocalPos.normalized;
                }
                else
                {
                    directionFromCenter = targetLocalPos.normalized;
                }
                
                var canvasHalfWidth = canvasRect.rect.width * 0.5f;
                var canvasHalfHeight = canvasRect.rect.height * 0.5f;
                var margin = 100f;
                
                var maxX = canvasHalfWidth - margin;
                var maxY = canvasHalfHeight - margin;
                
                var scaleX = Mathf.Abs(directionFromCenter.x) > 0.001f ? maxX / Mathf.Abs(directionFromCenter.x) : float.MaxValue;
                var scaleY = Mathf.Abs(directionFromCenter.y) > 0.001f ? maxY / Mathf.Abs(directionFromCenter.y) : float.MaxValue;
                var scale = Mathf.Min(scaleX, scaleY);
                
                arrowPosition = directionFromCenter * scale;
                arrowRotation = Mathf.Atan2(directionFromCenter.y, directionFromCenter.x) * Mathf.Rad2Deg;
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
        
        
        #endregion
    }
}