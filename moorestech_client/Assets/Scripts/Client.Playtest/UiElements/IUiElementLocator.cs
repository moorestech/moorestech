using Cysharp.Threading.Tasks;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     ・描画系非依存の窓口
    ///     ・testid照会と文字入力
    ///     ・段階0はWeb版
    ///     ・段階1はUITK版と両系合成
    ///     ・Renderer-independent gateway
    ///     ・Lookup by testid and text input
    ///     ・Stage 0 implements Web
    ///     ・Stage 1 implements UITK and the both-renderer composite
    /// </summary>
    public interface IUiElementLocator
    {
        // 照会の受け口と入力転送を開く。冪等
        // Open the query receiver and input forwarding; idempotent
        void StartSession();

        // 転送を止め、未完了の照会を次の実行へ持ち越さない
        // Stop forwarding and keep pending lookups out of the next run
        void StopSession();

        // 照会先の描画系がいま応答できるか
        // Whether the backing renderer can answer right now
        bool IsAvailable();

        // 1回だけ照会する。待ち・再試行は呼び出し側が持つ
        // Look up once; waiting and retries belong to the caller
        UniTask<UiElementSnapshot> Find(string testid, float remainingSeconds);

        // 値を置換し変更イベント発火。IME非経由
        // Replace the value and fire change events, bypassing IME
        UniTask<UiSetTextResult> SetText(string testid, string text, float remainingSeconds);
    }
}
