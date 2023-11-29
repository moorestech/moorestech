using UnityEngine;

namespace MainGame.UnityView.UI.UIState.UIObject
{
    public class BlockInventoryObject : MonoBehaviour
    {
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
        
        public void SetBlockInventoryType(BlockInventoryType type)
        {
            Debug.LogError("BlockInventoryObjectを実装していません！");
            switch (type)
            {
                case BlockInventoryType.Chest:
                    break;
                case BlockInventoryType.Miner:
                    break;
                case BlockInventoryType.Machine:
                    break;
                case BlockInventoryType.Generator:
                    break;
            }
        }
    }

    public enum BlockInventoryType
    {
        Chest,
        Miner,
        Machine,
        Generator,
    }
}