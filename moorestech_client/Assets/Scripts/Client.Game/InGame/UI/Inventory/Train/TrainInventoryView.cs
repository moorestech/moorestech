using System.Collections.Generic;
using Client.Game.InGame.Train.View.Object;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Train
{
    public class TrainInventoryView : MonoBehaviour, ITrainInventoryView
    {
        private const string ContainerMissingMessage = "この列車にはアイテムコンテナがありません";
        private const string TrainCarMissingMessage = "列車が見つかりません";
        private const string OpenFailedMessage = "列車インベントリを開けません";

        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects => _subInventorySlotObjects;
        public int Count => _subInventorySlotObjects.Count;
        public List<IItemStack> SubInventory { get; } = new();
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; protected set; }


        [SerializeField] private Transform slotParentTransform;

        [SerializeField] private TMP_Text containerMissingMessageText;

        private readonly List<ItemSlotView> _subInventorySlotObjects = new();


        public void Initialize(TrainCarEntityObject trainCarEntity)
        {
            containerMissingMessageText.gameObject.SetActive(false);
            ISubInventoryIdentifier = new TrainInventorySubInventoryIdentifier(trainCarEntity.TrainCarInstanceId.AsPrimitive());
            for (int i = 0; i < trainCarEntity.GetTrainCarMasterElement().InventorySlots; i++)
            {
                var slotObject = Instantiate(ItemSlotView.Prefab, slotParentTransform);
                _subInventorySlotObjects.Add(slotObject);
            }
        }

        public void HideSlotObjects()
        {
            SubInventory.Clear();
            ClearSlotObjects();
        }

        public void ShowMessage(TrainInventoryMessageType messageType)
        {
            // 表示種別に応じてView内部で文言を決定する
            // Resolve the display text inside the view based on the message type
            var message = messageType switch
            {
                TrainInventoryMessageType.ContainerMissing => ContainerMissingMessage,
                TrainInventoryMessageType.TrainCarMissing => TrainCarMissingMessage,
                TrainInventoryMessageType.OpenFailed => OpenFailedMessage,
                _ => OpenFailedMessage
            };
            containerMissingMessageText.text = message;
            containerMissingMessageText.gameObject.SetActive(true);
        }

        public void UpdateItemList(List<IItemStack> response)
        {
            SubInventory.Clear();
            SubInventory.AddRange(response);
        }

        public void UpdateInventorySlot(int slot, IItemStack item)
        {
            if (SubInventory.Count <= slot)
            {
                // TODO ログ基盤に入れる
                Debug.LogError($"インベントリのサイズを超えています。Item:{item} slot:{slot}");
                return;
            }

            SubInventory[slot] = item;
        }

        public void DestroyUI()
        {
            Destroy(gameObject);
        }

        private void ClearSlotObjects()
        {
            foreach (var slotObject in _subInventorySlotObjects)
            {
                if (slotObject != null) Destroy(slotObject.gameObject);
            }

            _subInventorySlotObjects.Clear();
        }
    }
}
