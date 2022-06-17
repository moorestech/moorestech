using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using MainGame.Network.Receive;
using MainGame.UnityView.Game;
using MainGame.UnityView.Util;
using TMPro;
using UnityEngine;
using VContainer;
using Debug = UnityEngine.Debug;

namespace MainGame.Presenter.Loading
{
    public class LoadingFinishDetector : MonoBehaviour,IInitialViewLoadingDetector
    {
        [SerializeField] private GameObject loadingUI;
        [SerializeField] private TMP_Text loadingLog;
        private IPlayerPosition _playerPosition;

        private readonly Stopwatch _loadingStopwatch = new();
        
        [Inject]
        public void Construct(ReceiveInitialHandshakeProtocol receiveInitialHandshakeProtocol,IPlayerPosition playerPosition)
        {
            _loadingStopwatch.Start();
            _playerPosition = playerPosition;
            receiveInitialHandshakeProtocol.OnFinishHandshake += OnFinishHandshake;
        }

        private void OnFinishHandshake(Vector2 playerStartPosition)
        {
            loadingLog.text += $"\nサーバーハンドシェイク完了 {_loadingStopwatch.Elapsed}";
            _playerPosition.SetPlayerPosition(playerStartPosition);
            CheckFinishLoading(LoadingElement.Handshake);
        }

        public void FinishItemTextureLoading()
        {
            loadingLog.text += $"\nアイテムテクスチャロード完了  {_loadingStopwatch.Elapsed}";
            CheckFinishLoading(LoadingElement.ItemTexture);
        }

        public void FinishMapTileTextureLoading() 
        { 
            loadingLog.text += $"\nタイルテクスチャロード完了  {_loadingStopwatch.Elapsed}";
            CheckFinishLoading(LoadingElement.MapTileTexture); 
        }

        public void FinishBlockModelLoading()
        {
            loadingLog.text += $"\nブロッックモデルロード完了  {_loadingStopwatch.Elapsed}";
            CheckFinishLoading(LoadingElement.BlockModel);
        }

        
        

        private List<LoadingElement> _loadingElements = new();
        private void CheckFinishLoading(LoadingElement loadingElement)
        {
            var index = _loadingElements.IndexOf(loadingElement);
            
            if (index != -1)
            {
                //すでにロードしていたのにまたチェックが入っているのは以上がある
                throw new Exception("同じロード完了メッセージが２回以上きました " + loadingElement);
            }
            _loadingElements.Add(loadingElement);

            
            if (Enum.GetNames(typeof(LoadingElement)).Length == _loadingElements.Count)
            {
                //ロード完了　ここの処理が増えたらイベント化を検討する
                FinishLoading().Forget();
            }
        }

        private async UniTask FinishLoading()
        {
            _loadingStopwatch.Stop();
            Debug.Log("ロード完了　" + _loadingStopwatch.Elapsed);
            await UniTask.Delay(1000);
            loadingUI.SetActive(false);
        }
    }

    enum LoadingElement
    {
        Handshake,
        ItemTexture,
        MapTileTexture,
        BlockModel
    }
}