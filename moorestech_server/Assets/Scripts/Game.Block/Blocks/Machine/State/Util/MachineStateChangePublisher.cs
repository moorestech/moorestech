using System;
using UniRx;

namespace Game.Block.Blocks.Machine.State.Util
{
    // 通常機械と清浄室機械の状態イベントを出す口。前回通知した状態を覚え、状態の変化時と加工中に通知する
    // The state-event outlet shared by normal and clean-room machines; remembers the last notified state and notifies on a change or while processing
    internal sealed class MachineStateChangePublisher
    {
        public IObservable<Unit> OnChangeBlockState => _changeState;
        private readonly Subject<Unit> _changeState = new();

        // 状態詳細へ「直前の状態」として載る値。通知の後に更新するので、購読側は通知時点で遷移前の状態を読める
        // Published as the previous state in the detail; updated after notifying so subscribers read the pre-transition state at notice time
        public ProcessState LastState { get; private set; } = ProcessState.Idle;

        // tick外で状態を書き換えた操作や給電の直後に、変化の有無を問わず通知する
        // Notify unconditionally right after an off-tick state rewrite or a power supply
        public void Publish()
        {
            _changeState.OnNext(Unit.Default);
        }

        // tick内の遷移の後、状態が変わった時・加工中なら通知して記録を進める
        // After the tick's transition, notify on a state change or while processing, then advance the record
        public void PublishIfStateChanged(ProcessState currentState)
        {
            if (LastState == currentState && currentState != ProcessState.Processing) return;
            PublishTickState(currentState);
        }

        // tick内で詳細が確定した時点の通知。記録も進めるので、同じtickの遷移判定が重ねて通知しない
        // The notice once the tick's detail settles; it advances the record so the same tick's transition check does not notify again
        public void PublishTickState(ProcessState currentState)
        {
            _changeState.OnNext(Unit.Default);
            LastState = currentState;
        }
    }
}
