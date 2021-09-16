namespace Core.Electric
{
    public interface IBlockElectric
    {
        int RequestPower();
        void SupplyPower(int power);
        int GetIntId();
    }
}