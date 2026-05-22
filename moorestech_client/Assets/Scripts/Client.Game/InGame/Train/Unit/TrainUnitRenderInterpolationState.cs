namespace Client.Game.InGame.Train.Unit
{
    // 列車表示の補間率を読み取り側へ公開する
    // Exposes train render interpolation rate to readers
    public interface ITrainUnitRenderInterpolationProvider
    {
        double GetRenderInterpolationRate();
    }

    // simulator が更新した最新の描画補間率を保持する
    // Stores the latest render interpolation rate updated by the simulator
    public sealed class TrainUnitRenderInterpolationState : ITrainUnitRenderInterpolationProvider
    {
        private double _renderInterpolationRate;

        public double GetRenderInterpolationRate()
        {
            return _renderInterpolationRate;
        }

        public void SetRenderInterpolationRate(double renderInterpolationRate)
        {
            _renderInterpolationRate = renderInterpolationRate;
        }
    }
}
