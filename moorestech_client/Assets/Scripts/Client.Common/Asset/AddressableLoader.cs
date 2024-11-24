using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace Client.Common.Asset
{
    public class AddressableLoader
    {
        public static async UniTask<T> LoadAsync<T>(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return default;
            }
            
            var handle = Addressables.LoadAssetAsync<T>(address);
            await handle.Task;
            
            return handle.Result;
        }
        
        public static T Load<T>(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return default;
            }
            
            var handle = Addressables.LoadAssetAsync<T>(address);
            handle.WaitForCompletion();
            
            return handle.Result;
        }
        
        public static bool TryLoad<T>(string address, out T result)
        {
            if (string.IsNullOrEmpty(address))
            {
                result = default;
                return false;
            }
            
            var handle = Addressables.LoadAssetAsync<T>(address);
            if (!handle.IsDone)
            {
                result = default;
                return false;
            }
            
            result = handle.Result;
            return true;
        }
    }
}