namespace Client.Ui.Foundation.Input.OsCursor
{
    /// <summary>
    /// OS カーソルの要求の層。上の層の要求が勝つ（Web の CSS で内側の要素の cursor が地に勝ち、:active のパン中が全体を上書きするのに対応）
    /// Layers of OS cursor requests; a higher layer wins (as Web CSS lets an inner element's cursor beat the surface and :active panning override everything)
    /// </summary>
    public enum UiOsCursorLayer
    {
        // 地（ビューポート等の広い面の掴む手）
        // The surface (the grab hand over a wide area such as a viewport)
        Surface,
        // 地の上の要素（カード等の指）
        // An element on the surface (the pointer over a card and the like)
        Element,
        // ドラッグ中（パン中の握った手）
        // While dragging (the grabbing hand during a pan)
        Drag,
    }
}
