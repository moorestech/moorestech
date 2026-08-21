using System.Collections.Generic;
using Client.Common.Asset;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Source
{
    /// <summary>
    ///     DetailEntryが持つアドレスを実アセットへ解決して設定へ差し戻す。未解決の検知は生成側の
    ///     ThrowIfUnresolvedに任せ、ここは「どのアドレスを引くべきか」だけを判断する
    ///     Resolves the addresses held by DetailEntry into assets and pushes them back into the configs; detecting an
    ///     unresolved asset is left to the generator's ThrowIfUnresolved, so this only decides which addresses to load
    /// </summary>
    public static class DetailAssetResolver
    {
        public static async UniTask ResolveAsync(IReadOnlyList<BiomeDetailConfig> detailConfigs)
        {
            foreach (var detailConfig in detailConfigs)
            foreach (var detailEntry in detailConfig.entries)
            {
                await ResolvePrototypeAsync(detailEntry.prototypeConfig);
                await ResolveTextureFilterAsync(detailEntry.textureFilter);
            }
        }

        // 使わない側のアドレスは空文字が正しい値。空キーをAddressablesへ渡すとInvalidKeyExceptionになる
        // The unused side's address is legitimately empty, and handing an empty key to Addressables raises InvalidKeyException
        private static async UniTask ResolvePrototypeAsync(DetailPrototypeConfig prototypeConfig)
        {
            if (prototypeConfig.usePrototypeMesh)
            {
                prototypeConfig.SetPrototypeMesh(await AddressableLoader.LoadAsyncDefault<GameObject>(prototypeConfig.prototypeMeshAddressablePath));
                return;
            }

            prototypeConfig.SetPrototypeTexture(await AddressableLoader.LoadAsyncDefault<Texture2D>(prototypeConfig.prototypeTextureAddressablePath));
        }

        // 無効フィルタのエントリはEvaluateが一度も読まず、アドレスも空のまま置かれている
        // A disabled filter's entries are never read by Evaluate and are left holding empty addresses
        private static async UniTask ResolveTextureFilterAsync(DetailTextureFilter textureFilter)
        {
            if (!textureFilter.enabled) return;

            foreach (var textureFilterEntry in textureFilter.entries)
                textureFilterEntry.SetLayer(await AddressableLoader.LoadAsyncDefault<TerrainLayer>(textureFilterEntry.layerAddressablePath));
        }
    }
}
