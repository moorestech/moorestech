using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using MainGame.Network.Receive;
using MainGame.UnityView.Block;
using MainGame.UnityView.Game;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.WorldMapTile;
using TMPro;
using UnityEngine;
using VContainer;
using Debug = UnityEngine.Debug;

namespace MainGame.Presenter.Loading
{
    public class LoadingFinishDetector : MonoBehaviour
    {
        [SerializeField] private GameObject loadingUI;
        [SerializeField] private TMP_Text loadingLog;


        private readonly List<LoadingElement> _loadingElements = new();

        private readonly Stopwatch _loadingStopwatch = new();
        private IPlayerPosition _playerPosition;

        [Inject]
        public void Construct(ReceiveInitialHandshakeProtocol receiveInitialHandshakeProtocol, IPlayerPosition playerPosition, BlockGameObjectFactory blockGameObjectFactory, ItemImages itemImages, WorldMapTileMaterials worldMapTileMaterials)
        {
            _loadingStopwatch.Start();
            _playerPosition = playerPosition;

            loadingUI.SetActive(true);

            receiveInitialHandshakeProtocol.OnFinishHandshake += OnFinishHandshake;
            blockGameObjectFactory.OnLoadFinished += FinishBlockModelLoading;
            itemImages.OnLoadFinished += FinishItemTextureLoading;
            worldMapTileMaterials.OnLoadFinished += FinishMapTileTextureLoading;
        }

        private void OnFinishHandshake(Vector2 playerStartPosition)
        {
            loadingLog.text += $"\nサーバーハンドシェイク完了 {_loadingStopwatch.Elapsed}";

            //プレイヤーのオブジェクトにポジションをセットする
            //この関数自体はawait UniTask.SwitchToMainThread(); のあと呼ばれているが、
            //プレイヤーに座標をセットした後ThirdPersonControllerによる謎の「戻し」が発生する
            //そのため、あらかじめオフにしていたプレイヤーを、座標を設定した後オンにして一旦解決とする
            _playerPosition.SetPlayerPosition(playerStartPosition);
            _playerPosition.SetActive(true);

            CheckFinishLoading(LoadingElement.Handshake);
        }

        private void FinishItemTextureLoading()
        {
            loadingLog.text += $"\nアイテムテクスチャロード完了  {_loadingStopwatch.Elapsed}";
            CheckFinishLoading(LoadingElement.ItemTexture);
        }

        private void FinishMapTileTextureLoading()
        {
            loadingLog.text += $"\nタイルテクスチャロード完了  {_loadingStopwatch.Elapsed}";
            CheckFinishLoading(LoadingElement.MapTileTexture);
        }

        private void FinishBlockModelLoading()
        {
            loadingLog.text += $"\nブロッックモデルロード完了  {_loadingStopwatch.Elapsed}";
            CheckFinishLoading(LoadingElement.BlockModel);
        }

        private void CheckFinishLoading(LoadingElement loadingElement)
        {
            var index = _loadingElements.IndexOf(loadingElement);

            if (index != -1)
                //すでにロードしていたのにまたチェックが入っているのは以上がある
                throw new Exception("同じロード完了メッセージが２回以上きました " + loadingElement);
            _loadingElements.Add(loadingElement);


            if (Enum.GetNames(typeof(LoadingElement)).Length == _loadingElements.Count)
                //ロード完了　ここの処理が増えたらイベント化を検討する
                FinishLoading().Forget();
        }

        private async UniTask FinishLoading()
        {
            _loadingStopwatch.Stop();
            Debug.Log("ロード完了　" + _loadingStopwatch.Elapsed);
            await UniTask.Delay(1000);
            loadingUI.SetActive(false);
        }
    }

    internal enum LoadingElement
    {
        Handshake,
        ItemTexture,
        MapTileTexture,
        BlockModel
    }
}