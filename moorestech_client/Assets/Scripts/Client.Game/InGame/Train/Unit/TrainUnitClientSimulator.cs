using Client.Game.InGame.Train.View;
using UnityEngine;
using VContainer.Unity;
using Client.Game.InGame.Train.Network.TickSynchronization;

namespace Client.Game.InGame.Train.Unit
{
    public sealed class TrainUnitClientSimulator : ITickable
    {
        private readonly TrainTickContext _context;
        private readonly TrainUnitHashVerifier _hashVerifier;
        private readonly TrainUnitVisualUpdateSystem _visualUpdateSystem;

        public TrainUnitClientSimulator(TrainTickContext context, TrainUnitHashVerifier hashVerifier, TrainUnitVisualUpdateSystem visualUpdateSystem)
        {
            _context = context;
            _hashVerifier = hashVerifier;
            _visualUpdateSystem = visualUpdateSystem;
        }

        public void Tick()
        {
            // stream進行後の推定tickで列車の表示を更新する。
            // Update train visuals from the estimated tick after advancing the stream.
            var renderTick = _context.AdvanceController.Advance(Time.deltaTime, _hashVerifier);
            _visualUpdateSystem.UpdateAll(renderTick, _context.State.GetTick());
        }
    }
}
