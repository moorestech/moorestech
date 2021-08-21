namespace industrialization.Core.Electric
{
    public interface IBlockElectric
    {
        int RequestPower();
        void SupplyPower(int power);
        uint GetIntId();
    }
}