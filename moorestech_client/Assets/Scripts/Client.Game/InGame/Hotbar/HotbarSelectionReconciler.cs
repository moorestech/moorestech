using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using UniRx;

namespace Client.Game.InGame.Hotbar
{
    /// <summary>
    ///     割当変更のたびに保持中の由来枠を解決し直し、ハイライトと実際の設置対象の乖離を残さない
    ///     Re-resolves the held origin slot on every assignment change so the highlight and the actual placement target never diverge
    /// </summary>
    public class HotbarSelectionReconciler
    {
        private readonly ClientHotbarDatastore _clientHotbarDatastore;
        private readonly PlacementTargetResolver _placementTargetResolver;
        private readonly PlaceSystemStateController _placeSystemStateController;

        public HotbarSelectionReconciler(ClientHotbarDatastore clientHotbarDatastore, PlacementTargetResolver placementTargetResolver, PlaceSystemStateController placeSystemStateController)
        {
            _clientHotbarDatastore = clientHotbarDatastore;
            _placementTargetResolver = placementTargetResolver;
            _placeSystemStateController = placeSystemStateController;

            _clientHotbarDatastore.OnAssignmentsChanged.Subscribe(_ => Reconcile());
        }

        private void Reconcile()
        {
            // ホットバー由来で保持しているときだけ追従する。メニュー・スポイト由来は割当と無関係
            // Only a hotbar-originated hold follows the assignments; menu and eyedropper origins are unrelated
            if (!_placeSystemStateController.CurrentOrigin.TryGetHotbarSlot(out var slot)) return;

            // 空枠・未解決になった枠は保持を解く。持てない対象を握ったままにしない
            // A slot that became empty or unresolvable releases the hold, so no unusable target stays in hand
            var targetId = _clientHotbarDatastore.Assignments[slot];
            if (targetId == Guid.Empty || !_placementTargetResolver.TryResolve(targetId, out var target))
            {
                _placeSystemStateController.SetTarget(null, PlacementOrigin.NonHotbar);
                return;
            }

            _placeSystemStateController.SetTarget(target, PlacementOrigin.FromHotbarSlot(slot));
        }
    }
}
