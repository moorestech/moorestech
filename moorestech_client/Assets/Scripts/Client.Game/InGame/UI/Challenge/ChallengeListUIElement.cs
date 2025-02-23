using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeListUIElement : MonoBehaviour
    {
        private Vector2 AnchoredPosition => rectTransform.anchoredPosition;
        
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private ItemSlotObject itemSlotObject;
        [SerializeField] private RectTransform connectLineParent;
        
        [SerializeField] private GameObject completedObject;
        
        private ChallengeMasterElement _challengeMasterElement;
        
        public void Initialize(ChallengeMasterElement challengeMasterElement)
        {
            _challengeMasterElement = challengeMasterElement;
            var param = (DisplayDisplayListParam)challengeMasterElement.DisplayListParam;
            
            rectTransform.anchoredPosition = param.UIPosition;
            
            if (param.IconItem != null)
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(param.IconItem.Value);
                itemSlotObject.SetItem(itemView, 0);
            }
        }
        
        public void CreateConnect(Dictionary<Guid, ChallengeListUIElement> challengeListUIElements)
        {
            // 線のオブジェクトをオフにしておく
            // Turn off the line object
            connectLineParent.gameObject.SetActive(false);
            
            // 前のチャレンジがある場合、線を引く
            // If there is a previous challenge, draw a line
            var prev = _challengeMasterElement.PrevChallengeGuid;
            if (!prev.HasValue) return;
            if (!challengeListUIElements.TryGetValue(prev.Value, out var prevChallengeListUIElement)) return;
            
            // 線の長さと角度を計算して適用
            // Calculate and apply the length and angle of the line
            var currentPosition = AnchoredPosition;
            var targetPosition = prevChallengeListUIElement.AnchoredPosition;
            
            connectLineParent.gameObject.SetActive(true);
            var distance = Vector2.Distance(currentPosition, targetPosition);
            connectLineParent.sizeDelta = new Vector2(distance, connectLineParent.sizeDelta.y);
            
            var angle = Mathf.Atan2(targetPosition.y - currentPosition.y, targetPosition.x - currentPosition.x) * Mathf.Rad2Deg;
            connectLineParent.localEulerAngles = new Vector3(0, 0, angle);
        }
        
        public void SetStatus(bool isCompleted)
        {
            completedObject.SetActive(isCompleted);
        }
    }
}