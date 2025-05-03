using System;
using System.Threading;
using Client.Game.InGame.CraftTree.Network;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.Manager
{
    /// <summary>
    /// クラフトツリーの定期的な同期を管理するサービスクラス
    /// </summary>
    public class CraftTreeSyncService : MonoBehaviour
    {
        private ClientCraftTreeManager _craftTreeManager;
        private CraftTreeNetworkService _networkService;
        private float _syncInterval = 30.0f; // デフォルト同期間隔：30秒
        private float _timeSinceLastSync;
        private bool _isSyncing;
        
        /// <summary>
        /// 初期化
        /// </summary>
        /// <param name="craftTreeManager">クラフトツリーマネージャー</param>
        /// <param name="networkService">ネットワークサービス</param>
        public void Initialize(ClientCraftTreeManager craftTreeManager, CraftTreeNetworkService networkService)
        {
            _craftTreeManager = craftTreeManager ?? throw new ArgumentNullException(nameof(craftTreeManager));
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
            _timeSinceLastSync = 0;
        }
        
        /// <summary>
        /// 同期間隔を設定
        /// </summary>
        /// <param name="interval">同期間隔（秒）</param>
        public void SetSyncInterval(float interval)
        {
            if (interval <= 0)
                throw new ArgumentException("Sync interval must be positive", nameof(interval));
                
            _syncInterval = interval;
        }
        
        /// <summary>
        /// 同期を一時停止
        /// </summary>
        public void PauseSync()
        {
            enabled = false;
        }
        
        /// <summary>
        /// 同期を再開
        /// </summary>
        public void ResumeSync()
        {
            enabled = true;
        }
        
        /// <summary>
        /// 強制的に即時同期を実行
        /// </summary>
        public void ForceSyncNow()
        {
            if (_isSyncing || _craftTreeManager == null || _craftTreeManager.CurrentTree == null)
                return;
                
            _isSyncing = true;
            _craftTreeManager.SendTreeToServer();
            _timeSinceLastSync = 0;
            _isSyncing = false;
        }
        
        private void Update()
        {
            // 同期が有効でない、またはマネージャーがない場合は何もしない
            if (!enabled || _craftTreeManager == null || _craftTreeManager.CurrentTree == null)
                return;
                
            // 同期間隔を経過したかどうか
            _timeSinceLastSync += Time.deltaTime;
            
            if (_timeSinceLastSync >= _syncInterval && !_isSyncing)
            {
                // 同期間隔を経過したので同期を実行
                _isSyncing = true;
                
                try
                {
                    // サーバーにツリーを送信
                    _craftTreeManager.SendTreeToServer();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error during craft tree sync: {ex.Message}");
                }
                
                // 同期タイマーをリセット
                _timeSinceLastSync = 0;
                _isSyncing = false;
            }
        }
        
        private void OnApplicationPause(bool pause)
        {
            if (!pause && enabled)
            {
                // アプリケーションがバックグラウンドから復帰したときに強制同期
                ForceSyncNow();
            }
        }
    }
}