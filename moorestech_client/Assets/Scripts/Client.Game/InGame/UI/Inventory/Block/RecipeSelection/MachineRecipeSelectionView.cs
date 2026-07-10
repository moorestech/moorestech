using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.State;
using Game.UnlockState;
using Server.Protocol.PacketResponse;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block.RecipeSelection
{
    // UI移行まで既存テキストを選択ボタンに使う最小View
    // Minimal view that reuses the existing text as a selector until the large UI migration
    public class MachineRecipeSelectionView : MonoBehaviour
    {
        private readonly List<Guid?> _candidateRecipeGuids = new();
        private readonly Dictionary<Guid, string> _recipeLabels = new();
        private BlockGameObject _blockGameObject;
        private TMP_Text _label;
        private Button _button;
        private bool _isRequesting;

        public void Initialize(TMP_Text label, BlockGameObject blockGameObject, IGameUnlockStateData unlockState)
        {
            if (_button != null) _button.onClick.RemoveListener(OnButtonClicked);
            _candidateRecipeGuids.Clear();
            _recipeLabels.Clear();
            _label = label;
            _blockGameObject = blockGameObject;
            _button = label.GetComponent<Button>();
            if (_button == null) _button = label.gameObject.AddComponent<Button>();
            _button.onClick.AddListener(OnButtonClicked);

            // 未選択と利用可能レシピを候補にする
            // Put unselected first, followed only by unlocked recipes for this machine
            _candidateRecipeGuids.Add(null);
            var blockGuid = blockGameObject.BlockMasterElement.BlockGuid;
            var candidates = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .Where(recipe => recipe.BlockGuid == blockGuid)
                .Where(recipe => unlockState.MachineRecipeUnlockStateInfos.TryGetValue(recipe.MachineRecipeGuid, out var state) && state.IsUnlocked);
            foreach (var recipe in candidates)
            {
                _candidateRecipeGuids.Add(recipe.MachineRecipeGuid);
                _recipeLabels.Add(recipe.MachineRecipeGuid,
                    string.Join(" / ", recipe.OutputItems.Select(output => MasterHolder.ItemMaster.GetItemMaster(output.ItemGuid).Name)));
            }
            Refresh();
        }

        public void Refresh()
        {
            var state = _blockGameObject.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
            if (state == null) return;
            if (state.MachineRecipeGuid == null)
            {
                SetLabel(null);
                return;
            }
            if (Guid.TryParse(state.MachineRecipeGuid, out var recipeGuid)) SetLabel(recipeGuid);
            else _label.text = "不正なレシピGUID";
        }

        private void OnButtonClicked()
        {
            SelectNextAsync().Forget();

            #region Internal

            async UniTask SelectNextAsync()
            {
                if (_isRequesting || _candidateRecipeGuids.Count == 0) return;
                var state = _blockGameObject.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
                if (!TryGetCurrentRecipeGuid(state, out var currentGuid))
                {
                    _label.text = "不正なレシピGUID";
                    return;
                }

                // 次候補を要求して適用結果を表示する
                // Request the next candidate and display the applied result
                _isRequesting = true;
                var currentIndex = _candidateRecipeGuids.IndexOf(currentGuid);
                var nextGuid = _candidateRecipeGuids[(currentIndex + 1) % _candidateRecipeGuids.Count];
                var request = MachineRecipeSelectionProtocol.MachineRecipeSelectionRequest.CreateSetRequest(_blockGameObject.BlockPosInfo.OriginalPos, nextGuid);
                var response = await ClientContext.VanillaApi.MachineRecipeSelection.Send(request, this.GetCancellationTokenOnDestroy());
                _isRequesting = false;
                if (response == null) return;

                switch (response.FailureReason)
                {
                    case MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.None:
                        SetLabel(response.GetAppliedRecipeGuid());
                        return;
                    case MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.NotHandshaken:
                    case MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.BlockNotFound:
                    case MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.NotMachine:
                    case MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.RecipeNotFound:
                    case MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.RecipeForDifferentBlock:
                    case MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.RecipeLocked:
                    case MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.RefundCapacityInsufficient:
                    case MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.InvalidRequest:
                    case MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.NotAuthorized:
                    case MachineRecipeSelectionProtocol.MachineRecipeSelectionFailureReason.TooFar:
                        _label.text = $"レシピ変更失敗: {response.FailureReason}";
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            static bool TryGetCurrentRecipeGuid(MachineBlockStateDetail state, out Guid? recipeGuid)
            {
                recipeGuid = null;
                if (state == null || state.MachineRecipeGuid == null) return true;
                if (!Guid.TryParse(state.MachineRecipeGuid, out var parsedRecipeGuid)) return false;
                recipeGuid = parsedRecipeGuid;
                return true;
            }

            #endregion
        }

        private void SetLabel(Guid? recipeGuid)
        {
            if (!recipeGuid.HasValue)
            {
                _label.text = "レシピ未選択";
                return;
            }

            _label.text = _recipeLabels.TryGetValue(recipeGuid.Value, out var label) ? label : "不明なレシピ";
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnButtonClicked);
        }
    }
}
