using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// 列の枠の間の隙間の印（Web の data-hotbar-row。ここで離すと受けて無操作）
    /// Mark for the gap between slots in the row (the Web data-hotbar-row; releasing here is received and does nothing)
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
