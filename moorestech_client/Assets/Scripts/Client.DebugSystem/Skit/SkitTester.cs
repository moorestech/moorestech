using System;
using System.Collections.Generic;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.Block;
using Client.Game.InGame.Environment;
using Client.Game.InGame.Skit;
using Client.Game.InGame.World;
using Client.Game.Skit;
using Client.Network.API;
using Client.Skit.Skit;
using Client.Skit.UI;
using Core.Item.Interface;
using Game.CraftTree.Models;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Client.DebugSystem.Skit
{
    public class SkitTester : MonoBehaviour
    {
        [SerializeField] private SkitManager skitManager;
        [SerializeField] private BlockGameObjectDataStore blockGameObjectDataStore;
        [SerializeField] private EnvironmentRoot environmentRoot;
        [SerializeField] private SkitUI skitUI;
        
        private IObjectResolver _resolver;
        
        private void Awake()
        {
            // DIコンテナでSkitManagerの依存を解決
            var builder = new ContainerBuilder();
            
            // 必要な依存関係の登録
            builder.Register<ISkitActionContext, SkitActionContext>(Lifetime.Singleton);
            
            // Hierarchy上のコンポーネントを登録
            builder.RegisterComponent(skitManager);
            builder.RegisterComponent(blockGameObjectDataStore);
            builder.RegisterComponent(environmentRoot);
            
            
            // SkitFireManagerを登録
            builder.Register<SkitFireManager>(Lifetime.Singleton);
            
            // 依存関係を解決
            _resolver = builder.Build();
            _resolver.Inject(skitManager);
        }
        

        
        private void OnDestroy()
        {
            _resolver?.Dispose();
        }
    }
}