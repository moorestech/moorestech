namespace Core.Electric
{
    public interface IBlockElectric
    {
        int GetRequestPower();
        void SupplyPower(int power);
        int GetEntityId();
    }
}