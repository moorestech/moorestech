using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Client.Common.Asset
{
    public class AddressableLoader
    {
        public static async UniTask<LoadedAsset<T>> LoadAsync<T>(string address) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(address))
            {
                return null;
            }
            
            var handle = Addressables.LoadAssetAsync<T>(address);
            await handle.Task;
            
            return handle.Status == AsyncOperationStatus.Succeeded ? new LoadedAsset<T>(handle.Result) : null;
        }
    }
    
    public class LoadedAsset<T> : IDisposable where T : UnityEngine.Object
    {
        public T Asset { get; }
        
        public LoadedAsset(T asset)
        {
            Asset = asset;
        }
        
        public void Dispose()
        {
            Addressables.Release(Asset);
        }
    }
}