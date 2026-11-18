using UniRx;

namespace Client.Ui.Foundation.Stage.ScaleReading
{
    /// <summary>
    /// 舞台の倍率の読み口
    /// Read port for the stage scale
    /// </summary>
    public sealed class StageScaleSource
    {
        public IReadOnlyReactiveProperty<float> Scale { get; }

        public StageScaleSource(IReadOnlyReactiveProperty<float> scale)
        {
            Scale = scale;
        }
    }
}
