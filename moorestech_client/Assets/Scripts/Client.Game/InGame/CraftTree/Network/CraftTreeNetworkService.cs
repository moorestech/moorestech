using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Network;
using Core.Item;
using Game.CraftTree.Data;
using Game.CraftTree.Network;
using MessagePack;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.Network
{
    /// <summary>
    /// クラフトツリー関連のサーバー通信を担当するサービスクラス
    /// </summary>
    public class CraftTreeNetworkService
    {
        private readonly IVanillaApi _vanillaApi;
        private Action<CraftTreeUpdateData> _onUpdateReceived;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="vanillaApi">サーバー通信API</param>
        public CraftTreeNetworkService(IVanillaApi vanillaApi)
        {
            _vanillaApi = vanillaApi ?? throw new ArgumentNullException(nameof(vanillaApi));
            
            // サーバーからのイベント通知を受け取るよう登録
            _vanillaApi.Event.SubscribeEventResponse(
                CraftTreeUpdateEventMessagePack.EventTag, 
                OnCraftTreeUpdateEvent);
        }
        
        /// <summary>
        /// サーバーからの更新通知を処理するハンドラを登録
        /// </summary>
        /// <param name="onUpdateReceived">更新処理ハンドラ</param>
        public void RegisterForServerUpdates(Action<CraftTreeUpdateData> onUpdateReceived)
        {
            _onUpdateReceived = onUpdateReceived;
        }
        
        /// <summary>
        /// サーバーからのクラフトツリー更新イベント処理
        /// </summary>
        /// <param name="payload">イベントペイロード</param>
        private void OnCraftTreeUpdateEvent(byte[] payload)
        {
            try
            {
                var updateEvent = MessagePackSerializer.Deserialize<CraftTreeUpdateEventMessagePack>(payload);
                var updateData = updateEvent.ToUpdateData();
                
                // 登録済みハンドラを呼び出し
                _onUpdateReceived?.Invoke(updateData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to process craft tree update event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// クラフトツリーをサーバーに送信
        /// </summary>
        /// <param name="tree">送信するクラフトツリー</param>
        public async Task SendCraftTree(Game.CraftTree.Data.CraftTree tree)
        {
            if (tree == null)
                return;
                
            try
            {
                // ツリーをシリアライズ
                var treeData = Game.CraftTree.Utility.CraftTreeSerializer.Serialize(tree);
                
                // サーバーにツリーデータを送信
                var response = await _vanillaApi.Response.ApplyCraftTree(treeData);
                
                // 成功時の処理（必要に応じて追加）
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send craft tree to server: {ex.Message}");
            }
        }
        
        /// <summary>
        /// アイテム用のレシピリストをサーバーから取得
        /// </summary>
        /// <param name="itemId">アイテムID</param>
        /// <returns>レシピデータのリスト</returns>
        public async Task<List<RecipeData>> GetRecipesForItem(ItemId itemId)
        {
            try
            {
                // サーバーからレシピを取得
                var recipes = await _vanillaApi.Response.GetRecipesForItem(itemId);
                return recipes;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get recipes for item {itemId}: {ex.Message}");
                return new List<RecipeData>();
            }
        }
    }
}