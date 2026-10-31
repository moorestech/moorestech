using System;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using UniRx;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    // 部屋から押し込まれた稼働可否を保持し、変化したときだけブロック状態として通知する
    // Holds the operability pushed by the room and notifies a block state change only when it changes
    public class CleanRoomOperabilityStateComponent : IBlockStateObservable
    {
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        private readonly Subject<Unit> _onChangeBlockState = new();

        // 部屋の効果が届く前の値は CleanRoomEffect.BeforeFirstPush の1か所で決める（抽選側のデコレータも同じ所から引く）
        // The value before any room effect arrives is defined once by CleanRoomEffect.BeforeFirstPush (the draw decorator reads the same place)
        public CleanRoomOperability Operability { get; private set; } = CleanRoomEffect.BeforeFirstPush().Operability;

        public void SetOperability(CleanRoomOperability operability)
        {
            BlockException.CheckDestroy(this);

            // 毎tick同じ値が届くため、変化したときだけ通知する
            // The same value arrives every tick, so notify only on change
            if (Operability == operability) return;
            Operability = operability;
            _onChangeBlockState.OnNext(Unit.Default);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            return new[] { CleanRoomOperabilityStateDetail.CreateState(Operability) };
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
            _onChangeBlockState.Dispose();
        }
    }
}
