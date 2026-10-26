using Cysharp.Threading.Tasks;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     testidでUI要素を照会し文字を入れる、描画系に依存しない窓口
    ///     Web版（CEF）を段階0で、UITK版と両系照合の合成を段階1で同じinterfaceに実装する
    ///     Renderer-independent gateway that looks up UI elements by testid and enters text
    ///     Web (CEF) is implemented in stage 0; UITK and the both-renderer composite implement the same interface in stage 1
    /// </summary>
    public interface IUiElementLocator
    {
        // 照会先の描画系がいま応答できるか
        // Whether the backing renderer can answer right now
        bool IsAvailable();

        // 1回だけ照会する。待ち・再試行は呼び出し側が持つ
        // Look up once; waiting and retries belong to the caller
        UniTask<UiElementSnapshot> Find(string testid);

        // テキスト欄の値を置き換えて変更イベントを流す。IMEは通さない
        // Replace a text field's value and raise its change event without IME
        UniTask<UiSetTextResult> SetText(string testid, string text);
    }
}
