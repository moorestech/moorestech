using System;
using UnityEngine;
using VRM;
using VRMShaders;

namespace MainGame.Mod
{
    public class LoadGlb : MonoBehaviour
    {
        private const string path = "./testcube.glb";

        private void Start()
        {
            Load();
        }

        async void Load()
        {
            Debug.Log(path);
            var instance = await VrmUtility.LoadAsync(path, new RuntimeOnlyAwaitCaller());

            instance.ShowMeshes();
        }
    }
}