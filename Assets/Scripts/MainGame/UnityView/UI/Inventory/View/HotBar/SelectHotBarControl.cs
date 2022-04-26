using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.View.HotBar
{
    public class SelectHotBarControl : MonoBehaviour
    {
        [SerializeField] private SelectHotBarView selectHotBarView;
        [SerializeField] private HotBarItemView hotBarItemView;
        
        private MoorestechInputSettings _inputSettings;
        
        public int SelectIndex => _selectIndex;
        private int _selectIndex = 0;
        

        public bool IsClicked => _isClickedCount == 0 || _isClickedCount == 1;
        private int _isClickedCount = -1;
        
        public void Start()
        {
            _inputSettings = new();
            _inputSettings.Enable();

            foreach (var slot in hotBarItemView.Slots)
            {
                slot.OnLeftClickDown += ClickItem;
            }
            
        }

        private void ClickItem(InventoryItemSlot inventoryItemSlot)
        {
            var slot = 0;
            for (var i = 0; i < hotBarItemView.Slots.Count; i++)
            {
                if (inventoryItemSlot == hotBarItemView.Slots[i])
                {
                    slot = i;
                }
            }
            _selectIndex = slot;
            _isClickedCount = 0;
            selectHotBarView.SetSelect(slot);
        }

        private void Update()
        {
            //キーボード入力で選択
            if (_inputSettings.UI.HotBar.ReadValue<int>() != 0)
            {
                //キー入力で得られる値は1〜9なので-1する
                _selectIndex = _inputSettings.UI.HotBar.ReadValue<int>() - 1;
                selectHotBarView.SetSelect(_selectIndex);
            }
        }

        /// <summary>
        /// ButtonがクリックされたことをFixedUpdate内で確認したいのでクリックされてから2フレームはtrueとする
        /// </summary>
        public void FixedUpdate()
        {
            if (_isClickedCount == 0 || _isClickedCount == 1)
            {
                _isClickedCount++;
            }
            if (_isClickedCount == 2)
            {
                _isClickedCount = -1;
            }
        }
    }
}