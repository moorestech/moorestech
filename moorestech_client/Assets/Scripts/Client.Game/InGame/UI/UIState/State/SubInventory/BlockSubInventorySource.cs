using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Block;
using Game.Common.MessagePack;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public class BlockSubInventorySource : ISubInventorySource
    {
        public InventoryIdentifierMessagePack InventoryIdentifier { get; }
        public string UIPrefabAddressablePath => _blockGameObject.BlockMasterElement.BlockUIAddressablesPath;
        
        private readonly BlockGameObject _blockGameObject;
        
        public BlockSubInventorySource(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateBlockMessage(blockGameObject.BlockPosInfo.OriginalPos);
        }
        
        public void ExecuteInitialize(ISubInventoryView subInventoryView)
        {
            ((IBlockInventoryView) subInventoryView).Initialize(_blockGameObject);
        }
    }
    
}
