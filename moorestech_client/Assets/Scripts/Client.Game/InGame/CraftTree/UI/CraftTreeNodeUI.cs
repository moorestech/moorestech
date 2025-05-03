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
    /// クラフトツリーの個々のノードの視覚表現を担当するクラス
    /// </summary>
    public class CraftTreeNodeUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _background;
        [SerializeField] private Image _itemIcon;
        [SerializeField] private Text _itemNameText;
        [SerializeField] private Text _progressText;
        [SerializeField] private Image _progressBar;
        [SerializeField] private Color _incompleteColor = Color.gray;
        [SerializeField] private Color _completedColor = new Color(1.0f, 0.6f, 0.0f); // オレンジ色
        [SerializeField] private GameObject _expandIcon;
        [SerializeField] private float _hoverScaleMultiplier = 1.1f;
        [SerializeField] private float _width = 120f;
        [SerializeField] private float _height = 80f;
        
        // イベント
        public event Action OnNodeClicked;
        public event Action<Vector2> OnNodeRightClicked;
        
        // 内部状態
        private CraftTreeNode _node;
        private ItemMaster _itemMaster;
        private Vector3 _originalScale;
        private bool _isHovering;
        
        private void Awake()
        {
            _originalScale = transform.localScale;
            _itemMaster = Core.Master.MasterHolder.Instance.ItemMaster;
        }
        
        /// <summary>
        /// ノードの設定
        /// </summary>
        /// <param name="node">設定するノードデータ</param>
        public void SetupNode(CraftTreeNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            
            // アイテム情報を取得
            var itemInfo = _itemMaster.LookupItemInfo(node.itemId);
            if (itemInfo == null)
            {
                Debug.LogWarning($"Item info not found for ID: {node.itemId}");
                return;
            }
            
            // UI要素の更新
            if (_itemNameText != null)
                _itemNameText.text = itemInfo.DisplayName;
                
            if (_itemIcon != null)
                _itemIcon.sprite = LoadItemSprite(itemInfo.IconPath);
                
            // 進捗表示の更新
            UpdateProgressDisplay();
            
            // 展開アイコンの表示/非表示
            if (_expandIcon != null)
                _expandIcon.SetActive(node.children.Count == 0 && node.state != NodeState.Completed);
                
            // スタイルの更新
            UpdateStyle();
        }
        
        /// <summary>
        /// 進捗表示の更新
        /// </summary>
        private void UpdateProgressDisplay()
        {
            if (_node == null)
                return;
                
            // プログレステキストの更新
            if (_progressText != null)
                _progressText.text = $"{_node.currentCount} / {_node.requiredCount}";
                
            // プログレスバーの更新
            if (_progressBar != null)
            {
                float fillAmount = _node.requiredCount > 0 
                    ? (float)_node.currentCount / _node.requiredCount 
                    : 0;
                _progressBar.fillAmount = Mathf.Clamp01(fillAmount);
            }
        }
        
        /// <summary>
        /// スタイルの更新
        /// </summary>
        private void UpdateStyle()
        {
            if (_node == null || _background == null)
                return;
                
            // 状態に応じて色を変更
            _background.color = _node.state == NodeState.Completed ? _completedColor : _incompleteColor;
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
        
        /// <summary>
        /// ノードの幅を取得
        /// </summary>
        /// <returns>ノードの幅</returns>
        public float GetWidth()
        {
            return _width;
        }
        
        /// <summary>
        /// ノードの高さを取得
        /// </summary>
        /// <returns>ノードの高さ</returns>
        public float GetHeight()
        {
            return _height;
        }
        
        #region イベントハンドラ
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_node == null)
                return;
                
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // 左クリック
                OnNodeClicked?.Invoke();
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                // 右クリック
                OnNodeRightClicked?.Invoke(eventData.position);
            }
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            transform.localScale = _originalScale * _hoverScaleMultiplier;
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            transform.localScale = _originalScale;
        }
        
        #endregion
    }
}