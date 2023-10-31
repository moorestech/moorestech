using System.IO;
using Cysharp.Threading.Tasks;
using UniGLTF;
using UnityEngine;
using VRM;
using VRMShaders;

namespace MainGame.ModLoader.Glb
{
    public class GlbLoader
    {
        public static async UniTask<GameObject> Load(string extractedModDirectory,string path)
        {
            var fullPath = Path.Combine(extractedModDirectory, path);
            if (!File.Exists(fullPath))
            {
                Debug.Log($"{fullPath} not found");
                return null;
            }
            
            
            var awaitCaller = new ImmediateCaller();
            using GltfData data = new AutoGltfFileParser(fullPath).Parse();
            
            using var loader = new ImporterContext(data);
            var load = await loader.LoadAsync(awaitCaller);
            load.ShowMeshes();

            return load.Root;
        }
    }
}