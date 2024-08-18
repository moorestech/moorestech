using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class ItemShooterConfigParam : IBlockConfigParam
    {
        public readonly float InitialShootSpeed;
        
        public readonly int InventoryItemNum;
        public readonly float ItemShootSpeed;
        
        public readonly float Acceleration;
        
        public ItemShooterConfigParam(dynamic blockParam)
        {
            InventoryItemNum = blockParam.inventoryItemNum;
            
            InitialShootSpeed = blockParam.initialShootSpeed;
            ItemShootSpeed = blockParam.itemShootSpeed;
            
            Acceleration = blockParam.acceleration;
        }
        
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            return new ItemShooterConfigParam(blockParam);
        }
    }
}