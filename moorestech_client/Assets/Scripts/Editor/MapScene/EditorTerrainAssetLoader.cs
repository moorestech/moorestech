#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Environment.Terrain.Assets;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Client.MapScene.Editor
{
    public sealed class EditorTerrainAssetLoader : ITerrainAssetLoader
    {
        private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _duplicatePaths = new(StringComparer.Ordinal);

        public EditorTerrainAssetLoader()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Preview Addressables settings are missing.");

            // フォルダ登録も展開し、完全一致のアドレスだけを解決する
            // Expand folder entries and resolve only exact addresses
            var entries = new List<AddressableAssetEntry>();
            settings.GetAllAssets(entries, false);
            foreach (var entry in entries)
            {
                if (!_paths.TryAdd(entry.address, entry.AssetPath))
                {
                    // 既存の無関係な重複は記録し、実際に要求された時点で候補を列挙して拒否する
                    // Record unrelated existing duplicates and reject them with their candidates only when requested
                    if (!_duplicatePaths.TryGetValue(entry.address, out var paths))
                    {
                        paths = new List<string> { _paths[entry.address] };
                        _duplicatePaths.Add(entry.address, paths);
                    }
                    paths.Add(entry.AssetPath);
                }
            }
        }

        public UniTask<T> LoadAsync<T>(string address, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_duplicatePaths.TryGetValue(address, out var duplicates))
                throw new InvalidOperationException($"Duplicate preview asset address: {address}, {typeof(T).Name}, {string.Join(", ", duplicates)}");
            if (!_paths.TryGetValue(address, out var path))
                throw new InvalidOperationException($"Preview asset address is missing: {address}, {typeof(T).Name}");

            // AssetDatabaseの借用資産を返すため、呼び手は破棄しない
            // Return a borrowed AssetDatabase asset that the caller must not destroy
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException($"Preview asset is missing: {address}, {typeof(T).Name}, {path}");
            return UniTask.FromResult(asset);
        }
    }
}
#endif
