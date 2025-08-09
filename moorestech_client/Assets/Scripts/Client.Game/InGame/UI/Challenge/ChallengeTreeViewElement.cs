using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Mooresmaster.Model.ChallengesModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeTreeViewElement : MonoBehaviour
    {
        public Vector2 AnchoredPosition => rectTransform.anchoredPosition;
        public ChallengeMasterElement ChallengeMasterElement { get; private set; }
        public RectTransform RectTransform => rectTransform;
        
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private RectTransform connectLinePrefab;
        
        [SerializeField] private ItemSlotView itemSlotView;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text summary;
        
        
        // 生成された接続線のリスト
        private readonly List<RectTransform> _connectLines = new();
        
        public void SetChallenge(ChallengeMasterElement challengeMasterElement, ChallengeListUIElementState currentState)
        {
            ChallengeMasterElement = challengeMasterElement;
            rectTransform.anchoredPosition = challengeMasterElement.DisplayListParam.UIPosition;
            rectTransform.localScale = challengeMasterElement.DisplayListParam.UIScale;
            
            var itemView = ClientContext.ItemImageContainer.GetItemView(challengeMasterElement.DisplayListParam.IconItem);
            itemSlotView.SetItem(itemView, 0);
            itemSlotView.SetSlotViewOption(new CommonSlotViewOption
            {
                IsShowToolTip = false,
            });
            
            title.text = challengeMasterElement.Title;
            summary.text = challengeMasterElement.Summary;
        }
        
        public void CreateConnect(Transform lineParent, Dictionary<Guid, ChallengeTreeViewElement> challengeElements)
        {
            // 既存の接続線をクリア
            ClearConnectLines();
            
            // 前のチャレンジがある場合、線を引く
            var prevGuids = ChallengeMasterElement.PrevChallengeGuids;
            if (prevGuids == null) return;
            
            foreach (var prev in prevGuids)
            {
                if (!challengeElements.TryGetValue(prev, out var prevChallengeElement)) continue;
                
                // 線を引く
                CreateLine(prevChallengeElement, lineParent);
            }
            
            #region Internal
            
            void CreateLine(ChallengeTreeViewElement prevChallengeElement, Transform parent)
            {
                // 線の長さと角度を計算して適用
                var currentPosition = AnchoredPosition;
                var targetPosition = prevChallengeElement.AnchoredPosition;
                
                // 接続線を取得または作成
                var connectLine = Instantiate(connectLinePrefab, transform);
                connectLine.gameObject.SetActive(true);
                
                var distance = Vector2.Distance(currentPosition, targetPosition);
                connectLine.sizeDelta = new Vector2(distance, connectLine.sizeDelta.y);
                
                var angle = Mathf.Atan2(targetPosition.y - currentPosition.y, targetPosition.x - currentPosition.x) * Mathf.Rad2Deg;
                connectLine.localEulerAngles = new Vector3(0, 0, angle);
                
                // 親の位置を変更
                connectLine.SetParent(parent);
                
                // 親によってスケールが変わっている可能性があるので戻す
                connectLine.localScale = Vector3.one;
                
                // 接続線リストに追加
                _connectLines.Add(connectLine);
            }
            
            #endregion
        }
        
        private void OnDestroy()
        {
            // 生成された接続線を削除
            ClearConnectLines();
        }
        
        private void ClearConnectLines()
        {
            // 生成された接続線を削除
            foreach (var line in _connectLines)
            {
                if (line != null)
                {
                    Destroy(line.gameObject);
                }
            }
            
            _connectLines.Clear();
        }
    }
    
    public enum ChallengeListUIElementState
    {
        Before,
        Current,
        Completed,
        Locked,
    }
}