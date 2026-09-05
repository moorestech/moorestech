// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.UIState;
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
        [SerializeField] private PlayerInventoryMainSlotsView mainSlotsView;

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

        private void Awake()
        {
            // 動的生成されたメインスロットを共通操作ハンドラへ接続する
            // Connect dynamically generated main slots to the shared interaction handler
            mainSlotsView.OnSlotViewCreated.Subscribe(slotView => slotView.OnPointerEvent.Subscribe(HandleSlotPointerEvent));

            //整理ボタンのクリックでメイン＋開いているサブインベントリを整理する
            //Clicking the sort button tidies the main and currently open sub inventory.
            sortInventoryButton.onClick.AddListener(() => _playerInventory.SortInventory());
        }

        private void Start()
        {
            _interaction = new PlayerInventorySlotInteraction(_playerInventory, mainSlotsView.SlotViews);
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
            // レベルアップ後のスロット数に合わせてビューを増やす
            // Grow the views to match the slot count after level upgrades
            mainSlotsView.SetSlotCount(_playerInventory.LocalPlayerInventory.MainSlotCount);

            for (var i = 0; i < _playerInventory.LocalPlayerInventory.Count; i++)
            {
                var item = _playerInventory.LocalPlayerInventory[i];
                var itemView = ClientContext.ItemImageContainer.GetItemView(item.Id);

                if (i < mainSlotsView.SlotViews.Count)
                    mainSlotsView.SlotViews[i].SetItem(itemView, item.Count);
                else
                    break;
            }
        }
    }
}
