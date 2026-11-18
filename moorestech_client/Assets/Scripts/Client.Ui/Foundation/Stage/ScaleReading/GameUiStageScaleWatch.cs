using Client.Ui.Foundation.Root;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Foundation.Stage.ScaleReading
{
    /// <summary>
    /// 倍率の見張りを始める入口
    /// Entry point that starts the scale watch
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
