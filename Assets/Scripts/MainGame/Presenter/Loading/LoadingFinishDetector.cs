using System;
using System.Collections.Generic;
using MainGame.Network.Receive;
using MainGame.UnityView.Game;
using MainGame.UnityView.Util;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Loading
{
    public class LoadingFinishDetector : MonoBehaviour,IInitialViewLoadingDetector
    {
        [SerializeField] private GameObject loadingUI;
        private IPlayerPosition _playerPosition;
        
        [Inject]
        public void Construct(ReceiveInitialHandshakeProtocol receiveInitialHandshakeProtocol,IPlayerPosition playerPosition)
        {
            _playerPosition = playerPosition;
            receiveInitialHandshakeProtocol.OnFinishHandshake += OnFinishHandshake;
        }

        private void OnFinishHandshake(Vector2 playerStartPosition)
        {
            _playerPosition.SetPlayerPosition(playerStartPosition);
            CheckFinishLoading(LoadingElement.Handshake);
        }

        public void FinishItemTextureLoading() { CheckFinishLoading(LoadingElement.ItemTexture); }

        public void FinishMapTileTextureLoading() { CheckFinishLoading(LoadingElement.MapTileTexture); }

        public void FinishBlockModelLoading() { CheckFinishLoading(LoadingElement.BlockModel); }

        
        

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
                loadingUI.SetActive(false);
            }

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