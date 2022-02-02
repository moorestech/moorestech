using MainGame.UnityView.ControllerInput;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using VContainer;
using VContainer.Unity;

namespace MainGame.UnityView.UI.Inventory.Control
{
    public class MouseInventoryInput : IControllerInput,IPostStartable
    {
        private MainInventoryItemView _mainInventoryItemView;
        
        [Inject]
        public void Construct(MainInventoryItemView mainInventoryItemView) { _mainInventoryItemView = mainInventoryItemView; }
        
        public void PostStart()
        {
            foreach (var slot in _mainInventoryItemView.GetInventoryItemSlots())
            {
                slot.SubscribeOnItemSlotClick(OnSlotClick);
            }
        }

        private void OnSlotClick(int slot)
        {
            
        }
        
        public void OnInput()
        {
            throw new System.NotImplementedException();
        }

        public void OffInput()
        {
            throw new System.NotImplementedException();
        }

    }
}