using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using UnityEngine;

namespace Client.Starter.Initialization.Context
{
    /// <summary>
    ///     撮影器を主シーンへ持ち越してクライアントコンテキストを組む
    ///     Carries the photographer into the main scene and composes client context
    /// </summary>
    public static class ClientContextComposer
    {
        public static void Compose(ModAssetLoadResult assets, ServerConnectionResult server, BlockIconImagePhotographer photographer)
        {
            // 被写体を地形や既設ブロックから離した撮影空間へ移す
            // Park subjects away from terrain and placed blocks
            photographer.PrepareForMainScene();
            Object.DontDestroyOnLoad(photographer.gameObject);
            photographer.transform.position = new Vector3(0f, -5000f, 0f);
            new ClientContext(assets.BlockGameObjectPrefabContainer, assets.ItemImageContainer, assets.BlockImageContainer, assets.TrainCarImageContainer, assets.ConnectToolImageContainer, assets.FluidImageContainer, server.PlayerConnectionSetting, server.VanillaApi, photographer);
        }
    }
}
