namespace Game.Block.Blocks.ItemShooter
{
    public interface IItemShooterComponent
    {
        ShooterInventoryItem InsertItemFromShooter(ShooterInventoryItem inventoryItem);
        void SetExternalAcceleration(float acceleration);
    }
}
