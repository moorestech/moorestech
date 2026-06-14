using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // 受信した CleanRoomEffect を保持する。未プッシュ時の初期値は InValidRoom=false（最悪側）。
    // 未配線・未登録の機械が最高グレードで稼働する「安全でないフォールバック」を構造的に禁止する。
    // Holds the last pushed CleanRoomEffect; defaults to InValidRoom=false (worst case)
    // so an unwired machine can never run at the best grade by accident.
    public class CleanRoomStateReceiverComponent : ICleanRoomStateReceiver
    {
        public CleanRoomEffect CurrentEffect { get; private set; } = new(false, 0, 0.0);

        public void SetCleanRoomEffect(CleanRoomEffect effect)
        {
            CurrentEffect = effect;
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
