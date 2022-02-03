using System;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using UnityEngine.Serialization;

namespace Test.TestModule.UITestModule
{
    public class InventoryTestModule : MonoBehaviour
    {
        [SerializeField] private MainInventoryItemView mainInventoryItemView;
        [SerializeField] private ItemImages itemImages;

        private void Start()
        {
            mainInventoryItemView.Construct(itemImages);
        }
    }
}