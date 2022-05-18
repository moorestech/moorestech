namespace Core.Electric
{
    public interface IBlockElectric
    {
        //TODO プロパティにする
        int GetRequestPower();
        void SupplyPower(int power);
        public int EntityId { get; }
    }
}