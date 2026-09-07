// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。DI登録から外れたため毎フレーム再描画も廃止し、Phase2で本体ごと削除する（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and unmaintained; the per-frame redraw is gone now that the view left DI, and the class itself is deleted in Phase2 (docs/webui/ugui-retirement-plan.md)
using System;
using System.Collections.Generic;
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
    }
}
