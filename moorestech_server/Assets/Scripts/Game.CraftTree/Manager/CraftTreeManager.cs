using System;
using System.Collections.Generic;
using System.IO;
using Core.Item;
using Core.Master;
using Game.Context;
using Game.CraftTree.Data;
using Game.CraftTree.Data.Transfer;
using Game.CraftTree.Network;
using Game.CraftTree.Utility;
using Game.Paths;
using Newtonsoft.Json;

namespace Game.CraftTree.Manager
{
    /// <summary>
    /// 各プレイヤーのクラフトツリーを管理するマネージャークラス
    /// </summary>
    public class CraftTreeManager
    {
        private Dictionary<PlayerId, Data.CraftTree> _playerCraftTrees;
        private readonly PlayerInventoryService _inventoryService;
        private readonly RecipeService _recipeService;
        private readonly string _saveDirectory;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="inventoryService">インベントリサービス</param>
        /// <param name="recipeService">レシピサービス</param>
        public CraftTreeManager(PlayerInventoryService inventoryService, RecipeService recipeService)
        {
            _playerCraftTrees = new Dictionary<PlayerId, Data.CraftTree>();
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _recipeService = recipeService ?? throw new ArgumentNullException(nameof(recipeService));
            
            // セーブディレクトリを設定
            _saveDirectory = Path.Combine(GameSystemPaths.GetSaveDirectory(), "craft_trees");
            
            // ディレクトリが存在しない場合は作成
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }
            
            // インベントリ変更イベントを購読
            _inventoryService.OnInventoryChanged += OnPlayerInventoryChanged;
        }
        
        /// <summary>
        /// インベントリ変更イベントハンドラ
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        private void OnPlayerInventoryChanged(PlayerId playerId)
        {
            // プレイヤーのクラフトツリー状態を更新
            UpdateTreeState(playerId);
        }
        
        /// <summary>
        /// クライアントから受け取ったツリーデータを適用
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <param name="treeData">クラフトツリーデータ</param>
        public void ApplyCraftTreeFromClient(PlayerId playerId, CraftTreeData treeData)
        {
            // クライアントから受け取ったデータを変換
            var tree = CraftTreeSerializer.Deserialize(treeData);
            if (tree == null)
                return;
                
            _playerCraftTrees[playerId] = tree;
            
            // インベントリに基づいてツリーの状態を即時更新
            UpdateTreeState(playerId);
            
            // 変更を永続化
            SaveCraftTree(playerId);
        }
        
        /// <summary>
        /// プレイヤーのクラフトツリー状態を更新
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        public void UpdateTreeState(PlayerId playerId)
        {
            if (!_playerCraftTrees.TryGetValue(playerId, out var tree))
                return;
                
            // プレイヤーのインベントリを取得
            var inventory = _inventoryService.GetInventoryItems(playerId);
            
            // ツリーの状態を更新
            tree.UpdateTreeState(inventory);
        }
        
        /// <summary>
        /// クライアントへの更新データを取得
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>更新データ</returns>
        public CraftTreeUpdateData GetUpdatesForClient(PlayerId playerId)
        {
            if (!_playerCraftTrees.TryGetValue(playerId, out var tree)) 
                return new CraftTreeUpdateData();
            
            // 目標アイテムを抽出
            var goalItems = tree.ExtractGoalItems();
            
            // プレイヤーインベントリを取得
            var inventory = _inventoryService.GetInventoryItems(playerId);
            
            // 現在クラフト可能なアイテムを抽出
            var craftableItems = tree.ExtractCraftableItems(inventory);
            
            // クライアントへの更新データを作成
            return new CraftTreeUpdateData
            {
                goalItems = goalItems,
                updatedNodes = new List<NodeUpdateData>(), // 差分更新は実装省略
                fullTreeData = CraftTreeSerializer.Serialize(tree)
            };
        }
        
        /// <summary>
        /// プレイヤー用のクラフトツリーを取得または作成
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <returns>クラフトツリー</returns>
        public Data.CraftTree GetOrCreateTreeForPlayer(PlayerId playerId)
        {
            if (_playerCraftTrees.TryGetValue(playerId, out var tree))
                return tree;
                
            // プレイヤー用の初期ツリーを作成
            // 仮のルートノード（実際のゲームロジックでは必要に応じて変更）
            var defaultItemId = new ItemId(1); // 仮のアイテムID
            var rootNode = new CraftTreeNode(defaultItemId, 1);
            var newTree = new Data.CraftTree(rootNode);
            
            _playerCraftTrees[playerId] = newTree;
            return newTree;
        }
        
        /// <summary>
        /// クラフトツリーを永続化
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        public void SaveCraftTree(PlayerId playerId)
        {
            if (!_playerCraftTrees.TryGetValue(playerId, out var tree))
                return;
                
            try
            {
                // ツリーデータをシリアライズ
                var treeData = CraftTreeSerializer.Serialize(tree);
                string jsonData = JsonConvert.SerializeObject(treeData, Formatting.Indented);
                
                // プレイヤー固有のファイルに保存
                string filePath = Path.Combine(_saveDirectory, $"crafttree_{playerId.Value}.json");
                File.WriteAllText(filePath, jsonData);
            }
            catch (Exception ex)
            {
                ServerContext.Logger.LogError($"Failed to save craft tree for player {playerId}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// クラフトツリーを読み込み
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        public void LoadCraftTree(PlayerId playerId)
        {
            try
            {
                // プレイヤー固有のファイルパス
                string filePath = Path.Combine(_saveDirectory, $"crafttree_{playerId.Value}.json");
                
                // ファイルが存在しない場合は初期状態とする
                if (!File.Exists(filePath))
                {
                    return;
                }
                
                // ファイルからデータを読み込み
                string jsonData = File.ReadAllText(filePath);
                var treeData = JsonConvert.DeserializeObject<CraftTreeData>(jsonData);
                
                if (treeData != null)
                {
                    // データをツリーに変換して保存
                    var tree = CraftTreeSerializer.Deserialize(treeData);
                    if (tree != null)
                    {
                        _playerCraftTrees[playerId] = tree;
                    }
                }
            }
            catch (Exception ex)
            {
                ServerContext.Logger.LogError($"Failed to load craft tree for player {playerId}: {ex.Message}");
            }
        }
    }
}