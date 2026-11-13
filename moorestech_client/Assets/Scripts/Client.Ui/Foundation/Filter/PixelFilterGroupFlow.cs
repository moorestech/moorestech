namespace Client.Ui.Foundation.Filter
{
    /// <summary>
    /// 群の層を置き場の中でどう広げるか
    /// How the group layers spread inside their host
    /// </summary>
    public enum PixelFilterGroupFlow
    {
        // 置き場の箱いっぱいに絶対配置で重ねる（置き場が自分で大きさを持つとき）
        // Absolutely fill the host box (when the host sizes itself)
        Fill,
        // 置き場の流れに入り、中身の大きさで置き場を広げる（置き場の大きさが中身で決まるとき）
        // Join the host flow so the content sizes the host (when the host sizes to its content)
        Flow,
    }
}
