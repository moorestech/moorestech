namespace Game.Block.Blocks.ItemShooter
{
    // シューター連携のための共通契約 // Shared contract for shooter-to-shooter interactions
    public interface IItemShooterComponent
    {
        ShooterInventoryItem InsertItemFromShooter(ShooterInventoryItem inventoryItem);
        void SetExternalAcceleration(float acceleration);
    }
}
