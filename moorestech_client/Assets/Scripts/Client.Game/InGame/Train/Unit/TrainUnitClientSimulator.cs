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
            if (!_context.IsInitialSnapshotApplied || _context.State.IsStopped) return;
            // 進行後のtickで列車表示を更新。
            // Update train visuals at the advanced tick.
            var renderTick = _context.AdvanceController.Advance(Time.deltaTime, _hashVerifier);
            if (!_context.State.IsStopped)
                _visualUpdateSystem.UpdateAll(renderTick, _context.State.GetTick());
        }
    }
}
