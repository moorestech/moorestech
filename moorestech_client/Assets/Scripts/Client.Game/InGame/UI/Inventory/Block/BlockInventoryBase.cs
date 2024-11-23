using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Sub;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public abstract class BlockInventoryBase : MonoBehaviour
    {
        public abstract void OpenBlockInventoryType(BlockGameObject blockGameObject);
    }
}