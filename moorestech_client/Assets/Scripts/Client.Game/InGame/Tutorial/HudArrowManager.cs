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
        private static HudArrowManager Instance { get; set; }
        
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
        
        public static void RegisterHudArrowTarget(GameObject target, HudArrowOptions options = default)
        {
            if (!Instance) return;
            
            var arrow = Instantiate(Instance.hudArrowImagePrefab, Instance.transform);
            arrow.Initialize(target, options);
            Instance._hudArrows[target] = arrow;
        }
        
        public static void UnregisterHudArrowTarget(GameObject target)
        {
            if (!Instance) return;

            if (Instance._hudArrows.TryGetValue(target, out var arrowTransform))
            {
                if (arrowTransform != null)
                    Destroy(arrowTransform.gameObject);
                Instance._hudArrows.Remove(target);
            }
        }
        
        private void UpdateArrowTransform(GameObject target, HudArrow arrow)
        {
            if (arrow == null || target == null)
                return;
            
            var currentCamera = CameraManager.MainCamera.Camera;
            if (!currentCamera)
                return;

            // 射影計算はWorldPinScreenProjectionに集約（Webピンと同一の座標源）
            // Projection math is centralized in WorldPinScreenProjection (same source as web pins)
            var targetWorldPos = target.transform.position;
            var projection = WorldPinScreenProjection.Project(currentCamera, targetWorldPos);

            if (projection.OnScreen) OnScreenProcess();
            else OffScreenProcess();

            return;


            #region Internal

            void OnScreenProcess()
            {
                // 画面内の場合

                // Canvas のサイズ
                var canvasSize = _canvasRect.rect.size;

                // CSS座標（左上原点）を中央原点・上正のCanvas座標に変換
                // Convert CSS coords (top-left origin) into center-origin, +Y-up canvas coords
                var targetCanvasPos = new Vector2(
                    (projection.ScreenX - 0.5f) * canvasSize.x,
                    (0.5f - projection.ScreenY) * canvasSize.y
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

                // CSS方向（+Y下）を上正のCanvas方向へ反転
                // Flip the CSS direction (+Y down) into +Y-up canvas space
                var direction = new Vector2(projection.DirectionX, -projection.DirectionY);
                
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
        
        public static void SetActive(bool enable)
        {
            if (Instance == null) return;
            Instance.gameObject.SetActive(enable);
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