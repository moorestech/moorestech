using System.Collections.Generic;
using Client.Common.Asset;
using Client.Skit.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public interface ISkitEnvironmentManager
    {
        UniTask AddEnvironmentAsync(string addressablePath, Vector3 position, Vector3 rotation);
        void RemoveEnvironment(string addressablePath);
    }
    
    public partial class ControlSkitBackgroundCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var environmentManager = storyContext.GetService<ISkitEnvironmentManager>();
            
            if (Action == "Add")
            {
                // JSONの位置はスポーン基準の相対値なので原点を足してワールド座標へ
                // JSON positions are spawn-relative, so add the origin to reach world space
                var origin = storyContext.GetSkitOrigin();
                await environmentManager.AddEnvironmentAsync(SkitEnvironmentAddressablePath, origin.ToWorld(Position), Rotation);
            }
            else if (Action == "Remove")
            {
                environmentManager.RemoveEnvironment(SkitEnvironmentAddressablePath);
            }
            
            return null;
        }
    }
    
    public class SkitEnvironmentManager : ISkitEnvironmentManager
    {
        private readonly Dictionary<string, GameObject> _loadedEnvironments = new();
        private readonly Transform _environmentParent;
        
        public SkitEnvironmentManager(Transform environmentParent)
        {
            _environmentParent = environmentParent;
        }
        
        public async UniTask AddEnvironmentAsync(string addressablePath, Vector3 position, Vector3 rotation)
        {
            if (string.IsNullOrEmpty(addressablePath))
                return;
            
            if (_loadedEnvironments.ContainsKey(addressablePath))
                return;
            
            var loadedAsset = await AddressableLoader.LoadAsync<GameObject>(addressablePath);
            if (loadedAsset?.Asset == null)
                return;
            
            var instance = Object.Instantiate(loadedAsset.Asset, _environmentParent);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(rotation);
            _loadedEnvironments[addressablePath] = instance;
        }
        
        public void RemoveEnvironment(string addressablePath)
        {
            if (string.IsNullOrEmpty(addressablePath))
                return;
            
            if (!_loadedEnvironments.TryGetValue(addressablePath, out var environment))
                return;
            
            if (environment != null)
                Object.Destroy(environment);
            
            _loadedEnvironments.Remove(addressablePath);
        }
    }
}