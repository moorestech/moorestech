using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Client.Common.Asset
{
    public class AddressableLoader
    {
        
        public static LoadedAsset<T> Load<T>(string address) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(address)) return null;
            
            var handle = Addressables.LoadAssetAsync<T>(address);
            try
            {
                handle.WaitForCompletion();   // 完了するまでブロック
            }
            catch (Exception e)
            {
                Debug.LogError($"Addressables Load Error: {address}\n{e.Message}\n{e.StackTrace}");
                return null;
            }
            
            return handle.Status == AsyncOperationStatus.Succeeded ? new LoadedAsset<T>(handle.Result) : null;
        }
        
        public static T LoadDefault<T>(string address) where T : UnityEngine.Object
        {
            var loadedAsset = Load<T>(address);
            return loadedAsset?.Asset;
        }
        
        
        public static async UniTask<LoadedAsset<T>> LoadAsync<T>(string address, CancellationToken ct = default) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(address)) return null;
            
            var handle = Addressables.LoadAssetAsync<T>(address);
            try
            {
                await handle.ToUniTask(cancellationToken: ct);
            }
            catch (Exception e)
            {
                Debug.LogError($"Addressables Load Error: {address}\n{e.Message}\n{e.StackTrace}");
                return null;
            }
            
            return handle.Status == AsyncOperationStatus.Succeeded ? new LoadedAsset<T>(handle.Result) : null;
        }
        
        public static async UniTask<T> LoadAsyncDefault<T>(string address, CancellationToken ct = default) where T : UnityEngine.Object
        {
            var loadedAsset = await LoadAsync<T>(address, ct);
            return loadedAsset?.Asset;
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