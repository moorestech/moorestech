using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Model.Network.Event;
using MainGame.Network.Util;

namespace MainGame.Network.Receive
{
    /// <summary>
    /// Analysis player inventory data 
    /// </summary>
    public class ReceivePlayerInventoryProtocol : IAnalysisPacket
    {
        private readonly MainInventoryUpdateEvent _mainInventoryUpdateEvent;
        private readonly CraftingInventoryUpdateEvent _craftingInventoryUpdateEvent;
        
        public ReceivePlayerInventoryProtocol(
            IMainInventoryUpdateEvent mainInventoryUpdateEvent,ICraftingInventoryUpdateEvent craftingInventoryUpdateEvent)
        {
            _mainInventoryUpdateEvent = mainInventoryUpdateEvent as MainInventoryUpdateEvent;
            _craftingInventoryUpdateEvent = craftingInventoryUpdateEvent as CraftingInventoryUpdateEvent;
        }


        public void Analysis(List<byte> data)
        {
            var bytes = new ByteArrayEnumerator(data);
            //packet id
            bytes.MoveNextToGetShort();
            //player id
            var playerId = bytes.MoveNextToGetInt();
            //padding
            bytes.MoveNextToGetShort();
            
            //main inventory items
            var mainItems = new List<ItemStack>();
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                var id = bytes.MoveNextToGetInt();
                var count = bytes.MoveNextToGetInt();
                mainItems.Add(new ItemStack(id, count));
            }
            _mainInventoryUpdateEvent.InvokeMainInventoryUpdate(
                new MainInventoryUpdateProperties(
                    playerId,
                    mainItems));
            
            
            //craft inventory items
            var craftItems = new List<ItemStack>();
            for (int i = 0; i < PlayerInventoryConstant.CraftingInventorySize; i++)
            {
                var id = bytes.MoveNextToGetInt();
                var count = bytes.MoveNextToGetInt();
                craftItems.Add(new ItemStack(id, count));
            }
            var resultId = bytes.MoveNextToGetInt();
            var resultCount = bytes.MoveNextToGetInt();
            var resultItem = new ItemStack(resultId, resultCount);
            var canCraft = bytes.MoveNextToGetByte() == 1;
            _craftingInventoryUpdateEvent.InvokeCraftingInventoryUpdate(
                new CraftingInventoryUpdateProperties(playerId,canCraft,craftItems,resultItem));
            
        }
    }


    public class User
    {
        public string UserName;
        public int Age;
        public string ItemId;
    }
}