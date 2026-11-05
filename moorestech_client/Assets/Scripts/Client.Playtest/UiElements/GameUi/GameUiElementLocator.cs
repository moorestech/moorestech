using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;

namespace Client.Playtest.UiElements.GameUi
{
    /// <summary>
    /// GameUiRoot のパネルを照会する UITK 版ロケータ。照会はプロセス内で同期に終わる
    /// UITK locator over the GameUiRoot panel; lookups finish synchronously inside the process
    /// </summary>
    /// <remarks>
    /// 拡張点: 照会・可視・被覆・文字入力の判定を足すときは GameUiElementQuery に足す（パネルを引数で受ける純粋な関数のまま保ち、L1 で UiTestHost のパネルに対して試す）。座標の換算は GameUiScreenMapper が持つ
    /// Extension: add lookup, visibility, cover or text-input rules to GameUiElementQuery (keep it a pure function over a passed panel root, tested at L1 against the UiTestHost panel); coordinate mapping lives in GameUiScreenMapper
    /// </remarks>
    internal sealed class GameUiElementLocator : IUiElementLocator
    {
        // UITK はプロセス内なので WS の受け口も入力転送も要らない
        // UITK lives in-process, so there is no WS receiver or input forwarding to open
        public void StartSession()
        {
        }

        public void StopSession()
        {
        }

        private static bool IsAvailable()
        {
            return GameUiRoot.Instance != null;
        }

        public UniTask<UiElementSnapshot> Find(string testid, float remainingSeconds)
        {
            if (!IsAvailable()) return UniTask.FromResult(UiElementSnapshot.Unavailable(testid));
            var root = GameUiRoot.Instance.GetRootElement();
            return UniTask.FromResult(GameUiElementQuery.Find(root, testid, new GameUiScreenMapper(root)));
        }

        public UniTask<UiSetTextResult> SetText(string testid, string text, float remainingSeconds)
        {
            if (!IsAvailable()) return UniTask.FromResult(new UiSetTextResult(UiSetTextStatus.RendererUnavailable, string.Empty));
            return UniTask.FromResult(GameUiElementQuery.SetText(GameUiRoot.Instance.GetRootElement(), testid, text));
        }
    }
}
