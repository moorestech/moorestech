using MainGame.Network.Send;
using MainGame.UnityView.UI.UIState.UIObject;
using VContainer.Unity;

namespace MainGame.Presenter.Inventory.Send
{
    public class CraftPacketSend : IInitializable
    {
        private readonly SendCraftProtocol _sendCraftProtocol;
        private readonly MoorestechInputSettings _inputSettings;

        public CraftPacketSend(PlayerInventoryObject playerInventoryObject,SendCraftProtocol sendCraftProtocol,MoorestechInputSettings inputSettings)
        {
            _sendCraftProtocol = sendCraftProtocol;
            _inputSettings = inputSettings;
            playerInventoryObject.OnResultSlotClick += OnCraft;
        }

        private void OnCraft()
        {
            if (_inputSettings.UI.AllCraft.IsPressed())
            {
                _sendCraftProtocol.SendAllCraft();
                return;
            }

            if (_inputSettings.UI.OneStackCraft.IsPressed())
            {
                _sendCraftProtocol.SendOneStackCraft();
                return;
            }
            
            _sendCraftProtocol.SendOneCraft();
        }

        public void Initialize() { }
    }
}