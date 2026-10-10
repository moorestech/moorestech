using System;
using System.Collections.Generic;
using Core.Master;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste.Cache
{
    /// <summary>
    ///     共有プランナーが読む入力の値を保持し、同一入力の計画だけ再利用する
    ///     Retains planner input values and reuses a plan only for identical inputs
    /// </summary>
    internal sealed class BlueprintPastePlanCache
    {
        private BlueprintPastePlanKey _key;
        private BlueprintPastePlan _plan;

        internal bool TryGet(BlueprintPastePlanKey key, out BlueprintPastePlan plan)
        {
            plan = _plan;
            return plan != null && _key.Matches(key);
        }

        internal void Store(BlueprintPastePlanKey key, BlueprintPastePlan plan)
        {
            _key = key;
            _plan = plan;
        }

        internal void Clear()
        {
            _key = null;
            _plan = null;
        }
    }

    internal sealed class BlueprintPastePlanKey
    {
        private readonly Guid _blueprintGuid;
        private readonly ulong _blueprintRevision;
        private readonly int _rotationStep;
        private readonly ulong _occupancyRevision;
        private readonly ulong _walletRevision;
        private readonly ulong _unlockRevision;
        private readonly bool _paymentWaived;
        private readonly BlueprintPasteOrigin[] _origins;
        private readonly Dictionary<ItemId, int> _held;

        internal BlueprintPastePlanKey(Guid blueprintGuid, ulong blueprintRevision, int rotationStep,
            ulong occupancyRevision, ulong walletRevision, ulong unlockRevision, bool paymentWaived,
            IReadOnlyList<BlueprintPasteOrigin> origins, IReadOnlyDictionary<ItemId, int> held)
        {
            _blueprintGuid = blueprintGuid;
            _blueprintRevision = blueprintRevision;
            _rotationStep = rotationStep;
            _occupancyRevision = occupancyRevision;
            _walletRevision = walletRevision;
            _unlockRevision = unlockRevision;
            _paymentWaived = paymentWaived;
            _origins = new BlueprintPasteOrigin[origins.Count];
            for (var i = 0; i < origins.Count; i++) _origins[i] = origins[i];
            _held = new Dictionary<ItemId, int>();
            foreach (var (itemId, count) in held) _held[itemId] = count;
        }

        internal bool Matches(BlueprintPastePlanKey other)
        {
            if (_blueprintGuid != other._blueprintGuid || _blueprintRevision != other._blueprintRevision ||
                _rotationStep != other._rotationStep || _occupancyRevision != other._occupancyRevision ||
                _walletRevision != other._walletRevision || _unlockRevision != other._unlockRevision ||
                _paymentWaived != other._paymentWaived || _origins.Length != other._origins.Length ||
                _held.Count != other._held.Count) return false;

            // 途中の地面欠損や所持数変更も一致判定に含める
            // Include intermediate ground failures and inventory count changes in equality
            for (var i = 0; i < _origins.Length; i++)
                if (_origins[i].Position != other._origins[i].Position ||
                    _origins[i].IsGroundFound != other._origins[i].IsGroundFound) return false;
            foreach (var (itemId, count) in _held)
                if (!other._held.TryGetValue(itemId, out var otherCount) || count != otherCount) return false;
            return true;
        }
    }
}
