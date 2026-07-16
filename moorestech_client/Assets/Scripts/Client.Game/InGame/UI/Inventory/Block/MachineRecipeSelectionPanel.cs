using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.State;
using Game.UnlockState;
using Mooresmaster.Model.MachineRecipesModule;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Block
{
    /// <summary>
    /// レシピ選択パネル。アンロック済みレシピをクリックで選択
    /// 選択状態はブロック状態同期（MachineBlockStateDetail.SelectedRecipeGuid）から毎フレーム反映する。
    /// Recipe selection panel; lists unlocked recipes of the block and applies selection via protocol.
    /// </summary>
    public class MachineRecipeSelectionPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform recipeSlotParent;

        [Inject] private IGameUnlockStateData _gameUnlockStateData;

        private readonly List<(ItemSlotView view, MachineRecipeMasterElement recipe)> _slots = new();
        private readonly List<MachineRecipeMasterElement> _blockRecipes = new();
        private CompositeDisposable _slotSubscriptions = new();
        private BlockGameObject _blockGameObject;
        private CancellationTokenSource _cts;
        private string _lastSelectedGuidStr;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            _cts = new CancellationTokenSource();

            // 対象ブロックのレシピを共有マスタから導出する（一覧取得プロトコルは無い）
            // Derive the block's recipes from the shared master (no list protocol exists)
            var blockGuid = _blockGameObject.BlockMasterElement.BlockGuid;
            foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (recipe.BlockGuid == blockGuid) _blockRecipes.Add(recipe);
            }
            BuildRecipeSlots();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _slotSubscriptions.Dispose();
            _cts?.Dispose();
        }

        private void BuildRecipeSlots()
        {
            // 旧スロットを破棄して作り直す（アンロック増加時の再構築に対応）
            // Destroy old slots and rebuild (supports rebuilding when unlocks grow)
            _slotSubscriptions.Dispose();
            _slotSubscriptions = new CompositeDisposable();
            foreach (var (view, _) in _slots) Destroy(view.gameObject);
            _slots.Clear();
            _lastSelectedGuidStr = null;

            var unlockInfos = _gameUnlockStateData.MachineRecipeUnlockStateInfos;
            foreach (var recipe in _blockRecipes)
            {
                if (!unlockInfos.TryGetValue(recipe.MachineRecipeGuid, out var info) || !info.IsUnlocked) continue;

                var slotView = Instantiate(ItemSlotView.Prefab, recipeSlotParent);
                SetRecipeView(slotView, recipe);

                var captured = recipe;
                slotView.OnLeftClickUp.Subscribe(_ => SendSetRecipe(captured).Forget()).AddTo(_slotSubscriptions);
                slotView.OnRightClickUp.Subscribe(_ => SendClearIfSelected(captured).Forget()).AddTo(_slotSubscriptions);
                _slots.Add((slotView, recipe));
            }

            #region Internal

            void SetRecipeView(ItemSlotView slotView, MachineRecipeMasterElement recipe)
            {
                // 先頭出力をアイコン化、無ければ液体で代替
                // Show the first output as the icon; fluid-only recipes fall back to the first output fluid
                if (0 < recipe.OutputItems.Length)
                {
                    var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
                    var itemView = ClientContext.ItemImageContainer.GetItemView(outputItemId);
                    slotView.SetItem(itemView, recipe.OutputItems[0].Count, BuildToolTip(recipe));
                    return;
                }
                var fluidId = MasterHolder.FluidMaster.GetFluidId(recipe.OutputFluids[0].FluidGuid);
                var fluidView = ClientContext.FluidImageContainer.GetItemView(fluidId);
                slotView.SetFluid(fluidView, BuildToolTip(recipe));
            }

            string BuildToolTip(MachineRecipeMasterElement recipe)
            {
                // 入出力ともアイテム→液体の順で列挙する
                // List items first and then fluids on both sides
                var inputs = new List<string>();
                foreach (var input in recipe.InputItems) inputs.Add($"{MasterHolder.ItemMaster.GetItemMaster(input.ItemGuid).Name}×{input.Count}");
                foreach (var input in recipe.InputFluids) inputs.Add($"{MasterHolder.FluidMaster.GetFluidMaster(input.FluidGuid).Name}×{input.Amount}");
                var outputs = new List<string>();
                foreach (var output in recipe.OutputItems) outputs.Add($"{MasterHolder.ItemMaster.GetItemMaster(output.ItemGuid).Name}×{output.Count}");
                foreach (var output in recipe.OutputFluids) outputs.Add($"{MasterHolder.FluidMaster.GetFluidMaster(output.FluidGuid).Name}×{output.Amount}");
                return $"{string.Join(" + ", inputs)} → {string.Join(" + ", outputs)} ({recipe.Time}秒)";
            }

            #endregion
        }

        private void Update()
        {
            // アンロック数とスロット数の不一致で再構築する（開いたままのアンロック追従）
            // Rebuild when the unlocked count no longer matches the slots (tracks unlocks while open)
            var unlockInfos = _gameUnlockStateData.MachineRecipeUnlockStateInfos;
            var unlockedCount = 0;
            foreach (var recipe in _blockRecipes)
            {
                if (unlockInfos.TryGetValue(recipe.MachineRecipeGuid, out var info) && info.IsUnlocked) unlockedCount++;
            }
            if (unlockedCount != _slots.Count) BuildRecipeSlots();

            // 選択状態はサーバーのブロック状態同期から導出する（Set応答を待たずとも変化が反映される）
            // Selection highlight derives from the synced block state, so external changes also show up
            var state = _blockGameObject.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
            if (state == null || state.SelectedRecipeGuid == _lastSelectedGuidStr) return;
            _lastSelectedGuidStr = state.SelectedRecipeGuid;

            foreach (var (view, recipe) in _slots)
            {
                var isSelected = recipe.MachineRecipeGuid.ToString() == state.SelectedRecipeGuid;
                view.SetSlotViewOption(new CommonSlotViewOption { HotBarSelected = isSelected });
            }
        }

        private async UniTaskVoid SendSetRecipe(MachineRecipeMasterElement recipe)
        {
            var cts = _cts;
            if (cts == null) return;
            var request = MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRecipeRequest(
                _blockGameObject.BlockPosInfo.OriginalPos, recipe.MachineRecipeGuid, ClientContext.PlayerConnectionSetting.PlayerId);
            var response = await ClientContext.VanillaApi.Response.SendMachineRecipeSelectionRequest(request, cts.Token);
            if (cts.IsCancellationRequested) return;
            if (response == null || !response.Success) Debug.Log($"レシピ選択失敗: {response?.FailureReason}");
        }

        private async UniTaskVoid SendClearIfSelected(MachineRecipeMasterElement recipe)
        {
            // 右クリックのみ選択解除
            // Right-click clears only when the clicked recipe is the selected one
            if (recipe.MachineRecipeGuid.ToString() != _lastSelectedGuidStr) return;
            var cts = _cts;
            if (cts == null) return;
            var request = MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateClearRequest(
                _blockGameObject.BlockPosInfo.OriginalPos, ClientContext.PlayerConnectionSetting.PlayerId);
            var response = await ClientContext.VanillaApi.Response.SendMachineRecipeSelectionRequest(request, cts.Token);
            if (cts.IsCancellationRequested) return;
            if (response == null || !response.Success) Debug.Log($"レシピ選択解除失敗: {response?.FailureReason}");
        }
    }
}
