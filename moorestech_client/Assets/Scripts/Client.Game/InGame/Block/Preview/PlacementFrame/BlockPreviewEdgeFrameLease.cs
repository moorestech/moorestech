using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.Context;
using Core.Master;
using UniRx;
using UniRx.Triggers;

namespace Client.Game.InGame.Block.Preview.PlacementFrame
{
    /// <summary>
    ///     ブロック1個ぶんの枠の借り口。描画指示で借り、描画しない指示と破棄で返す
    ///     A block's frame lease; rents on a render instruction and returns on a non-render instruction or destruction
    /// </summary>
    public sealed class BlockPreviewEdgeFrameLease : IPreviewOnlyObject
    {
        private readonly BlockGameObject _owner;
        private BlockPreviewEdgeFramePool _rentedFromPool;
        private BlockPreviewBoundingBox _rentedFrame;

        public BlockPreviewEdgeFrameLease(BlockGameObject owner)
        {
            _owner = owner;
            // 撤去と置き換えのどちらの破棄でも返す
            // Return on destruction by either removal or replacement
            owner.OnDestroyAsObservable().Subscribe(_ => Release());
        }

        public void Initialize(BlockId blockId)
        {
        }

        // 有効化だけでは借りない。描画の指示が来た時だけ借りる
        // Activation alone never rents; only a render instruction does
        public void SetActive(bool active)
        {
            if (!active) Release();
        }

        public void SetEnableRenderers(bool enable)
        {
            if (enable) Rent();
            else Release();
        }

        private void Rent()
        {
            // silent-ok: 描画指示は半径の再評価ごとに重ねて届く / 借りている枠をそのまま使う
            // Render instructions repeat on every radius re-evaluation; keep the frame already rented
            if (_rentedFrame != null) return;
            // ハンドシェイクで復元されるブロックはClientDIContextより先に作られるため、借りる時にプールを読む
            // Handshake-restored blocks are created before ClientDIContext, so the pool is read at rent time
            _rentedFromPool = ClientDIContext.BlockPreviewEdgeFramePool;
            _rentedFrame = _rentedFromPool.Rent(_owner);
        }

        private void Release()
        {
            // silent-ok: 描画しない指示は借りていない時にも届く（設置モード外の全ブロック等） / 返すものが無い
            // Non-render instructions also arrive with nothing rented (e.g. every block outside build mode)
            if (_rentedFrame == null) return;
            _rentedFromPool.Return(_rentedFrame);
            _rentedFrame = null;
        }
    }
}
