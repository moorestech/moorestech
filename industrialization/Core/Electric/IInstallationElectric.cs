namespace industrialization.Core.Electric
{
    public interface IInstallationElectric
    {
        int RequestPower();
        void SupplyPower(int power);
    }
}