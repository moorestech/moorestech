using System.Collections.Generic;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.View
{
    //TODO ブロックインベントリを開くシステム
    //TODO modelから呼び出されるようにする？　UI共通基盤ことを考えると削除した方がいいかも
    public class BlockInventoryItemView : MonoBehaviour
    {
        private const int SlotCount = 5;
        
        
        [SerializeField] private RectTransform inputItems;
        [SerializeField] private RectTransform outputItems;

        [SerializeField] private InventoryItemSlot inventoryItemSlot;
        private readonly List<InventoryItemSlot> _playerInventorySlots = new();
        private readonly List<InventoryItemSlot> _inputInventorySlots = new();
        private readonly List<InventoryItemSlot> _outputInventorySlots = new();
        private ItemImages _itemImages;
        private int inputSlotCount;
        private int outputSlotCount;
        
        [Inject]
        public void Construct(ItemImages itemImages)
        {
            
            _itemImages = itemImages;
            
            //プレイヤーインベントリの表示
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                var s = Instantiate(inventoryItemSlot.gameObject, transform).GetComponent<InventoryItemSlot>();
                s.Construct(i);
                _playerInventorySlots.Add(s);
            }

            //ブロックインベントリの表示
            for (int i = 0; i < SlotCount; i++)
            {
                //inputの設定
                var s = Instantiate(inventoryItemSlot.gameObject, inputItems).GetComponent<InventoryItemSlot>();
                s.Construct(i);
                s.gameObject.SetActive(false);
                _inputInventorySlots.Add(s);
                
                //outputの設定
                s = Instantiate(inventoryItemSlot.gameObject, outputItems).GetComponent<InventoryItemSlot>();
                s.Construct(i);
                s.gameObject.SetActive(false);
                _outputInventorySlots.Add(s);
            }
        }

        //ブロックのインベントリを開く
        public void OpenBlockInventory(string uiType, params short[] param)
        {
            //TODO ステータスとステUitypeを渡しているけど、ここは共通インベントリ基盤を作成する
            OpenInventory(param[0],param[1]);
        }
        
        public void OpenInventory(int input, int output)
        {
            inputSlotCount = input;
            outputSlotCount = output;
            //全て非表示
            _inputInventorySlots.ForEach(i => i.gameObject.SetActive(false));
            _outputInventorySlots.ForEach(i => i.gameObject.SetActive(false));
            
            //必要な分だけ表示し、indexを設定する
            for (int i = 0; i < input; i++)
            {
                _inputInventorySlots[i].gameObject.SetActive(true);
                _inputInventorySlots[i].Construct(PlayerInventoryConstant.MainInventorySize + i);
            }

            for (int i = 0; i < output; i++)
            {
                _outputInventorySlots[i].gameObject.SetActive(true);
                _outputInventorySlots[i].Construct(PlayerInventoryConstant.MainInventorySize + input + i);
            }
        }

        private void OnInventoryUpdate(int slot, int itemId, int count)
        {
            var sprite = _itemImages.GetItemImage(itemId);
            _playerInventorySlots[slot].SetItem(sprite,count);
        }
        

        public void BlockInventoryUpdate(int slot, int itemId, int count)
        {
            var sprite = _itemImages.GetItemImage(itemId);
            if (slot < inputSlotCount)
            {
                _inputInventorySlots[slot].SetItem(sprite,count);
            }
            else
            {
                _outputInventorySlots[slot - inputSlotCount].SetItem(sprite,count);
            }
        }
        
        
        public IReadOnlyList<InventoryItemSlot> GetAllInventoryItemSlots()
        {
            var merged = new List<InventoryItemSlot>();
            merged.AddRange(_playerInventorySlots);
            merged.AddRange(_inputInventorySlots);
            merged.AddRange(_outputInventorySlots);
            return merged;
        }
        
        public InventoryItemSlot GetOpenedInventoryItemSlot(int index)
        {
            if (index < PlayerInventoryConstant.MainInventorySize)
            {
                return _playerInventorySlots[index];
            }
            
            if (index < PlayerInventoryConstant.MainInventorySize + inputSlotCount)
            {
                return _inputInventorySlots[index];
            }

            return _outputInventorySlots[index - inputSlotCount];
        }
    }
}