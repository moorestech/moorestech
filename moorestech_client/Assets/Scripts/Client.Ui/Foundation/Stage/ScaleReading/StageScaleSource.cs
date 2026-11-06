using UniRx;

namespace Client.Ui.Foundation.Stage.ScaleReading
{
    /// <summary>
    /// 舞台の倍率の読み口（実画面 px ÷ パネル px）。本番は StageScaleWatch の値を、テストは外から与えた値を配る（設計書 A-41）
    /// Read port for the stage scale (real-screen px per panel px); production hands out the StageScaleWatch value and tests hand in their own (design A-41)
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
