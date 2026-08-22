using System;
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

        public void ManualUpdate()
        {
            // チャレンジ開始前から装備済みの取りこぼしを初回tickだけ照会する
            // Query once on the first tick to recover an item equipped before this challenge started
            if (_completed || _initialCheckDone) return;
            _initialCheckDone = true;

            foreach (var playerId in _playerInventoryDataStore.GetAllPlayerId())
            {
                CheckEquipped(playerId);
            }
        }

        private void OnEquipmentSlotUpdated(PlayerInventoryUpdateEventProperties properties)
        {
            CheckEquipped(properties.PlayerId);
        }

        private void OnSelectedEquipmentIndexUpdated(EquipmentSelectedIndexUpdateEventProperties properties)
        {
            CheckEquipped(properties.PlayerId);
        }

        private void CheckEquipped(int playerId)
        {
            if (_completed) return;

            var selectedItem = _playerInventoryDataStore.GetInventoryData(playerId).EquipmentInventory.GetSelectedItem();
            if (selectedItem.Id != _targetItemId) return;

            _completed = true;
            _onChallengeComplete.OnNext(this);
        }
    }
}
