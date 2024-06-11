using Core.Item.Interface.Config;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class ItemShooterConfigParam : IBlockConfigParam
    {
        public readonly float InitialShootSpeed;
        
        public readonly int InventoryItemNum;
        public readonly float ItemShootSpeed;
        
        public readonly float DownAcceleration;
        public readonly float UpDeceleration;
        public readonly float HorizontalDeceleration;
        
        public ItemShooterConfigParam(dynamic blockParam)
        {
            InventoryItemNum = blockParam.inventoryItemNum;
            
            InitialShootSpeed = blockParam.initialShootSpeed;
            ItemShootSpeed = blockParam.itemShootSpeed;
            
            DownAcceleration = blockParam.downAcceleration;
            UpDeceleration = blockParam.upDeceleration;
            HorizontalDeceleration = blockParam.horizontalDeceleration;
        }
        
        public float GetAcceleration(BlockDirection blockDirection)
        {
            switch (blockDirection)
            {
                case BlockDirection.North:
                case BlockDirection.East:
                case BlockDirection.South:
                case BlockDirection.West: // Minus because of deceleration
                    return -HorizontalDeceleration; // 減速なのでマイナス
                case BlockDirection.UpNorth:
                case BlockDirection.UpEast:
                case BlockDirection.UpSouth:
                case BlockDirection.UpWest: // Minus because of deceleration
                    return -UpDeceleration; // 減速なのでマイナス
                case BlockDirection.DownNorth:
                case BlockDirection.DownEast:
                case BlockDirection.DownSouth:
                case BlockDirection.DownWest:
                    return DownAcceleration;
                default:
                    return HorizontalDeceleration;
            }
        }
        
        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            return new ItemShooterConfigParam(blockParam);
        }
    }
}