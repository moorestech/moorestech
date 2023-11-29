using System.Collections.Generic;
using MainGame.UnityView.UI.UIObjects;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory
{
    public class PlayerInventoryController : MonoBehaviour
    {
        [SerializeField] private List<UIBuilderItemSlotObject> mainInventorySlotObjects;
        
        private ISubInventoryController _subInventoryController;
        
        public void SetSubInventory(ISubInventoryController subInventoryController)
        {
            _subInventoryController = subInventoryController;
        }
    }
}