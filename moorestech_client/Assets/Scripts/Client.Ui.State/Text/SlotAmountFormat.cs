using System.Globalization;

namespace Client.Ui.State.Text
{
    /// <summary>
    /// スロットの量バッジの表記を 1 つに揃える。整数は千区切り、小数は 2 桁まで（Web slotAmountFormat.ts の写し）
    /// The single spelling for slot amount badges: thousands separators, fractions up to 2 digits (port of the Web slotAmountFormat.ts)
    /// </summary>
    public static class SlotAmountFormat
    {
        // レシピの液体量は小数を取り、実行時のタンク残量は流量加算の double が来るので桁を有限で止める
        // Recipe fluid amounts are fractional and runtime tank amounts are flow-accumulated doubles, so cap the digits
        public static string Format(double amount)
        {
            return amount.ToString("#,0.##", CultureInfo.InvariantCulture);
        }
    }
}
