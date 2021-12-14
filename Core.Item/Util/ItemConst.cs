namespace Core.Item.Util
{
    /// <summary>
    /// アイテムがない時のアイテムIDは0です
    /// 変えることはないと思いますが、万が一変える時はTestアセンブリItemConstも一緒に変更してください
    /// </summary>
    internal static class ItemConst
    {
        //ここの値を変更するときはTestアセンブリのItemConstも変更してください
        public const int NullItemId = 0;
    }
}