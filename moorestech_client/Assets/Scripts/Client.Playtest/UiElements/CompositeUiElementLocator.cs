using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.UiElements
{
    /// <summary>
    /// GameUi を先に探し、無ければ Web を使う両系合成。同じ testid が両系で見つかったら Ambiguous で失敗させる
    /// Both-renderer composite that prefers GameUi and falls back to Web; a testid found in both fails as Ambiguous
    /// </summary>
    internal sealed class CompositeUiElementLocator : IUiElementLocator
    {
        private readonly IUiElementLocator _gameUi;
        private readonly IUiElementLocator _web;

        public CompositeUiElementLocator(IUiElementLocator gameUi, IUiElementLocator web)
        {
            _gameUi = gameUi;
            _web = web;
        }

        public void StartSession()
        {
            _gameUi.StartSession();
            _web.StartSession();
        }

        public void StopSession()
        {
            _gameUi.StopSession();
            _web.StopSession();
        }

        public bool IsAvailable()
        {
            return _gameUi.IsAvailable() || _web.IsAvailable();
        }

        public async UniTask<UiElementSnapshot> Find(string testid, float remainingSeconds)
        {
            // 重複を見落とさないよう、GameUi で見つかっても Web まで照会する
            // Query Web even after a GameUi hit so duplicates across renderers are not missed
            var gameUi = await _gameUi.Find(testid, remainingSeconds);
            var web = await _web.Find(testid, remainingSeconds);

            if (gameUi.IsFound && web.IsFound)
            {
                Debug.LogError($"[PlaytestUi] testid '{testid}' resolved in both game UI and web UI");
                return UiElementSnapshot.Ambiguous(testid);
            }
            // 片系の重複は他系の Found より先に返し、取り違えを防ぐ
            // Return a duplicate on either side before the other side's hit to avoid picking the wrong element
            if (gameUi.Status == UiElementLookupStatus.Ambiguous) return gameUi;
            if (web.Status == UiElementLookupStatus.Ambiguous) return web;
            if (gameUi.IsFound) return gameUi;
            if (web.IsFound) return web;

            // どちらにも無ければ、GameUi が応答できる限りその NotFound を返す
            // When neither has it, return GameUi's NotFound as long as GameUi can answer
            return gameUi.Status == UiElementLookupStatus.RendererUnavailable ? web : gameUi;
        }

        public async UniTask<UiSetTextResult> SetText(string testid, string text, float remainingSeconds)
        {
            // GameUi で該当要素がある（入れた・拒んだ）ならその結果、無ければ Web に入れる
            // Use GameUi's result when it has the element (applied or refused), otherwise set it on Web
            var gameUi = await _gameUi.SetText(testid, text, remainingSeconds);
            if (gameUi.Status != UiSetTextStatus.NotFound && gameUi.Status != UiSetTextStatus.RendererUnavailable) return gameUi;
            return await _web.SetText(testid, text, remainingSeconds);
        }
    }
}
