using UnityEngine;

namespace MainGame.Network.Interface.Send
{
    public interface ISendBlockInventoryMoveItemProtocol
    {
        public void Send(Vector2Int position,int fromSlot,int toSlot,int itemCount);
    }
}