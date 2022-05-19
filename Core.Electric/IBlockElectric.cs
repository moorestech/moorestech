namespace Core.Electric
{
    public interface IBlockElectric
    {
        void SupplyPower(int power);
        public int EntityId { get; }
        public int RequestPower { get; }
    }
}