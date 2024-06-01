﻿using System.IO;
using Cysharp.Threading.Tasks;
using UniGLTF;
using UnityEngine;
using VRMShaders;

namespace Client.Mod.Glb
{
    public class GlbLoader
    {
        public static async UniTask<GameObject> Load(string extractedModDirectory, string path)
        {
            var fullPath = Path.Combine(extractedModDirectory, path);
            if (!File.Exists(fullPath))
            {
                Debug.Log($"{fullPath} not found");
                return null;
            }


            var awaitCaller = new ImmediateCaller();
            using var data = new AutoGltfFileParser(fullPath).Parse();

            using var loader = new ImporterContext(data);
            var load = await loader.LoadAsync(awaitCaller);
            load.ShowMeshes();

            return load.Root;
        }
    }
}