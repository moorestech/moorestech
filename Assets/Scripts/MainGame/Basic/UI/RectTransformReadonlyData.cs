using UnityEngine;

namespace MainGame.Basic.UI
{
    /// <summary>
    /// RectTransformのデータは欲しいがアクセスはしたくない時に使う
    /// </summary>
    public class RectTransformReadonlyData
    {
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public readonly Vector3 scale;
        
        public readonly Vector2 pivot;
        public readonly Rect rect;
        public readonly Vector2 anchoredPosition;
        public readonly Vector2 anchorMax;
        public readonly Vector2 anchorMin;
        public readonly Vector2 offsetMax;
        public readonly Vector2 offsetMin;
        public readonly Vector2 sizeDelta;
        public readonly Vector3 anchoredPosition3D;
        

        public RectTransformReadonlyData(RectTransform transform)
        {
            position = transform.position;
            rotation = transform.rotation;
            scale = transform.localScale;
            
            pivot = transform.pivot;
            rect = transform.rect;
            anchoredPosition = transform.anchoredPosition;
            anchorMax = transform.anchorMax;
            anchorMin = transform.anchorMin;
            offsetMax = transform.offsetMax;
            offsetMin = transform.offsetMin;
            sizeDelta = transform.sizeDelta;
            anchoredPosition3D = transform.anchoredPosition3D;
        }
        
        public void SyncRectTransform(RectTransform transform)
        {
            transform.position = position;
            transform.rotation = rotation;
            transform.localScale = scale;
            
            transform.pivot = pivot;
            transform.anchoredPosition = anchoredPosition;
            transform.anchorMax = anchorMax;
            transform.anchorMin = anchorMin;
            transform.offsetMax = offsetMax;
            transform.offsetMin = offsetMin;
            transform.sizeDelta = sizeDelta;
            transform.anchoredPosition3D = anchoredPosition3D;
        }
    }
}