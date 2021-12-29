namespace Core.Electric
{
    public interface IBlockElectric
    {
        int GetRequestPower();
        //TODO 供給した結果余った電力を返す
        void SupplyPower(int power);
        int GetIntId();
    }
}