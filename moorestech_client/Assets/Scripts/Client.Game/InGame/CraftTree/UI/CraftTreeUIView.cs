using System;
using System.Collections.Generic;
using Client.Game.InGame.CraftTree.Utility;
using Core.Item;
using Game.CraftTree.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.CraftTree.UI
{
    /// <summary>
    /// クラフトツリーUIの視覚表現を担当するクラス
    /// </summary>
    public class CraftTreeUIView : MonoBehaviour
    {
        // UI要素への参照
        [SerializeField] private GameObject _craftTreePanel;
        [SerializeField] private RectTransform _nodesContainer;
        [SerializeField] private CraftTreeNodeUI _nodeUIPrefab;
        [SerializeField] private RecipeSelectModalUI _recipeSelectModal;
        [SerializeField] private NodeContextMenuUI _contextMenu;
        [SerializeField] private SearchItemUI _searchItemUI;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _searchButton;
        
        // ノードの配置・表示設定
        [SerializeField] private float _nodeHorizontalSpacing = 200f;
        [SerializeField] private float _nodeVerticalSpacing = 100f;
        [SerializeField] private float _initialScale = 1.0f;
        [SerializeField] private float _minScale = 0.5f;
        [SerializeField] private float _maxScale = 2.0f;
        
        // イベント
        public event Action<CraftTreeNode> OnNodeClick;
        public event Action<CraftTreeNode, Vector2> OnNodeRightClick;
        public event Action<ItemId, int> OnGoalItemSelect;
        public event Action OnCloseButtonClick;
        public event Action OnSearchButtonClick;
        
        // 内部状態
        private Dictionary<CraftTreeNode, CraftTreeNodeUI> _nodeUIMap;
        private Vector2 _dragStartPosition;
        private Vector2 _dragOffset;
        private bool _isDragging;
        private float _currentScale;
        
        private void Awake()
        {
            _nodeUIMap = new Dictionary<CraftTreeNode, CraftTreeNodeUI>();
            _currentScale = _initialScale;
            
            // イベント登録
            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => OnCloseButtonClick?.Invoke());
                
            if (_searchButton != null)
                _searchButton.onClick.AddListener(() => OnSearchButtonClick?.Invoke());
        }
        
        /// <summary>
        /// UIの表示
        /// </summary>
        public void ShowUI()
        {
            gameObject.SetActive(true);
            
            if (_craftTreePanel != null)
                _craftTreePanel.SetActive(true);
                
            // コンテキストメニューとモーダルは初期状態では非表示
            HideContextMenu();
            HideRecipeSelectModal();
        }
        
        /// <summary>
        /// UIの非表示
        /// </summary>
        public void HideUI()
        {
            gameObject.SetActive(false);
            
            if (_craftTreePanel != null)
                _craftTreePanel.SetActive(false);
                
            HideContextMenu();
            HideRecipeSelectModal();
            HideSearchItemUI();
        }
        
        /// <summary>
        /// クラフトツリーの描画
        /// </summary>
        /// <param name="tree">描画するツリー</param>
        public void RenderTree(Game.CraftTree.Data.CraftTree tree)
        {
            ClearCurrentTree();
            
            if (tree == null || tree.rootNode == null || _nodesContainer == null || _nodeUIPrefab == null)
                return;
                
            // ルートノードから再帰的に描画
            RenderNode(tree.rootNode, Vector2.zero, 0);
            
            // ノード間のラインを描画（必要に応じて実装）
            DrawNodeConnections();
        }
        
        /// <summary>
        /// 現在のツリー表示をクリア
        /// </summary>
        private void ClearCurrentTree()
        {
            _nodeUIMap.Clear();
            
            // 子オブジェクトをすべて削除
            if (_nodesContainer != null)
            {
                foreach (Transform child in _nodesContainer)
                {
                    if (child.GetComponent<CraftTreeNodeUI>() != null)
                        Destroy(child.gameObject);
                }
            }
        }
        
        /// <summary>
        /// ノードを再帰的に描画
        /// </summary>
        /// <param name="node">描画するノード</param>
        /// <param name="position">配置位置</param>
        /// <param name="depth">階層の深さ</param>
        /// <returns>描画したノードの幅</returns>
        private float RenderNode(CraftTreeNode node, Vector2 position, int depth)
        {
            if (node == null || _nodeUIPrefab == null || _nodesContainer == null)
                return 0f;
                
            // ノードUIを生成
            var nodeUIObject = Instantiate(_nodeUIPrefab, _nodesContainer);
            if (nodeUIObject == null)
                return 0f;
                
            // 位置設定
            nodeUIObject.GetComponent<RectTransform>().anchoredPosition = position;
            
            // ノード情報を設定
            nodeUIObject.SetupNode(node);
            
            // イベントリスナーを設定
            nodeUIObject.OnNodeClicked += () => OnNodeClick?.Invoke(node);
            nodeUIObject.OnNodeRightClicked += (screenPos) => OnNodeRightClick?.Invoke(node, screenPos);
            
            // マップに登録
            _nodeUIMap[node] = nodeUIObject;
            
            // 子ノードがない場合は終了
            if (node.children.Count == 0)
                return nodeUIObject.GetWidth();
                
            // 子ノードの位置を計算して再帰的に描画
            float totalChildrenWidth = 0f;
            float childY = position.y - _nodeVerticalSpacing;
            List<float> childWidths = new List<float>();
            
            // 子ノードの幅を計算
            foreach (var child in node.children)
            {
                float childWidth = CalculateNodeWidth(child);
                childWidths.Add(childWidth);
                totalChildrenWidth += childWidth + _nodeHorizontalSpacing;
            }
            
            // 最後のスペースを引く
            totalChildrenWidth -= _nodeHorizontalSpacing;
            
            // 子ノードの配置開始X座標を計算（親の中心から子の全体幅の半分を引く）
            float startX = position.x - totalChildrenWidth / 2f;
            
            // 子ノードを描画
            for (int i = 0; i < node.children.Count; i++)
            {
                float childX = startX + childWidths[i] / 2f;
                RenderNode(node.children[i], new Vector2(childX, childY), depth + 1);
                startX += childWidths[i] + _nodeHorizontalSpacing;
            }
            
            return Mathf.Max(nodeUIObject.GetWidth(), totalChildrenWidth);
        }
        
        /// <summary>
        /// ノードの幅を計算（子ノードを含む）
        /// </summary>
        /// <param name="node">計算対象ノード</param>
        /// <returns>合計幅</returns>
        private float CalculateNodeWidth(CraftTreeNode node)
        {
            if (node == null)
                return 0f;
                
            if (node.children.Count == 0)
                return _nodeUIPrefab.GetWidth();
                
            float totalChildrenWidth = 0f;
            foreach (var child in node.children)
            {
                totalChildrenWidth += CalculateNodeWidth(child) + _nodeHorizontalSpacing;
            }
            
            return Mathf.Max(_nodeUIPrefab.GetWidth(), totalChildrenWidth - _nodeHorizontalSpacing);
        }
        
        /// <summary>
        /// ノード間の接続線を描画
        /// </summary>
        private void DrawNodeConnections()
        {
            // 接続線描画の実装
            // 例: LineRendererを使用、または専用のラインコンポーネントを作成
            // 実装の詳細はゲームの要件によって異なる
        }
        
        /// <summary>
        /// レシピ選択モーダルを表示
        /// </summary>
        /// <param name="node">対象ノード</param>
        /// <param name="recipes">選択可能なレシピリスト</param>
        /// <param name="onRecipeSelect">レシピ選択時のコールバック</param>
        public void ShowRecipeSelectModal(CraftTreeNode node, List<RecipeData> recipes, Action<RecipeData> onRecipeSelect)
        {
            if (_recipeSelectModal == null || node == null || recipes == null)
                return;
                
            _recipeSelectModal.gameObject.SetActive(true);
            _recipeSelectModal.Setup(node, recipes, onRecipeSelect);
        }
        
        /// <summary>
        /// レシピ選択モーダルを非表示
        /// </summary>
        public void HideRecipeSelectModal()
        {
            if (_recipeSelectModal != null)
                _recipeSelectModal.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// コンテキストメニューを表示
        /// </summary>
        /// <param name="actions">アクションリスト</param>
        /// <param name="position">表示位置</param>
        public void ShowContextMenu(List<NodeContextMenuAction> actions, Vector2 position)
        {
            if (_contextMenu == null || actions == null)
                return;
                
            _contextMenu.gameObject.SetActive(true);
            _contextMenu.Setup(actions, position);
        }
        
        /// <summary>
        /// コンテキストメニューを非表示
        /// </summary>
        public void HideContextMenu()
        {
            if (_contextMenu != null)
                _contextMenu.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// アイテム検索UIを表示
        /// </summary>
        /// <param name="categorizedItems">カテゴリ別アイテム一覧</param>
        public void ShowSearchItemUI(Dictionary<ItemCategory, List<ItemId>> categorizedItems)
        {
            if (_searchItemUI == null || categorizedItems == null)
                return;
                
            _searchItemUI.gameObject.SetActive(true);
            _searchItemUI.Setup(categorizedItems, (itemId, count) => 
            {
                HideSearchItemUI();
                OnGoalItemSelect?.Invoke(itemId, count);
            });
        }
        
        /// <summary>
        /// アイテム検索UIを非表示
        /// </summary>
        public void HideSearchItemUI()
        {
            if (_searchItemUI != null)
                _searchItemUI.gameObject.SetActive(false);
        }
        
        /// <summary>
        /// マウス/タッチ入力によるグラフ操作の処理（ドラッグ/ズーム）
        /// </summary>
        private void Update()
        {
            if (_nodesContainer == null)
                return;
                
            // ドラッグによる移動
            if (Input.GetMouseButtonDown(0) && !IsClickingOnUI())
            {
                _isDragging = true;
                _dragStartPosition = Input.mousePosition;
                _dragOffset = _nodesContainer.anchoredPosition;
            }
            else if (Input.GetMouseButton(0) && _isDragging)
            {
                Vector2 currentPosition = Input.mousePosition;
                Vector2 delta = currentPosition - _dragStartPosition;
                _nodesContainer.anchoredPosition = _dragOffset + delta / _currentScale;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
            }
            
            // ズーム処理
            float scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                // マウスの位置をキャンバス上の座標に変換
                Vector2 mousePos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)transform, Input.mousePosition, null, out mousePos);
                
                // ズーム前のコンテナ位置におけるマウス位置
                Vector2 beforeZoom = (mousePos - _nodesContainer.anchoredPosition) / _currentScale;
                
                // スケールの更新
                _currentScale = Mathf.Clamp(_currentScale + scrollDelta * 0.1f, _minScale, _maxScale);
                _nodesContainer.localScale = new Vector3(_currentScale, _currentScale, 1f);
                
                // ズーム後のコンテナ位置におけるマウス位置
                Vector2 afterZoom = beforeZoom * _currentScale;
                
                // コンテナの位置を調整してマウス位置を固定
                _nodesContainer.anchoredPosition = mousePos - afterZoom;
            }
        }
        
        /// <summary>
        /// クリックがUI要素上かどうかを判断
        /// </summary>
        /// <returns>UI要素上の場合はtrue</returns>
        private bool IsClickingOnUI()
        {
            // UI要素の判定（実装は環境によって異なる）
            return false;
        }
    }
}