namespace Client.Game.InGame.Train.Unit
{
    // 列車表示が参照する推定描画 tick を公開する
    // Exposes the estimated render tick used by train views
    public interface ITrainUnitRenderTickProvider
    {
        double GetRenderTick();
    }

    // simulator が更新した最新の推定描画 tick を保持する
    // Stores the latest estimated render tick updated by the simulator
    public sealed class TrainUnitRenderTickState : ITrainUnitRenderTickProvider
    {
        private double _renderTick;

        public double GetRenderTick()
        {
            return _renderTick;
        }

        public void SetRenderTick(double renderTick)
        {
            _renderTick = renderTick;
        }
    }
}
