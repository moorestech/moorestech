using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// 列の枠間の隙間の印。落とすと無操作
    /// Gap mark in the row; dropping does nothing
    /// </summary>
    public sealed class HotbarRowGapDropMark : IHotbarDropReceiver
    {
        public static readonly HotbarRowGapDropMark Instance = new();

        private HotbarRowGapDropMark()
        {
        }

        // 受けて何もしない（まだ HUD の中なので源は枠外の解除をしない）
        // Receive and do nothing (still inside the HUD, so the source must not clear as outside)
        public void Receive(HotbarDragSource source)
        {
            // silent-ok: 枠の間の隙間で離すのは Web と同じく解除でなく無操作 / HotbarHudDropTest「枠の間の隙間で離すと何も送らない」が固定する
        }
    }
}
