namespace Core.Ore
{
    /// <summary>
    /// そのブロックのintIDから、そのブロックの下にある（採掘可能な）oreIdを取得する
    /// </summary>
    public interface ICheckOreMining
    {
        public int Check(int intId);
    }
}