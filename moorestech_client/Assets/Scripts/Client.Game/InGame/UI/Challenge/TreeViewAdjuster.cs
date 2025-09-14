using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public static class TreeViewAdjuster
    {
        public static void AdjustParentSize(RectTransform resizeTarget, RectTransform offsetTarget, IEnumerable<ITreeViewElement> treeViewElements)
        {
            // 全要素の境界を計算
            var bounds = CalculateElementsBounds();
            
            // パディングを追加
            const float padding = 200f;
            var size = new Vector2(
                bounds.width + padding * 2,
                bounds.height + padding * 2
            );
            
            // 親のサイズを更新
            resizeTarget.sizeDelta = size;
            
            // アンカーポイントを中心に設定
            resizeTarget.pivot = new Vector2(0.5f, 0.5f);
            resizeTarget.anchorMin = new Vector2(0.5f, 0.5f);
            resizeTarget.anchorMax = new Vector2(0.5f, 0.5f);
            
            // 中心がズレるので要素をオフセットする
            // すべてのコンテンツの中心座標を計算し、その中心座標と本来の中心座標（サイズの半分の位置）のずれを計算し、そのずれをオフセットする
            var offset =  new Vector2(size.x * 0.5f, size.y * 0.5f) - bounds.center;
            offsetTarget.anchoredPosition = offset;

            
            
            #region Internal
            
            Rect CalculateElementsBounds()
            {
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;
                
                foreach (var element in treeViewElements)
                {
                    var pos = element.RectTransform.anchoredPosition;
                    var rectTransform = element.RectTransform;
                    var halfSize = rectTransform.sizeDelta * 0.5f;
                    
                    // 要素の四隅を考慮
                    minX = Mathf.Min(minX, pos.x - halfSize.x);
                    minY = Mathf.Min(minY, pos.y - halfSize.y);
                    maxX = Mathf.Max(maxX, pos.x + halfSize.x);
                    maxY = Mathf.Max(maxY, pos.y + halfSize.y);
                }
                
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            
            #endregion
        }
    }
}