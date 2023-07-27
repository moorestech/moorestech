using System;
using System.Collections.Generic;
using System.Linq;
using Core.Block.Blocks.Machine;
using Core.Block.Blocks.State;
using Core.Block.Config;
using MainGame.Basic.UI;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Builder;
using MainGame.UnityView.UI.Builder.BluePrint;
using MainGame.UnityView.UI.Builder.Element;
using MainGame.UnityView.UI.Builder.Unity;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
using Newtonsoft.Json;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.View
{
    /// <summary>
    /// TODO これ自体のリファクタをした方がいいかもなぁ 一つのインベントリクラスが動的に構築するんじゃなくて、各ブロックやインベントリに合わせてオブジェクトを生成しておくみたいな形式
    /// </summary>
    public class PlayerInventorySlots : MonoBehaviour
    {
        [SerializeField] private List<UIBuilderItemSlotObject> mainInventorySlots;
        [SerializeField] private UIBuilder uiBuilder;
        [SerializeField] private Transform subInventorySlotsParent;
        
        /// <summary>
        /// サブインベントリのオブジェクトのリスト
        /// </summary>
        private List<IUIBuilderObject> _subInventoryElementObjects = new();
        /// <summary>
        /// サブインベントリのスロットだけ
        /// </summary>
        private List<UIBuilderItemSlotObject> _subInventorySlots = new();

        public event Action<int> OnRightClickDown;
        public event Action<int> OnLeftClickDown;
        
        public event Action<int> OnRightClickUp;
        public event Action<int> OnLeftClickUp;
        public event Action<int> OnCursorEnter;
        public event Action<int> OnCursorExit;
        public event Action<int> OnCursorMove;
        public event Action<int> OnDoubleClick;
        public event Action<SubInventoryOptions> OnSetSubInventory;


        private void Awake()
        {
            //メインインベントリのスロットのイベント登録
            mainInventorySlots.
                Select((slot,index) => new{slot,index}).ToList().
                ForEach(slot =>
                {
                    slot.slot.OnRightClickDown += _ => OnRightClickDown?.Invoke(slot.index);
                    slot.slot.OnLeftClickDown += _ => OnLeftClickDown?.Invoke(slot.index);
                    slot.slot.OnRightClickUp += _ => OnRightClickUp?.Invoke(slot.index);
                    slot.slot.OnLeftClickUp += _ => OnLeftClickUp?.Invoke(slot.index);
                    slot.slot.OnCursorEnter += _ => OnCursorEnter?.Invoke(slot.index);
                    slot.slot.OnCursorExit += _ => OnCursorExit?.Invoke(slot.index);
                    slot.slot.OnCursorMove += _ => OnCursorMove?.Invoke(slot.index);
                    slot.slot.OnDoubleClick += _ => OnDoubleClick?.Invoke(slot.index);
                });
        }

        public void SetImage(int slot,ItemViewData itemView, int count)
        {
            if (slot < mainInventorySlots.Count)
            {
                mainInventorySlots[slot].SetItem(itemView,count);
            }else if(slot - mainInventorySlots.Count < _subInventorySlots.Count)
            {
                _subInventorySlots[slot - mainInventorySlots.Count].SetItem(itemView,count);
            }
        }


        private bool _isBlockInventoryOpening = false;
        private Vector2Int _blockPosition;

        public void SetSubSlots(SubInventoryViewBluePrint subInventoryViewBluePrint,SubInventoryOptions subInventoryOptions)
        {
            OnSetSubInventory?.Invoke(subInventoryOptions);

            _isBlockInventoryOpening = subInventoryOptions.IsBlock;
            _blockPosition = subInventoryOptions.BlockPosition;
            
            foreach (var subSlot in _subInventoryElementObjects)
            {
                Destroy(((MonoBehaviour)subSlot).gameObject);
            }
            _subInventoryElementObjects.Clear();
            _subInventoryElementObjects = uiBuilder.CreateSlots(subInventoryViewBluePrint,subInventorySlotsParent);

            _subInventorySlots = _subInventoryElementObjects.
                Where(o => o.BluePrintElement.ElementElementType is UIBluePrintElementType.ArraySlot or UIBluePrintElementType.OneSlot).
                Select(o => o as UIBuilderItemSlotObject).ToList();
            _subInventorySlots.
                Select((slot,index) => new{slot,index}).ToList().
                ForEach(slot =>
                {
                    var slotIndex = slot.index + mainInventorySlots.Count;
                    slot.slot.OnRightClickDown += _ => OnRightClickDown?.Invoke(slotIndex);
                    slot.slot.OnLeftClickDown += _ => OnLeftClickDown?.Invoke(slotIndex);
                    slot.slot.OnRightClickUp += _ => OnRightClickUp?.Invoke(slotIndex);
                    slot.slot.OnLeftClickUp += _ => OnLeftClickUp?.Invoke(slotIndex);
                    slot.slot.OnCursorEnter += _ => OnCursorEnter?.Invoke(slotIndex);
                    slot.slot.OnCursorExit += _ => OnCursorExit?.Invoke(slotIndex);
                    slot.slot.OnCursorMove += _ => OnCursorMove?.Invoke(slotIndex);
                    slot.slot.OnDoubleClick += _ => OnDoubleClick?.Invoke(slotIndex);
                });
        }
        
        /// <summary>
        /// ブロックの状態を表示する機能(プログレスバーの矢印の表示など）
        /// </summary>
        public void SetBlockState(BlockStateChangeProperties stateChangeProperties,string blockType,Vector2Int blockPos)
        {
            //ブロックを開いてなかったらスルー
            if (!_isBlockInventoryOpening) return;
            //開いているブロックじゃなければスルー
            if (_blockPosition != blockPos) return;
            
            //Machine以外は実装上表示することがないのでスルー
            //TODo 今後タイプが増えたら抽象化する
            if (blockType != VanillaBlockType.Machine && blockType != VanillaBlockType.Miner) return;
            
            
            //⚠️ ここから下はMachine、Minerのみの処理
            var progressArrow = _subInventoryElementObjects.Where(p =>
                p.BluePrintElement.ElementElementType == UIBluePrintElementType.ProgressArrow).ToList();
            if (progressArrow.Count == 0)
            {
                Debug.LogError("プログレスバーの矢印がない");
                return;
            }

            var data = JsonConvert.DeserializeObject<CommonMachineBlockStateChangeData>(stateChangeProperties.CurrentStateData);
            var amount = data.ProcessingRate;
            if (stateChangeProperties.CurrentState == VanillaMachineBlockStateConst.IdleState)
            {
                amount = 0;
            }

            foreach (var arrow in progressArrow)
            {
                ((UIBuilderProgressArrowObject)arrow).SetFillAmount(amount);
            }
        }

        /// <summary>
        /// ブループリントシステムで設定された名前でスロットのRectTransformを取得する
        /// </summary>
        /// <param name="idName">ブループリントで設定された名前</param>
        public RectTransformReadonlyData GetSlotRect(string idName)
        {
            foreach (var slot in _subInventorySlots)
            {
                if (slot.BluePrintElement?.IdName == idName)
                {
                    return new RectTransformReadonlyData(slot.transform as RectTransform);
                }
            }

            //TODO スロット以外の要素も探索するようにする

            return null;
        }

    }
}