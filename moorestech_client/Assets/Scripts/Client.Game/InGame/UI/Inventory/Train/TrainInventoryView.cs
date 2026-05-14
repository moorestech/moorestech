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
        private const string ContainerMissingMessageObjectName = "ContainerMissingMessage";

        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects => _subInventorySlotObjects;
        public int Count => _subInventorySlotObjects.Count;
        public List<IItemStack> SubInventory { get; } = new();
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; protected set; }


        [SerializeField] private Transform slotParentTransform;
        private readonly List<ItemSlotView> _subInventorySlotObjects = new();
        private TMP_Text _containerMissingMessageText;


        public void Initialize(TrainCarEntityObject trainCarEntity)
        {
            HideContainerMissingMessage();
            ISubInventoryIdentifier = new TrainInventorySubInventoryIdentifier(trainCarEntity.TrainCarInstanceId.AsPrimitive());
            for (int i = 0; i < trainCarEntity.TrainCarMasterElement.InventorySlots; i++)
            {
                var slotObject = Instantiate(ItemSlotView.Prefab, slotParentTransform);
                _subInventorySlotObjects.Add(slotObject);
            }
        }

        public void ShowContainerMissingMessage(string message)
        {
            SubInventory.Clear();
            ClearSlotObjects();

            // 列車にアイテムコンテナがないことをUI内に表示する
            // Show within the UI that this train has no item container
            var messageText = GetOrCreateContainerMissingMessageText();
            messageText.text = message;
            messageText.gameObject.SetActive(true);
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
                //TODO ログ基盤にいれる
                Debug.LogError($"インベントリのサイズを超えています。item:{item} slot:{slot}");
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

        private void HideContainerMissingMessage()
        {
            if (_containerMissingMessageText == null) return;
            _containerMissingMessageText.gameObject.SetActive(false);
        }

        private TMP_Text GetOrCreateContainerMissingMessageText()
        {
            if (_containerMissingMessageText != null) return _containerMissingMessageText;

            // Prefabを直接編集せず、実行時にメッセージ用テキストを追加する
            // Add the message text at runtime without editing the prefab asset
            var messageObject = new GameObject(ContainerMissingMessageObjectName, typeof(RectTransform));
            messageObject.transform.SetParent(transform, false);
            var rectTransform = (RectTransform)messageObject.transform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            _containerMissingMessageText = messageObject.AddComponent<TextMeshProUGUI>();
            _containerMissingMessageText.alignment = TextAlignmentOptions.Center;
            _containerMissingMessageText.fontSize = 24;
            _containerMissingMessageText.color = Color.white;
            _containerMissingMessageText.raycastTarget = false;
            return _containerMissingMessageText;
        }
    }
}
