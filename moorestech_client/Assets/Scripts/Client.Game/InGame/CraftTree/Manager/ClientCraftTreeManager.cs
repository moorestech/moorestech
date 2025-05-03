using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Game.InGame.CraftTree.Network;
using Core.Item;
using Game.CraftTree.Data;
using Game.CraftTree.Network;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.Manager
{
    /// <summary>
    /// クライアント側のクラフトツリーを管理するマネージャクラス
    /// </summary>
    public class ClientCraftTreeManager
    {
        private Game.CraftTree.Data.CraftTree _currentTree;
        private readonly CraftTreeNetworkService _networkService;
        private readonly ClientInventoryService _inventoryService;
        
        /// <summary>
        /// 現在のツリー
        /// </summary>
        public Game.CraftTree.Data.CraftTree CurrentTree => _currentTree;
        
        /// <summary>
        /// ツリー更新時のイベント
        /// </summary>
        public event Action OnTreeUpdated;
        
        /// <summary>
        /// 目標アイテム更新時のイベント
        /// </summary>
        public event Action<List<GoalItem>> OnGoalItemsUpdated;
        
        /// <summary>
        /// クラフト可能アイテム更新時のイベント
        /// </summary>
        public event Action<List<GoalItem>> OnCraftableItemsUpdated;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="networkService">ネットワークサービス</param>
        /// <param name="inventoryService">インベントリサービス</param>
        public ClientCraftTreeManager(
            CraftTreeNetworkService networkService, 
            ClientInventoryService inventoryService)
        {
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            
            // サーバーからの更新通知を処理するハンドラを登録
            _networkService.RegisterForServerUpdates(ReceiveServerUpdate);
            
            // インベントリ変更時にローカルのツリー状態を更新
            _inventoryService.OnInventoryChanged += () => 
            {
                if (_currentTree != null)
                {
                    UpdateLocalTreeState();
                }
            };
        }
        
        /// <summary>
        /// 目的アイテムを設定
        /// </summary>
        /// <param name="itemId">アイテムID</param>
        /// <param name="count">必要数量</param>
        public void SetGoalItem(ItemId itemId, int count)
        {
            if (count <= 0)
            {
                Debug.LogWarning("Goal item count must be positive");
                return;
            }
            
            // 新しいツリーのルートノードを作成
            var rootNode = new CraftTreeNode(itemId, count);
            
            // 新しいCraftTreeを作成
            _currentTree = new Game.CraftTree.Data.CraftTree(rootNode);
            
            // UIを更新
            OnTreeUpdated?.Invoke();
            
            // ツリーをサーバーに送信
            SendTreeToServer();
        }
        
        /// <summary>
        /// ノードを展開（子ノードを表示）
        /// </summary>
        /// <param name="node">展開対象ノード</param>
        public void ExpandNode(CraftTreeNode node)
        {
            if (node == null)
                return;
                
            // ノードが既に展開されている場合は何もしない
            if (node.children.Count > 0)
                return;
                
            // サーバーからノード用のレシピを取得
            _networkService.GetRecipesForItem(node.itemId)
                .ContinueWith(task =>
                {
                    // メインスレッドで結果を処理
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        if (task.IsFaulted || task.Result == null)
                        {
                            Debug.LogError($"Failed to get recipes: {task.Exception?.Message}");
                            return;
                        }
                        
                        var recipes = task.Result;
                        if (recipes.Count == 1)
                        {
                            // レシピが1つだけなら自動選択
                            SelectRecipe(node, recipes[0]);
                        }
                        else if (recipes.Count > 1)
                        {
                            // UIコントローラー側でレシピ選択UIを表示するためのイベントを発火
                            // ここではイベント定義のみ（UIコントローラーにて実装）
                            // OnRecipeSelectionNeeded?.Invoke(node, recipes);
                        }
                        else
                        {
                            // レシピがない場合は原材料アイテムとして扱う
                            Debug.Log($"No recipes for item {node.itemId}. Treating as base material.");
                        }
                    });
                });
        }
        
        /// <summary>
        /// レシピを選択
        /// </summary>
        /// <param name="node">対象ノード</param>
        /// <param name="recipe">選択レシピ</param>
        public void SelectRecipe(CraftTreeNode node, RecipeData recipe)
        {
            if (node == null || recipe == null)
                return;
                
            // 以前のレシピがあれば、子ノードをクリア
            node.children.Clear();
            
            // 新しいレシピを設定
            node.selectedRecipe = recipe;
            
            // レシピの材料に基づいて子ノードを作成
            foreach (var ingredient in recipe.inputs)
            {
                var childNode = new CraftTreeNode(ingredient.itemId, ingredient.count);
                childNode.parent = node;
                node.children.Add(childNode);
            }
            
            // ツリー全体の状態を更新
            UpdateLocalTreeState();
            
            // ツリーをサーバーに送信
            SendTreeToServer();
        }
        
        /// <summary>
        /// コンテキストメニューアクションのリストを取得
        /// </summary>
        /// <param name="node">アクション対象ノード</param>
        /// <returns>アクションリスト</returns>
        public List<NodeContextMenuAction> GetContextMenuActions(CraftTreeNode node)
        {
            var actions = new List<NodeContextMenuAction>();
            
            if (node == null)
                return actions;
                
            if (node.state == NodeState.Completed)
            {
                // 完了状態のノードのアクション
                actions.Add(new NodeContextMenuAction(
                    "レシピ変更", 
                    () => 
                    {
                        // このアクションが選択されたときの処理は後で実装
                        // レシピ選択UIを表示するなど
                    },
                    true
                ));
                
                actions.Add(new NodeContextMenuAction(
                    "ここから上を未完了", 
                    () => 
                    {
                        MarkNodeUncompleted(node);
                    },
                    true
                ));
            }
            else
            {
                // 未完了状態のノードのアクション
                actions.Add(new NodeContextMenuAction(
                    "レシピ変更", 
                    () => 
                    {
                        // レシピ変更アクション
                    },
                    true
                ));
                
                actions.Add(new NodeContextMenuAction(
                    "下位を完了にする", 
                    () => 
                    {
                        MarkNodeCompleted(node);
                    },
                    true
                ));
            }
            
            return actions;
        }
        
        /// <summary>
        /// ノードとその子孫を完了状態にマーク
        /// </summary>
        /// <param name="node">対象ノード</param>
        public void MarkNodeCompleted(CraftTreeNode node)
        {
            if (node == null)
                return;
                
            // ノードとその子孫を完了状態にマーク
            MarkNodeCompletedRecursive(node);
            
            // UIを更新
            OnTreeUpdated?.Invoke();
            
            // ツリーをサーバーに送信
            SendTreeToServer();
        }
        
        /// <summary>
        /// ノードとその子孫を再帰的に完了状態にマーク
        /// </summary>
        /// <param name="node">対象ノード</param>
        private void MarkNodeCompletedRecursive(CraftTreeNode node)
        {
            node.state = NodeState.Completed;
            
            foreach (var child in node.children)
            {
                MarkNodeCompletedRecursive(child);
            }
        }
        
        /// <summary>
        /// ノードとその先祖を未完了状態にマーク
        /// </summary>
        /// <param name="node">対象ノード</param>
        public void MarkNodeUncompleted(CraftTreeNode node)
        {
            if (node == null)
                return;
                
            // ノードを未完了状態にマーク
            node.state = NodeState.Incomplete;
            
            // 親ノードも連鎖的に未完了状態にマーク
            var current = node.parent;
            while (current != null)
            {
                current.state = NodeState.Incomplete;
                current = current.parent;
            }
            
            // UIを更新
            OnTreeUpdated?.Invoke();
            
            // ツリーをサーバーに送信
            SendTreeToServer();
        }
        
        /// <summary>
        /// ツリーをサーバーに送信
        /// </summary>
        public void SendTreeToServer()
        {
            if (_currentTree == null)
                return;
                
            _networkService.SendCraftTree(_currentTree)
                .ContinueWith(task =>
                {
                    // メインスレッドでエラー処理
                    if (task.IsFaulted)
                    {
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            Debug.LogError($"Failed to send tree: {task.Exception?.Message}");
                        });
                    }
                });
        }
        
        /// <summary>
        /// サーバーからの更新を受信して適用
        /// </summary>
        /// <param name="updateData">更新データ</param>
        public void ReceiveServerUpdate(CraftTreeUpdateData updateData)
        {
            if (updateData == null)
                return;
                
            if (updateData.fullTreeData != null)
            {
                // サーバーから完全なツリーデータを受信
                _currentTree = Game.CraftTree.Utility.CraftTreeSerializer.Deserialize(updateData.fullTreeData);
                
                // UI更新
                OnTreeUpdated?.Invoke();
            }
            else if (updateData.updatedNodes != null && updateData.updatedNodes.Count > 0)
            {
                // 部分的な更新の適用
                foreach (var nodeUpdate in updateData.updatedNodes)
                {
                    if (_currentTree == null)
                        continue;
                        
                    // 該当ノードを探して状態を更新
                    if (_currentTree.nodesByItemId.TryGetValue(nodeUpdate.nodeItemId, out var nodes))
                    {
                        foreach (var node in nodes)
                        {
                            node.state = nodeUpdate.newState;
                            // 進捗も更新
                        }
                    }
                }
                
                // UI更新
                OnTreeUpdated?.Invoke();
            }
            
            // 目標アイテムとクラフト可能アイテムを更新
            if (updateData.goalItems != null)
            {
                OnGoalItemsUpdated?.Invoke(updateData.goalItems);
            }
        }
        
        /// <summary>
        /// ローカルツリーの状態を更新
        /// </summary>
        private void UpdateLocalTreeState()
        {
            if (_currentTree == null)
                return;
                
            // プレイヤーのインベントリを取得
            var inventory = _inventoryService.GetInventoryItems();
            
            // ツリーの状態を更新
            _currentTree.UpdateTreeState(inventory);
            
            // 目標アイテムと製作可能アイテムを抽出
            var goalItems = _currentTree.ExtractGoalItems();
            var craftableItems = _currentTree.ExtractCraftableItems(inventory);
            
            // イベント発火
            OnTreeUpdated?.Invoke();
            OnGoalItemsUpdated?.Invoke(goalItems);
            OnCraftableItemsUpdated?.Invoke(craftableItems);
        }
    }
    
    /// <summary>
    /// コンテキストメニューアクション
    /// </summary>
    public class NodeContextMenuAction
    {
        /// <summary>
        /// アクション名称
        /// </summary>
        public string name { get; }
        
        /// <summary>
        /// 実行アクション
        /// </summary>
        public Action action { get; }
        
        /// <summary>
        /// アクションが有効かどうか
        /// </summary>
        public bool isEnabled { get; }
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="name">アクション名称</param>
        /// <param name="action">実行アクション</param>
        /// <param name="isEnabled">アクションが有効かどうか</param>
        public NodeContextMenuAction(string name, Action action, bool isEnabled)
        {
            this.name = name;
            this.action = action;
            this.isEnabled = isEnabled;
        }
    }
    
    /// <summary>
    /// メインスレッドでの実行をディスパッチするためのヘルパークラス
    /// 実際の実装では、UniTaskやUniRxのMainThreadSchedulerなどを使用することを推奨
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private readonly Queue<Action> _executionQueue = new Queue<Action>();
        
        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance == null)
            {
                // 実際の実装ではもっと堅牢に
                var go = new GameObject("UnityMainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
        
        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }
        
        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }
    }
}