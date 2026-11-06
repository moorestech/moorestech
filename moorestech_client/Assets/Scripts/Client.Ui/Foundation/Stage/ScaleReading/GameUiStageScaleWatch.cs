using Client.Ui.Foundation.Root;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Foundation.Stage.ScaleReading
{
    /// <summary>
    /// GameUi のルートで倍率の見張りを始める（DI の入口。StageScaleSource はこの Scale を配る）
    /// Starts the scale watch on the GameUi root (DI entry; StageScaleSource hands out this Scale)
    /// </summary>
    public sealed class GameUiStageScaleWatch : IInitializable
    {
        private readonly StageScaleWatch _watch;

        public IReadOnlyReactiveProperty<float> Scale => _watch.Scale;

        public GameUiStageScaleWatch(GameUiRoot gameUiRoot)
        {
            _watch = new StageScaleWatch(gameUiRoot.GetRootElement());
        }

        public void Initialize()
        {
            _watch.Start();
        }
    }
}
