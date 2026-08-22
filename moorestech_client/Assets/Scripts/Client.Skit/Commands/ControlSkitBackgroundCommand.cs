using System.Collections.Generic;
using Client.Common.Asset;
using Client.Skit.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CommandForgeGenerator.Command
{
    public interface ISkitEnvironmentManager
    {
        // 受け口を相対位置型に限定し、原点加算の抜けをコンパイルエラーへ落とす（ADR 0029）
        // Accept only the relative-position type so a missing origin addition becomes a compile error (ADR 0029)
        UniTask AddEnvironmentAsync(string addressablePath, SkitRelativePosition position, Vector3 rotation);
        void RemoveEnvironment(string addressablePath);
    }
    
    public partial class ControlSkitBackgroundCommand
    {
        public async UniTask<CommandResultContext> ExecuteAsync(StoryContext storyContext)
        {
            var environmentManager = storyContext.GetService<ISkitEnvironmentManager>();
            
            if (Action == "Add")
            {
                await environmentManager.AddEnvironmentAsync(SkitEnvironmentAddressablePath, new SkitRelativePosition(Position), Rotation);
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
        private readonly SkitOrigin _skitOrigin;
        
        public SkitEnvironmentManager(Transform environmentParent, SkitOrigin skitOrigin)
        {
            _environmentParent = environmentParent;
            _skitOrigin = skitOrigin;
        }
        
        public async UniTask AddEnvironmentAsync(string addressablePath, SkitRelativePosition position, Vector3 rotation)
        {
            if (string.IsNullOrEmpty(addressablePath))
                return;
            
            if (_loadedEnvironments.ContainsKey(addressablePath))
                return;
            
            var loadedAsset = await AddressableLoader.LoadAsync<GameObject>(addressablePath);
            if (loadedAsset?.Asset == null)
                return;
            
            var instance = Object.Instantiate(loadedAsset.Asset, _environmentParent);
            PlaceInWorld(instance.transform, position.ToWorld(_skitOrigin), rotation);
            _loadedEnvironments[addressablePath] = instance;
        }
        
        // 親追従はやめてワールド固定にする。位置はSkitOriginで解決済みで、親を二重に効かせるとカメラ・キャラとずれる
        // Drop parent-following and fix in world space; positions are already resolved by SkitOrigin, and letting the parent apply twice would desync from camera and characters
        public static void PlaceInWorld(Transform instance, Vector3 worldPosition, Vector3 rotation)
        {
            instance.position = worldPosition;
            instance.rotation = Quaternion.Euler(rotation);
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