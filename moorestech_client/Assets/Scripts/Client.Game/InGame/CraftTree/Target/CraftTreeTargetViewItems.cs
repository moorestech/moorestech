using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Game.CraftTree.Models;
using Game.CraftTree.Models;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.Target
{
    public class CraftTreeTargetViewItems : MonoBehaviour
    {
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private ItemSlotView itemSlotView;
        
        [SerializeField] private RectTransform offsetUiTransform;
        [SerializeField] private float depthWidth = 50f;
        
        
        public CraftTreeNode Node { get; private set; }
        
        
        public void Initialize(CraftTreeNode node, int depth)
        {
            Node = node;
            
            SetItem();
            SetPosition();
            
            #region Internal
            
            void SetItem()
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(node.TargetItemId);
                itemNameText.text = $"{itemView.ItemName}  {node.CurrentCount} / {node.RequiredCount}";
                itemSlotView.SetItem(itemView, 0);
            }
            
            void SetPosition()
            {
                var position = offsetUiTransform.anchoredPosition;
                position.x = depth * depthWidth;
                offsetUiTransform.anchoredPosition = position;
            }
            
            #endregion
        }
    }
}