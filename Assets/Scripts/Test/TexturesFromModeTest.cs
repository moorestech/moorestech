using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameConst;
using MainGame.ModLoader;
using MainGame.ModLoader.Texture;
using SinglePlay;
using UnityEngine;

namespace Test
{
    public class TexturesFromModeTest : MonoBehaviour
    {
        [SerializeField] private List<Texture2D> textures;

        private async void Start()
        {
            var loadTextureList = await ItemTextureLoader.GetItemTexture(ServerConst.ServerModsDirectory,
                new SinglePlayInterface(ServerConst.ServerModsDirectory));
            textures = loadTextureList.Select(i => i.texture2D).ToList();
        }
    }
}