using Client.Playtest.UiElements;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.Dsl
{
    /// <summary>
    /// 決めた照会結果を返し、呼ばれた回数を数える偽のロケータ
    /// Fake locator that returns preset results and counts the calls it receives
    /// </summary>
    internal sealed class FakeUiElementLocator : IUiElementLocator
    {
        private readonly UiElementLookupStatus _findStatus;
        private readonly UiSetTextStatus _setTextStatus;

        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int SetTextCount { get; private set; }
        public int FindCount { get; private set; }
        public UiElementSnapshot LastFound { get; private set; }
        public float LastFindBudgetSeconds { get; private set; }

        public FakeUiElementLocator(UiElementLookupStatus findStatus, UiSetTextStatus setTextStatus)
        {
            _findStatus = findStatus;
            _setTextStatus = setTextStatus;
        }

        public void StartSession()
        {
            StartCount++;
        }

        public void StopSession()
        {
            StopCount++;
        }

        public UniTask<UiElementSnapshot> Find(string testid, float remainingSeconds)
        {
            FindCount++;
            LastFindBudgetSeconds = remainingSeconds;
            LastFound = _findStatus switch
            {
                UiElementLookupStatus.Found => UiElementSnapshot.Found(testid, true, true, default, "fake", new string[0]),
                UiElementLookupStatus.Ambiguous => UiElementSnapshot.Ambiguous(testid),
                UiElementLookupStatus.RendererUnavailable => UiElementSnapshot.Unavailable(testid),
                _ => UiElementSnapshot.NotFound(testid),
            };
            return UniTask.FromResult(LastFound);
        }

        public UniTask<UiSetTextResult> SetText(string testid, string text, float remainingSeconds)
        {
            SetTextCount++;
            return UniTask.FromResult(new UiSetTextResult(_setTextStatus, text));
        }
    }
}
