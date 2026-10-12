using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Main;
using Game.Blueprint;
using Game.Construction;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using UniRx;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste.Cache
{
    /// <summary>
    ///     プランナー入力を揃えて変更時だけ計画する
    ///     Gathers planner inputs and replans only when one changes
    /// </summary>
    internal sealed class BlueprintPastePlanEvaluator
    {
        private readonly ClientBlueprintPasteWorld _world;
        private readonly ConstructionWalletQuery _wallet;
        private readonly ILocalPlayerInventory _inventory;
        private readonly BlueprintPastePlanCache _cache = new();
        private ulong _walletRevision;

        internal BlueprintPastePlanEvaluator(ClientBlueprintPasteWorld world, ConstructionWalletQuery wallet,
            ILocalPlayerInventory inventory)
        {
            _world = world;
            _wallet = wallet;
            _inventory = inventory;
            _wallet.OnWalletChanged.Subscribe(_ => _walletRevision++);
        }

        internal BlueprintPastePlan Plan(Guid blueprintGuid, ulong blueprintRevision, BlueprintJsonObject blueprint,
            IReadOnlyList<BlueprintPasteOrigin> origins, int rotationStep)
        {
            // 外部変更される無料設置と全所持数を毎フレーム読む
            // Read externally mutable free placement and held counts every frame
            _world.BeginPlan();
            var held = ConstructionMaterialAccounting.TallyHeld(_inventory);
            if (!_world.TryGetUnlockRevision(out var unlockRevision))
            {
                // 世代を持たない解放情報では再利用せず安全に再判定する
                // Replan safely when the unlock source offers no revision
                _cache.Clear();
                return BlueprintPastePlanner.Plan(blueprint, origins, rotationStep, _world, _wallet, held);
            }

            var key = new BlueprintPastePlanKey(blueprintGuid, blueprintRevision, rotationStep,
                _world.OccupancyRevision, _walletRevision, unlockRevision, _world.IsPaymentWaived, origins, held);
            if (_cache.TryGet(key, out var plan)) return plan;
            plan = BlueprintPastePlanner.Plan(blueprint, origins, rotationStep, _world, _wallet, held);
            _cache.Store(key, plan);
            return plan;
        }

        internal void Clear() => _cache.Clear();
    }
}
