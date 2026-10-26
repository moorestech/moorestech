using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Client.Tests.Localization.Skit.Playback
{
    // 公開ロード経路へ不正タイトルを供給する
    // Supply an invalid title through the public asset-loading path
    internal sealed class InvalidTitleSkitAsset : IDisposable
    {
        public const string Address = "test-invalid-title-skit";
        private readonly ResourceLocationMap _locations = new(Address);
        private readonly InvalidTitleProvider _provider = new();

        public InvalidTitleSkitAsset()
        {
            _locations.Add(Address, new ResourceLocationBase(Address, Address, _provider.ProviderId, typeof(TextAsset)));
            Addressables.ResourceManager.ResourceProviders.Add(_provider);
            Addressables.AddResourceLocator(_locations);
        }

        public void Dispose()
        {
            // テスト用登録とロード資産を残さない
            // Remove test registrations and release the loaded asset
            Addressables.Release(_provider.Asset);
            Addressables.RemoveResourceLocator(_locations);
            Addressables.ResourceManager.ResourceProviders.Remove(_provider);
            UnityEngine.Object.DestroyImmediate(_provider.Asset);
        }

        private sealed class InvalidTitleProvider : ResourceProviderBase
        {
            public readonly TextAsset Asset = new("[]");

            public override void Provide(ProvideHandle provideHandle)
            {
                provideHandle.Complete(Asset, true, null);
            }
        }
    }
}
