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
        }

        public void Initialize() { }
    }
}