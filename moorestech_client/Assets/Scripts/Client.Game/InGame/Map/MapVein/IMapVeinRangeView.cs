namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     鉱脈範囲表示の窓口。設置側は表示したい状態の変化と毎フレームの駆動だけを渡す
    ///     Entry point of the vein range view; the placement side pushes the wanted display state and per-frame ticks only
    /// </summary>
    public interface IMapVeinRangeView
    {
        // 表示状態の変化時にだけ呼ぶ。優先規則はVeinDisplayの作り方が持つ
        // Called only when the display state changes; VeinDisplay's construction owns the precedence rule
        void SetVeinDisplay(VeinDisplay display);

        // カメラ追従の距離カリング用。表示中のフレーム駆動
        // Per-frame tick for camera-following distance culling while visible
        void ManualUpdate();
    }
}
