using System;
using Core.Master;
using UniRx;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction
{
    /// <summary>
    ///     「このブロックはこの種別の鉱脈にしか置けない」という設置制限の共有状態。書き手はチュートリアル、読み手は設置判定と鉱脈表示
    ///     Shared "this block may only go on veins of this type" restriction; written by the tutorial, read by placement checks and the vein view
    /// </summary>
    public class VeinRestrictedPlacementState
    {
        public IObservable<Unit> OnChanged => _onChanged;
        private readonly Subject<Unit> _onChanged = new();

        // 制限を入れたチュートリアル。解除は入れた本人だけに許し、入れ替わり時の取り違えを防ぐ
        // The tutorial that set the restriction; only it may clear, so an overlapping tutorial cannot drop someone else's
        private Guid? _ownerTutorialGuid;
        private Guid _veinGuid;
        private BlockId _blockId;

        public void SetRestriction(Guid tutorialGuid, Guid veinGuid, BlockId blockId)
        {
            _ownerTutorialGuid = tutorialGuid;
            _veinGuid = veinGuid;
            _blockId = blockId;
            _onChanged.OnNext(Unit.Default);
        }

        public void Clear(Guid tutorialGuid)
        {
            if (_ownerTutorialGuid != tutorialGuid) return;

            _ownerTutorialGuid = null;
            _onChanged.OnNext(Unit.Default);
        }

        /// <summary>
        ///     そのブロックが制限対象なら、置いてよい鉱脈種別を返す。同種インスタンスは全て許可される
        ///     Returns the vein type the block may go on when it is restricted; every instance of that type is allowed
        /// </summary>
        public bool TryGetRestrictedVeinType(BlockId blockId, out Guid veinGuid)
        {
            veinGuid = _veinGuid;
            return _ownerTutorialGuid.HasValue && _blockId == blockId;
        }
    }
}
