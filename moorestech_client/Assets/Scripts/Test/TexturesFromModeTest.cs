using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameConst;
using MainGame.ModLoader;
using MainGame.ModLoader.Texture;
using SinglePlay;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Test
{
    public class TexturesFromModeTest : MonoBehaviour
    {
        [SerializeField] private List<Texture2D> textures;

        private void Start()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            var singlePlay = new SinglePlayInterface(ServerConst.ServerModsDirectory);
            Debug.Log("シングルプレイインターフェース作成 " + stopwatch.Elapsed);

            var loadTextureList = ItemTextureLoader.GetItemTexture(ServerConst.ServerModsDirectory, singlePlay);
            
            Debug.Log("テクスチャロード時間 " + stopwatch.Elapsed);


            textures = loadTextureList.Select(i => i.texture2D).ToList();
            
            
            stopwatch.Stop();
            Debug.Log("最終テクスチャロード時間 " + stopwatch.Elapsed);
        }

    }
}