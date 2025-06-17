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
            
            var isOnScreen = viewportPos.z > 0 && 
                             viewportPos.x >= 0 && viewportPos.x <= 1 && 
                             viewportPos.y >= 0 && viewportPos.y <= 1;
            
            Vector2 canvasSize = canvasRect.rect.size;
            Vector2 canvasCenter = Vector2.zero;
            
            Vector2 arrowPosition;
            float arrowRotation;
            
            if (isOnScreen)
            {
                var screenPos = new Vector2(
                    (viewportPos.x - 0.5f) * canvasSize.x,
                    (viewportPos.y - 0.5f) * canvasSize.y
                );
                
                arrowPosition = screenPos;
                var direction = screenPos.normalized;
                arrowRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            }
            else
            {
                Vector2 screenPos;
                
                if (viewportPos.z < 0)
                {
                    viewportPos.x = 1f - viewportPos.x;
                    viewportPos.y = 1f - viewportPos.y;
                }
                
                screenPos = new Vector2(
                    (viewportPos.x - 0.5f) * canvasSize.x,
                    (viewportPos.y - 0.5f) * canvasSize.y
                );
                
                var direction = screenPos.normalized;
                
                var margin = 100f;
                var maxX = (canvasSize.x * 0.5f) - margin;
                var maxY = (canvasSize.y * 0.5f) - margin;
                
                if (Mathf.Abs(direction.x) < 0.001f && Mathf.Abs(direction.y) < 0.001f)
                {
                    direction = Vector2.right;
                }
                
                var scaleX = Mathf.Abs(direction.x) > 0.001f ? maxX / Mathf.Abs(direction.x) : float.MaxValue;
                var scaleY = Mathf.Abs(direction.y) > 0.001f ? maxY / Mathf.Abs(direction.y) : float.MaxValue;
                var scale = Mathf.Min(scaleX, scaleY);
                
                arrowPosition = direction * scale;
                arrowRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            }
            
            arrowTransform.anchoredPosition = arrowPosition;
            arrowTransform.rotation = Quaternion.Euler(0, 0, arrowRotation);
        }
        
        #endregion
    }
}