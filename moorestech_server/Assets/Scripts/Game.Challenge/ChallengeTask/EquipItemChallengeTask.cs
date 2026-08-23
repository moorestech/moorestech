using System;
using System.Collections.Generic;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Mooresmaster.Model.ChallengesModule;
using UniRx;

namespace Game.Challenge.Task
{
    /// <summary>
    ///     選択中の装備スロットに対象アイテムが入った時に達成する
    ///     Completes when the target item sits in the selected equipment slot
    /// </summary>
    public class EquipItemChallengeTask : IChallengeTask
    {
        public ChallengeMasterElement ChallengeMasterElement { get; }
        public IObservable<IChallengeTask> OnChallengeComplete => _onChallengeComplete;
        private readonly Subject<IChallengeTask> _onChallengeComplete = new();

        private bool _completed;
        private bool _initialCheckDone;

        // イベントは判定対象のplayerIdを積むだけで、判定と発火はティックで行う
        // Events only enqueue the player ids to check; the check and the completion fire on the tick
        private readonly HashSet<int> _playerIdsToCheck = new();

        private readonly ItemId _targetItemId;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public static IChallengeTask Create(ChallengeMasterElement challengeMasterElement)
        {
            return new EquipItemChallengeTask(challengeMasterElement);
        }

        private EquipItemChallengeTask(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;

            // マスタのtaskParam型不整合を生成時に検出する（前例: CompleteResearchChallengeTask）
            // Detect a taskParam type mismatch at construction time (precedent: CompleteResearchChallengeTask)
            var equipItemTaskParam = (EquipItemTaskParam)challengeMasterElement.TaskParam;
            _targetItemId = MasterHolder.ItemMaster.GetItemId(equipItemTaskParam.ItemGuid);
            _playerInventoryDataStore = ServerContext.GetService<IPlayerInventoryDataStore>();

            // スロット中身と選択indexは別々に変わるため両方を購読する
            // Slot contents and the selected index change independently, so subscribe to both
            var equipmentUpdateEvent = ServerContext.GetService<IEquipmentInventoryUpdateEvent>();
            equipmentUpdateEvent.Subscribe(OnEquipmentSlotUpdated);
            equipmentUpdateEvent.SubscribeSelectedEquipmentIndex(OnSelectedEquipmentIndexUpdated);
        }

        // 完了カスケードはインベントリ操作の途中に割り込ませない（ユーザー裁定 2026-08-23）
        // Never let the completion cascade cut into an in-flight inventory operation (user adjudication 2026-08-23)
        public void ManualUpdate()
        {
            if (_completed) return;

            EnqueueInitialCheckOnce();

            foreach (var playerId in _playerIdsToCheck)
            {
                if (IsTargetEquipped(playerId))
                {
                    _completed = true;
                    break;
                }
            }
            _playerIdsToCheck.Clear();

            if (_completed) _onChallengeComplete.OnNext(this);

            #region Internal

            // チャレンジ開始前から装備済みの取りこぼしを、プレイヤーが1人以上いる最初のtickで回収する
            // Recover an item equipped before this challenge started, on the first tick that has at least one player
            void EnqueueInitialCheckOnce()
            {
                if (_initialCheckDone) return;

                var allPlayerIds = _playerInventoryDataStore.GetAllPlayerId();
                if (allPlayerIds.Count == 0) return;

                _initialCheckDone = true;
                foreach (var playerId in allPlayerIds) _playerIdsToCheck.Add(playerId);
            }

            #endregion
        }

        private void OnEquipmentSlotUpdated(PlayerInventoryUpdateEventProperties properties)
        {
            _playerIdsToCheck.Add(properties.PlayerId);
        }

        private void OnSelectedEquipmentIndexUpdated(EquipmentSelectedIndexUpdateEventProperties properties)
        {
            _playerIdsToCheck.Add(properties.PlayerId);
        }

        private bool IsTargetEquipped(int playerId)
        {
            var selectedItem = _playerInventoryDataStore.GetInventoryData(playerId).EquipmentInventory.GetSelectedItem();
            return selectedItem.Id == _targetItemId;
        }
    }
}
