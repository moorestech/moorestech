using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.CleanRoom
{
    // レベル付き出力の抽選・格納ロジック（EUV→天井→down-bin、空スロット予約）。OutputInventory から分離した協調オブジェクト。
    // Leveled-output draw/insert logic (EUV/ceiling/down-bin, empty-slot reservation); a collaborator split out of the output inventory.
    public class CleanRoomMachineOutputEmitter
    {
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly FluidContainer[] _fluidContainers;
        private readonly CleanRoomStateReceiverComponent _receiver;

        // EUV失敗/Out で出力なしになったときに呼ぶテスト可視コールバック（サイレント消失アサート用）
        // Invoked when an EUV fail / Out yields no output (feeds the no-silent-loss assertion)
        private readonly Action _onNoOutputForTest;

        private IReadOnlyList<IItemStack> OutputSlot => _itemDataStoreService.InventoryItems;

        public CleanRoomMachineOutputEmitter(OpenableInventoryItemDataStoreService itemDataStoreService,
            FluidContainer[] fluidContainers, CleanRoomStateReceiverComponent receiver, Action onNoOutputForTest)
        {
            _itemDataStoreService = itemDataStoreService;
            _fluidContainers = fluidContainers;
            _receiver = receiver;
            _onNoOutputForTest = onNoOutputForTest;
        }

        // レベル付き出力は完了時 ItemId が down-bin/効果で変わり得るため空スロット方式で予約する
        // Leveled outputs reserve EMPTY slots because the final ItemId can change via down-bin / effect
        public bool IsAllowedToOutputItem(MachineRecipeMasterElement machineRecipe)
        {
            // 全出力要素を emission 順にシミュレートし、各要素が実際に格納できることを確認する。
            // 旧実装はレベル付き出力と副産物を独立判定したため、両者が同じ最後の空スロットを取り合い、副産物がサイレント消失した。
            // Simulate every output element in emission order so each one is actually placeable.
            // The old split check let a leveled chip and a by-product both claim the same last empty slot, silently dropping the by-product.
            var work = new IItemStack[OutputSlot.Count];
            for (var i = 0; i < work.Length; i++) work[i] = OutputSlot[i];
            var reserved = new bool[work.Length];

            foreach (var itemOutput in machineRecipe.OutputItems)
            {
                var isLeveled = MasterHolder.SemiconductorChipMaster.TryGetDistribution(machineRecipe.MachineRecipeGuid, itemOutput.ItemGuid, out _);
                if (isLeveled)
                {
                    // レベル付き出力は完了時 ItemId が未確定なので空スロットを1つ専有予約する（既存スタックへ統合不可）
                    // Leveled output reserves a dedicated empty slot (final ItemId is unknown; cannot merge into an existing stack)
                    var emptyIndex = FindUnreservedEmptySlot();
                    if (emptyIndex < 0) return false;
                    reserved[emptyIndex] = true;
                    continue;
                }

                // 非レベル出力（副産物）は InsertToSlot と同じ判定でシミュレート格納する（予約済みスロットは使わない）
                // Non-leveled by-products simulate insertion like InsertToSlot (matching-Id first, then empty), never using a reserved slot
                var outputItemId = MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);
                var outputItemStack = ServerContext.ItemStackFactory.Create(outputItemId, itemOutput.Count);
                var slotIndex = FindInsertableSlot(outputItemStack);
                if (slotIndex < 0) return false;
                work[slotIndex] = work[slotIndex].AddItem(outputItemStack).ProcessResultItemStack;
            }

            // 液体出力のスペースを確認する（Vanilla のコピー）
            // Check fluid output space (vanilla copy)
            for (var i = 0; i < machineRecipe.OutputFluids.Length; i++)
            {
                if (i >= _fluidContainers.Length) return false;

                var outputFluidId = MasterHolder.FluidMaster.GetFluidId(machineRecipe.OutputFluids[i].FluidGuid);
                var amount = machineRecipe.OutputFluids[i].Amount;

                if (_fluidContainers[i].FluidId != FluidMaster.EmptyFluidId && _fluidContainers[i].FluidId != outputFluidId) return false;
                if (_fluidContainers[i].Capacity - _fluidContainers[i].Amount < amount) return false;
            }

            return true;

            #region Internal

            int FindUnreservedEmptySlot()
            {
                for (var i = 0; i < work.Length; i++)
                    if (!reserved[i] && work[i].Id == ItemMaster.EmptyItemId) return i;
                return -1;
            }

            int FindInsertableSlot(IItemStack stack)
            {
                for (var i = 0; i < work.Length; i++)
                    if (!reserved[i] && work[i].IsAllowedToAdd(stack)) return i;
                return -1;
            }

            #endregion
        }

        // 出力要素単位で抽選して格納する（cycleSeed で決定的）
        // Emit outputs per element, drawing leveled ones deterministically from cycleSeed
        public void InsertOutputSlot(MachineRecipeMasterElement machineRecipe, long cycleSeed)
        {
            var effect = _receiver.CurrentEffect;

            for (var outputIndex = 0; outputIndex < machineRecipe.OutputItems.Length; outputIndex++)
            {
                var itemOutput = machineRecipe.OutputItems[outputIndex];

                ItemId outputItemId;
                var isLeveled = MasterHolder.SemiconductorChipMaster.TryGetDistribution(
                    machineRecipe.MachineRecipeGuid, itemOutput.ItemGuid, out _);
                if (isLeveled)
                {
                    // percent は EUV catastrophic 失敗率の補数（=成功率）。スキーマ default=1。
                    // percent is the complement of the EUV catastrophic failure rate (= success rate). schema default 1.
                    var percent = itemOutput.Percent;
                    var draw = SemiconductorChipDraw.TryResolveOutputItemId(
                        machineRecipe.MachineRecipeGuid, itemOutput.ItemGuid,
                        effect.MaxGrade, effect.DownBinRate, percent,
                        cycleSeed, outputIndex, out outputItemId);

                    if (draw != DrawResult.Drawn)
                    {
                        // EUV失敗/Out：出力なし（サイレント Lv1 禁止）。テストカウンタへ通知して何も出さない
                        // EUV fail / Out: no output (no silent Lv1). Notify the test counter and emit nothing
                        _onNoOutputForTest?.Invoke();
                        continue;
                    }
                }
                else
                {
                    // 副産物（分布なし）はベース ItemId のまま
                    // By-products keep their base ItemId
                    outputItemId = MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);
                }

                InsertToSlot(ServerContext.ItemStackFactory.Create(outputItemId, itemOutput.Count));
            }

            // 液体出力（Vanilla のコピー）
            // Fluid outputs (vanilla copy)
            for (var i = 0; i < machineRecipe.OutputFluids.Length; i++)
            {
                if (i >= _fluidContainers.Length) break;

                var fluidId = MasterHolder.FluidMaster.GetFluidId(machineRecipe.OutputFluids[i].FluidGuid);
                _fluidContainers[i].AddLiquid(new FluidStack(machineRecipe.OutputFluids[i].Amount, fluidId), FluidContainer.Empty);
            }
        }

        // 同 ItemId スロット→空スロットの順に1スタックを格納する
        // Insert a single stack into a matching-Id slot first, then an empty one
        private void InsertToSlot(IItemStack outputItemStack)
        {
            for (var i = 0; i < OutputSlot.Count; i++)
            {
                if (!OutputSlot[i].IsAllowedToAdd(outputItemStack)) continue;

                var item = OutputSlot[i].AddItem(outputItemStack).ProcessResultItemStack;
                _itemDataStoreService.SetItem(i, item);
                break;
            }
        }
    }
}
