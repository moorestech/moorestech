using System.Collections.Generic;
using Client.Common.Asset;
using Cysharp.Threading.Tasks;
using Game.MapGeneration.Facade;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Source
{
    /// <summary>
    ///     detailプロトタイプ仕様が持つアドレスを実アセットへ解決する。アドレスをキーにした辞書で返し、
    ///     未解決の検知と組み立ては呼び出し側（TerrainDetailPrototypeList）に任せる
    ///     Resolves the addresses a detail prototype spec holds into assets, returned in a dictionary keyed by address;
    ///     detecting an unresolved asset and assembling the prototype are left to the caller (TerrainDetailPrototypeList)
    /// </summary>
    public static class DetailAssetResolver
    {
        public static async UniTask<Dictionary<string, Object>> ResolveAsync(IReadOnlyList<DetailPrototypeSpec> prototypeSpecs)
        {
            var resolvedAssets = new Dictionary<string, Object>();
            foreach (var spec in prototypeSpecs)
            {
                // 使わない側のアドレスは空文字が正しい値。空キーをAddressablesへ渡すとInvalidKeyExceptionになる
                // The unused side's address is legitimately empty, and handing an empty key to Addressables raises InvalidKeyException
                var address = spec.usePrototypeMesh ? spec.prototypeMeshAddressablePath : spec.prototypeTextureAddressablePath;
                if (resolvedAssets.ContainsKey(address)) continue;

                resolvedAssets[address] = spec.usePrototypeMesh
                    ? await AddressableLoader.LoadAsyncDefault<GameObject>(address)
                    : await AddressableLoader.LoadAsyncDefault<Texture2D>(address);
            }

            return resolvedAssets;
        }
    }
}
