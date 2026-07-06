using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.UIState;
using Core.Master;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    ///     TODO フラグ管理をステートベースに変換する
    /// </summary>
    public class PlayerInventoryViewController : MonoBehaviour
    {
        [SerializeField] private GameObject mainInventoryObject;

        [SerializeField] private List<ItemSlotView> mainInventorySlotObjects;
        [SerializeField] private ItemSlotView grabInventorySlotView;

        public Transform SubInventoryParent => subInventoryParent.transform;
        [SerializeField] private Transform subInventoryParent;

        //インベントリ整理ボタン
        //Inventory sort button
        [SerializeField] private Button sortInventoryButton;

        private readonly List<IDisposable> _subInventorySlotUIEventUnsubscriber = new();

        [Inject] private LocalPlayerInventoryController _playerInventory;

        private ISubInventory _subInventory;

        // クリック/ドラッグ操作の解釈を担う非MonoBehaviourハンドラ
        // Non-MonoBehaviour handler that interprets click/drag gestures
        private PlayerInventorySlotInteraction _interaction;

        private bool IsGrabItem => _playerInventory.GrabInventory.Id != ItemMaster.EmptyItemId;

        private void Awake()
        {
            foreach (var mainInventorySlotObject in mainInventorySlotObjects) mainInventorySlotObject.OnPointerEvent.Subscribe(HandleSlotPointerEvent);

            //整理ボタンのクリックでメイン＋開いているサブインベントリを整理する
            //Clicking the sort button tidies the main and currently open sub inventory.
            sortInventoryButton.onClick.AddListener(() => _playerInventory.SortInventory());
        }

        private void Start()
        {
            _interaction = new PlayerInventorySlotInteraction(_playerInventory, mainInventorySlotObjects);
        }

        private void Update()
        {
            InventoryViewUpdate();
        }

        public void SetSubInventory(ISubInventory subInventory)
        {
            foreach (var disposable in _subInventorySlotUIEventUnsubscriber) disposable.Dispose();

            _subInventorySlotUIEventUnsubscriber.Clear();
            _subInventory = subInventory;
            _interaction.SetSubInventory(subInventory);
            _playerInventory.SetSubInventory(subInventory);
            foreach (var sub in _subInventory.SubInventorySlotObjects) _subInventorySlotUIEventUnsubscriber.Add(sub.OnPointerEvent.Subscribe(HandleSlotPointerEvent));
        }

        // スロットのポインタイベントを操作ハンドラへ橋渡しする
        // Bridges slot pointer events to the interaction handler
        private void HandleSlotPointerEvent((ItemSlotView slotObject, ItemUIEventType itemUIEvent) eventProperty)
        {
            _interaction.HandleSlotEvent(eventProperty);
        }

        public void SetActive(bool isActive)
        {
            // webモード中はWeb側が同画面を描画するためuGUIビューは表示しない（falseは常に通す）
            // In web mode the web renders this screen, so never show the uGUI view (false always passes)
            var visible = isActive && !WebUiScreenGate.IsWebUiMode;
            mainInventoryObject.SetActive(visible);
            subInventoryParent.gameObject.SetActive(visible);
        }

        private void InventoryViewUpdate()
        {
            for (var i = 0; i < _playerInventory.LocalPlayerInventory.Count; i++)
            {
                var item = _playerInventory.LocalPlayerInventory[i];
                var itemView = ClientContext.ItemImageContainer.GetItemView(item.Id);

                if (i < mainInventorySlotObjects.Count)
                {
                    mainInventorySlotObjects[i].SetItem(itemView, item.Count);
                }
                else
                {
                    var subIndex = i - mainInventorySlotObjects.Count;
                    _subInventory.SubInventorySlotObjects[subIndex].SetItem(itemView, item.Count);
                }
            }

            grabInventorySlotView.SetActive(IsGrabItem);
            var garbItemView = ClientContext.ItemImageContainer.GetItemView(_playerInventory.GrabInventory.Id);
            grabInventorySlotView.SetItem(garbItemView, _playerInventory.GrabInventory.Count);
        }
    }
}
