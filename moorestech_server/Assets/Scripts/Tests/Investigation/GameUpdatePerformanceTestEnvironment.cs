using System;
using System.IO;
using Core.Update;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;

namespace Tests.Investigation
{
    public class GameUpdatePerformanceTestEnvironment : IDisposable
    {
        public ServiceProvider ServiceProvider { get; }

        private GameUpdatePerformanceTestEnvironment(ServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public static GameUpdatePerformanceTestEnvironment CreateCurrentSave()
        {
            var serverDataDirectory = ServerDirectory.GetDirectory();
            var saveFilePath = MoorestechServerDIContainerOptions.DefaultSaveJsonFilePath;
            Assert.That(Directory.Exists(serverDataDirectory), Is.True, serverDataDirectory);
            Assert.That(File.Exists(saveFilePath), Is.True, saveFilePath);

            // 現在の通常セーブを実環境と同じ DI 経路でロードする
            // Load the current normal save through the same DI path as the server.
            var options = new MoorestechServerDIContainerOptions(serverDataDirectory)
            {
                saveJsonFilePath = new SaveJsonFilePath(saveFilePath)
            };
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(options);
            serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();

            var fileInfo = new FileInfo(saveFilePath);
            Debug.Log($"[GameUpdateProfile] Environment savePath={saveFilePath} bytes={fileInfo.Length} serverDataDirectory={serverDataDirectory}");
            return new GameUpdatePerformanceTestEnvironment(serviceProvider);
        }

        public void Dispose()
        {
            ServiceProvider.Dispose();
            GameUpdater.Dispose();
        }
    }
}
