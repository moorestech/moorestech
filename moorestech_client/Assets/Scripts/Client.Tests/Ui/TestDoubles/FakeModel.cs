using UniRx;

namespace Client.Tests.Ui.TestDoubles
{
    /// <summary>
    /// 画面の View を本物のサーバー無しで試すための偽モデルの基底。テストが Set で表示状態を差し込む
    /// Base of a fake model for testing a screen view without a real server; the test pushes display state with Set
    /// </summary>
    /// <remarks>
    /// 画面レーンの作り方: View が購読する ViewModel を T にして FakeXxxModel : FakeModel&lt;XxxViewModel&gt; を作り、View には Current を渡す。
    /// Playwright の e2e で setUiState(...) と書いていた箇所は、ここの Set(...) に置き換える（Set の直後に simulate.FrameUpdate() で描き直させる）。
    /// 拡張点: 1 画面で複数の独立した値を流すなら、ViewModel を 1 つの不変な型にまとめて T にする（偽モデルを複数並べない）。最小の例は Samples/ にある。
    /// How a screen lane uses it: make FakeXxxModel : FakeModel&lt;XxxViewModel&gt; with the view model the view subscribes to as T, and hand Current to the view.
    /// Where a Playwright e2e wrote setUiState(...), call Set(...) here instead (then simulate.FrameUpdate() so the view redraws).
    /// Extension: when one screen streams several independent values, gather them into one immutable view model type as T rather than lining up several fakes; the minimal example lives in Samples/.
    /// </remarks>
    public abstract class FakeModel<T>
    {
        private readonly ReactiveProperty<T> _current;

        protected FakeModel(T initialValue)
        {
            _current = new ReactiveProperty<T>(initialValue);
        }

        public IReadOnlyReactiveProperty<T> Current => _current;

        // 本番のモデルと同じく同値の再設定は流さない
        // Like the production models, re-setting the same value does not publish
        public void Set(T value)
        {
            _current.Value = value;
        }
    }
}
