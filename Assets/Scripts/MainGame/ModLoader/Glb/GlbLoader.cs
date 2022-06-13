using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VRM;
using VRMShaders;

namespace MainGame.ModLoader.Glb
{
    public class GlbLoader
    {
        public static async UniTask<GameObject> Load(string extractedModDirectory,string path)
        {
            var instance = await VrmUtility.LoadAsync(Path.Combine(extractedModDirectory, path), new RuntimeOnlyAwaitCaller());

            instance.ShowMeshes();

            return instance.gameObject;
        }
    }
}