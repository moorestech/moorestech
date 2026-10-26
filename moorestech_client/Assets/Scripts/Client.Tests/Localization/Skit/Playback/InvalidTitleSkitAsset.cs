using System;
using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.AsyncOperations;
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

        private AsyncOperationHandle<TextAsset> _fixtureHandle;
        private bool _loadCompleted;
        private int _beforeManagerReferenceCount;

        public int ReleaseCount => _provider.ReleaseCount;
        public bool IsAssetDestroyed => !_provider.Asset;

        public InvalidTitleSkitAsset()
        {
            _locations.Add(Address, new ResourceLocationBase(Address, Address, _provider.ProviderId, typeof(TextAsset)));
            Addressables.ResourceManager.ResourceProviders.Add(_provider);
            Addressables.AddResourceLocator(_locations);
        }

        public IEnumerator Initialize()
        {
            // 先行ロードの追跡登録後に完了を受け取る
            // Receive completion after the preload handle has been tracked
            _fixtureHandle = Addressables.LoadAssetAsync<TextAsset>(Address);
            _fixtureHandle.Completed += OnLoadCompleted;
            while (!_loadCompleted) yield return null;
            Assert.AreEqual(AsyncOperationStatus.Succeeded, _fixtureHandle.Status);
            _beforeManagerReferenceCount = GetReferenceCount();
        }

        public void AssertManagerOwnsCachedReference()
        {
            // 同一operationとmanager分の参照増加を確かめる
            // Verify the same cached operation and the manager reference increment
            var afterManagerReferenceCount = GetReferenceCount();
            var probe = Addressables.LoadAssetAsync<TextAsset>(Address);
            Assert.IsTrue(probe.Equals(_fixtureHandle));
            Addressables.Release(probe);
            Assert.GreaterOrEqual(afterManagerReferenceCount, _beforeManagerReferenceCount + 1);
            Debug.Log($"Skit fixture handle={_fixtureHandle.GetHashCode()} beforeManager={_beforeManagerReferenceCount} afterManager={afterManagerReferenceCount} sameCachedOperation=True");
        }

        public UniTask WaitForReleaseAsync() => _provider.Released.Task;

        public void Dispose()
        {
            // managerのロード参照とfixture自身の参照を各一度解放する
            // Release the manager load reference and the fixture reference once each
            Assert.IsTrue(_fixtureHandle.IsValid());
            Addressables.Release(_fixtureHandle);
            Assert.IsTrue(_fixtureHandle.IsValid());
            Addressables.Release(_fixtureHandle);
            Addressables.RemoveResourceLocator(_locations);
            Addressables.ResourceManager.ResourceProviders.Remove(_provider);
        }

        private int GetReferenceCount()
        {
            return (int)typeof(AsyncOperationHandle<TextAsset>)
                .GetProperty("ReferenceCount", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_fixtureHandle);
        }

        private void OnLoadCompleted(AsyncOperationHandle<TextAsset> handle)
        {
            _loadCompleted = true;
        }

        private sealed class InvalidTitleProvider : ResourceProviderBase
        {
            public readonly TextAsset Asset = new("[]");
            public readonly UniTaskCompletionSource Released = new();
            public int ReleaseCount { get; private set; }

            public override void Provide(ProvideHandle provideHandle)
            {
                provideHandle.Complete(Asset, true, null);
            }

            public override void Release(IResourceLocation location, object asset)
            {
                // 全参照が消えた時だけ資産を破棄する
                // Destroy the asset only when all handle references are released
                ReleaseCount++;
                UnityEngine.Object.DestroyImmediate(Asset);
                Released.TrySetResult();
            }
        }
    }
}
