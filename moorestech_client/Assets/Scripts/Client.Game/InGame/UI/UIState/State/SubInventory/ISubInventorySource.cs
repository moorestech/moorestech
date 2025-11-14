using Client.Game.InGame.UI.Inventory;
using Game.Common.MessagePack;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public interface ISubInventorySource
    {
        /// <summary>
        /// ブロックや列車を共通で扱えるインベントリ識別子
        /// Common inventory identifier that can handle blocks and trains
        /// </summary>
        InventoryIdentifierMessagePack InventoryIdentifier { get; }
        
        string UIPrefabAddressablePath { get; }
        
        
        /// <summary>
        /// インベントリソースの内部で、固有のサブインベントリオブジェクトの初期化を行う
        /// Within the inventory source, initialize the specific sub-inventory object
        /// </summary>
        void ExecuteInitialize(ISubInventoryView subInventoryView);
    }
}
