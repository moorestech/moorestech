using System.Collections.Generic;
using Core.Item;

namespace PlayerInventory
{
    public class PlayerInventory
    {
        public readonly int PlayerId;
        private readonly List<IItemStack> MainInventory;

        public PlayerInventory(int playerId)
        {
            PlayerId = playerId;
            MainInventory = new List<IItemStack>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                MainInventory.Add(new NullItemStack());
            }
        }
    }
}