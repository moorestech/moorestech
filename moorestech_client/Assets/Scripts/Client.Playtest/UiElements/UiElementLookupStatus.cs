namespace Client.Playtest.UiElements
{
    public enum UiElementLookupStatus
    {
        Found,
        NotFound,
        // 同testidが2件以上
        // Two or more elements share the testid
        Ambiguous,
        // パネルが応答しない（GameUiRoot 未生成）
        // The panel did not answer (GameUiRoot not created)
        RendererUnavailable,
    }
}
