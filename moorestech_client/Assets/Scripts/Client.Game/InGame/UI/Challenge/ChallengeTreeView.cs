using System;
using System.Collections.Generic;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeTreeView : MonoBehaviour
    {
        [SerializeField] private ChallengeTreeViewElement categoryElement;
        
        [SerializeField] private Transform challengeListParent;
        [SerializeField] private Transform connectLineParent; // 線は一番下に表示される必要があるため専用の親に格納する

        [SerializeField] private RectTransform resizeTarget;
        [SerializeField] private RectTransform offsetTarget;
        
        private readonly Dictionary<Guid, ChallengeTreeViewElement> _challengeElementsDictionary = new();
        
        public void SetChallengeCategory(ChallengeCategoryMasterElement category)
        {
            // 既存の要素をクリア
            ClearChallengeElements();
            
            // 新しいチャレンジ要素を作成
            foreach (var challenge in category.Challenges)
            {
                var challengeElement = Instantiate(categoryElement, challengeListParent);
                challengeElement.SetChallenge(challenge);
                
                _challengeElementsDictionary.Add(challenge.ChallengeGuid, challengeElement);
            }
            
            // 接続線を作成
            foreach (var challengeElement in _challengeElementsDictionary.Values)
            {
                challengeElement.CreateConnect(connectLineParent, _challengeElementsDictionary);
            }
            
            // 全要素を包含するように親のサイズを調整
            AdjustParentSize();
        }
        
        private void ClearChallengeElements()
        {
            foreach (var element in _challengeElementsDictionary.Values)
            {
                if (element != null)
                {
                    Destroy(element.gameObject);
                }
            }
            _challengeElementsDictionary.Clear();
        }
        
        private void AdjustParentSize()
        {
            if (_challengeElementsDictionary.Count == 0) return;
            
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
                
                foreach (var element in _challengeElementsDictionary.Values)
                {
                    var pos = element.AnchoredPosition;
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