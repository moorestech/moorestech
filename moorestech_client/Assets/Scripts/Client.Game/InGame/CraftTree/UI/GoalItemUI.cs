using System;
using Core.Item;
using Core.Master;
using Game.CraftTree.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.Game.InGame.CraftTree.UI
{
    /// <summary>
    /// 目標HUDに表示する個々のアイテム表示を担当するクラス
    /// </summary>
    public class GoalItemUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _itemIcon;
        [SerializeField] private Text _itemNameText;
        [SerializeField] private Text _progressText;
        [SerializeField] private Image _progressBar;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _hoverColor = new Color(0.9f, 0.9f, 1.0f);
        [SerializeField] private float _hoverScaleMultiplier = 1.05f;
        
        /// <summary>
        /// クリック時のイベント
        /// </summary>
        public event Action OnClicked;
        
        private ItemMaster _itemMaster;
        private GoalItem _goalItem;
        private Vector3 _originalScale;
        private bool _isHovering;
        
        private void Awake()
        {
            _originalScale = transform.localScale;
            _itemMaster = Core.Master.MasterHolder.Instance.ItemMaster;
        }
        
        /// <summary>
        /// 目標アイテムの設定
        /// </summary>
        /// <param name="goalItem">設定する目標アイテム</param>
        public void SetupGoalItem(GoalItem goalItem)
        {
            _goalItem = goalItem ?? throw new ArgumentNullException(nameof(goalItem));
            
            // アイテム情報を取得
            var itemInfo = _itemMaster.LookupItemInfo(goalItem.itemId);
            if (itemInfo == null)
            {
                Debug.LogWarning($"Item info not found for ID: {goalItem.itemId}");
                return;
            }
            
            // UI要素の更新
            if (_itemNameText != null)
                _itemNameText.text = itemInfo.DisplayName;
                
            if (_itemIcon != null)
                _itemIcon.sprite = LoadItemSprite(itemInfo.IconPath);
                
            UpdateProgressDisplay();
        }
        
        /// <summary>
        /// 進捗表示の更新
        /// </summary>
        private void UpdateProgressDisplay()
        {
            if (_goalItem == null)
                return;
                
            if (_progressText != null)
                _progressText.text = $"{_goalItem.availableCount} / {_goalItem.requiredCount}";
                
            if (_progressBar != null)
            {
                float fillAmount = _goalItem.requiredCount > 0 
                    ? (float)_goalItem.availableCount / _goalItem.requiredCount 
                    : 0;
                _progressBar.fillAmount = Mathf.Clamp01(fillAmount);
                
                // 完了状態に応じた色変更（必要に応じて）
                _progressBar.color = _goalItem.IsCompleted() 
                    ? new Color(0.2f, 0.8f, 0.2f) // 緑
                    : new Color(0.9f, 0.6f, 0.1f); // オレンジ
            }
        }
        
        /// <summary>
        /// アイテムアイコンのロード
        /// </summary>
        /// <param name="iconPath">アイコンパス</param>
        /// <returns>ロードしたスプライト</returns>
        private Sprite LoadItemSprite(string iconPath)
        {
            // リソースマネージャーからアイコンをロード
            // 実際の実装はゲームのリソース管理システムによって異なる
            try
            {
                // 例: Resourcesフォルダからロード
                return Resources.Load<Sprite>(iconPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load icon at path {iconPath}: {ex.Message}");
                return null;
            }
        }
        
        #region イベントハンドラ
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_goalItem == null)
                return;
                
            // クリック時のアクション（ツリー表示など）
            OnClicked?.Invoke();
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            transform.localScale = _originalScale * _hoverScaleMultiplier;
            
            // 背景色変更（Imageコンポーネントがある場合）
            var background = GetComponent<Image>();
            if (background != null)
                background.color = _hoverColor;
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            transform.localScale = _originalScale;
            
            // 背景色を戻す
            var background = GetComponent<Image>();
            if (background != null)
                background.color = _normalColor;
        }
        
        #endregion
    }
}