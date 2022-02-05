using MainGame.UnityView.Interface.PlayerInput;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class PlayerInventoryItemMoveTest : IPlayerInventoryItemMove
    {
        public void MoveAllItemStack(int fromSlot, int toSlot)
        {
            Debug.Log("MoveAllItemStack " + fromSlot + " " + toSlot);
        }

        public void MoveHalfItemStack(int fromSlot, int toSlot)
        {
            Debug.Log("MoveHalfItemStack " + fromSlot + " " + toSlot);
        }

        public void MoveOneItemStack(int fromSlot, int toSlot)
        {
            Debug.Log("MoveOneItemStack " + fromSlot + " " + toSlot);
        }
    }
}