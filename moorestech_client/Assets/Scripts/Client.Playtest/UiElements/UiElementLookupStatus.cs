namespace Client.Playtest.UiElements
{
    public enum UiElementLookupStatus
    {
        Found,
        NotFound,
        // 同じtestidが2件以上ある（UITK版と両系照合が返す）
        // Two or more elements share the testid (returned by UITK and the both-renderer composite)
        Ambiguous,
        // 描画系が応答しない（受け口未登録・応答タイムアウト）
        // The renderer did not answer (unregistered receiver or response timeout)
        RendererUnavailable,
    }
}
