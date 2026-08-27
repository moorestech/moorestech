using System;
using Core.Master;
using UniRx;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction
{
    /// <summary>
    ///     「このブロックはこの鉱脈にしか置けない」という設置制限の共有状態。書き手はチュートリアル、読み手は設置判定と鉱脈表示
    ///     Shared "this block may only go on this vein" restriction; written by the tutorial, read by placement checks and the vein view
    /// </summary>
    public class VeinRestrictedPlacementState
    {
        public Guid? VeinGuid { get; private set; }
        public BlockId? BlockId { get; private set; }

        public IObservable<Unit> OnChanged => _onChanged;
        private readonly Subject<Unit> _onChanged = new();

        public void SetRestriction(Guid veinGuid, BlockId blockId)
        {
            VeinGuid = veinGuid;
            BlockId = blockId;
            _onChanged.OnNext(Unit.Default);
        }

        public void Clear()
        {
            VeinGuid = null;
            BlockId = null;
            _onChanged.OnNext(Unit.Default);
        }

        public bool IsRestrictedBlock(BlockId blockId)
        {
            return VeinGuid.HasValue && BlockId.HasValue && BlockId.Value == blockId;
        }
    }
}
