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
    /// 機械のレシピ選択パネル。対象ブロックのアンロック済みレシピを一覧表示し、クリックで選択する。
    /// 選択状態はブロック状態同期（MachineBlockStateDetail.SelectedRecipeGuid）から毎フレーム反映する。
    /// Recipe selection panel; lists unlocked recipes of the block and applies selection via protocol.
    /// </summary>
    public class MachineRecipeSelectionPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform recipeSlotParent;

        [Inject] private IGameUnlockStateData _gameUnlockStateData;

        private readonly List<(ItemSlotView view, MachineRecipeMasterElement recipe)> _slots = new();
        private readonly CompositeDisposable _subscriptions = new();
        private BlockGameObject _blockGameObject;
        private CancellationTokenSource _cts;
        private string _lastSelectedGuidStr;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            _cts = new CancellationTokenSource();
            BuildRecipeSlots();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _subscriptions.Dispose();
            _cts?.Dispose();
        }

        private void BuildRecipeSlots()
        {
            // 対象ブロックのアンロック済みレシピを共有マスタから導出する（一覧取得プロトコルは無い）
            // Derive the block's unlocked recipes from the shared master (no list protocol exists)
            var blockGuid = _blockGameObject.BlockMasterElement.BlockGuid;
            var unlockInfos = _gameUnlockStateData.MachineRecipeUnlockStateInfos;
            foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (recipe.BlockGuid != blockGuid) continue;
                if (!unlockInfos.TryGetValue(recipe.MachineRecipeGuid, out var info) || !info.IsUnlocked) continue;

                var slotView = Instantiate(ItemSlotView.Prefab, recipeSlotParent);
                SetRecipeView(slotView, recipe);

                var captured = recipe;
                slotView.OnLeftClickUp.Subscribe(_ => SendSetRecipe(captured).Forget()).AddTo(_subscriptions);
                slotView.OnRightClickUp.Subscribe(_ => SendClearIfSelected(captured).Forget()).AddTo(_subscriptions);
                _slots.Add((slotView, recipe));
            }

            #region Internal

            void SetRecipeView(ItemSlotView slotView, MachineRecipeMasterElement recipe)
            {
                // 先頭出力アイテムをアイコンとして表示し、ツールチップに入出力を並べる
                // Show the first output item as the icon; list inputs and outputs in the tooltip
                var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
                var itemView = ClientContext.ItemImageContainer.GetItemView(outputItemId);
                slotView.SetItem(itemView, recipe.OutputItems[0].Count, BuildToolTip(recipe));
            }

            string BuildToolTip(MachineRecipeMasterElement recipe)
            {
                var inputs = new List<string>();
                foreach (var input in recipe.InputItems) inputs.Add($"{MasterHolder.ItemMaster.GetItemMaster(input.ItemGuid).Name}×{input.Count}");
                var outputs = new List<string>();
                foreach (var output in recipe.OutputItems) outputs.Add($"{MasterHolder.ItemMaster.GetItemMaster(output.ItemGuid).Name}×{output.Count}");
                return $"{string.Join(" + ", inputs)} → {string.Join(" + ", outputs)} ({recipe.Time}秒)";
            }

            #endregion
        }

        private void Update()
        {
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
            // 選択中のレシピを右クリックした時だけ解除する
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
