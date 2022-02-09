using MainGame.UnityView.Interface.PlayerInput;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class BlockInventoryItemMoveTest : IBlockInventoryItemMove
    {
        public void MoveAllItemStack(int fromSlot, int toSlot, bool toBlock)
        {
            Debug.Log("from:" + fromSlot + " to:" + toSlot + " toBlock:" + toBlock);
        }

        public void MoveHalfItemStack(int fromSlot, int toSlot, bool toBlock)
        {
            Debug.Log("from:" + fromSlot + " to:" + toSlot + " toBlock:" + toBlock);
        }

        public void MoveOneItemStack(int fromSlot, int toSlot, bool toBlock)
        {
            Debug.Log("from:" + fromSlot + " to:" + toSlot + " toBlock:" + toBlock);
        }
    }
}