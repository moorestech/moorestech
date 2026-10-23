namespace Game.Block.Interface.Component
{
    // 接続サービスが拠点に触る面。接続済みかの読み書きと、プール残量・接続できるか（プールが判定）の受け取りだけを持つ
    // The surface the connection service touches on a base: the connected flag and receiving the pool amount and whether it can connect (judged by the pool)
    public interface ISpaceElevatorBase : IBlockComponent
    {
        bool IsConnected { get; }
        void MarkConnected();
        void SetCablePoolAvailability(int poolAmount, bool hasEnoughCable);
    }
}
