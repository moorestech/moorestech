using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEditor.Compilation;

[InitializeOnLoad]
public static class SchemaWatcher
{
    private static readonly string schemaFolderPath;
    private static readonly string cacheFilePath;
    private static Dictionary<string, string> cachedFileHashes = new Dictionary<string, string>();
    
    // Core.Masterフォルダのパスを指定
    private static readonly string coreMasterFolderPath;
    
    static SchemaWatcher()
    {
        // プロジェクトフォルダ/../VanillaSchema のパスを取得
        schemaFolderPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../VanillaSchema"));
        // キャッシュファイルのパスを設定（Libraryフォルダ内）
        cacheFilePath = Path.Combine(Application.dataPath, "../Library/SchemaCache.txt");
        // Core.Masterフォルダのパスを取得（Assets/Core.Master）
        coreMasterFolderPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../moorestech_server/Assets/Scripts/Core.Master"));
        
        LoadCache();
        
        // エディタの更新イベントに登録
        EditorApplication.update += Update;
        
        #region Internal
        
        // キャッシュの読み込み
        void LoadCache()
        {
            if (!File.Exists(cacheFilePath)) return;
            
            cachedFileHashes = new Dictionary<string, string>();
            var lines = File.ReadAllLines(cacheFilePath);
            foreach (var line in lines)
            {
                var split = line.Split('|');
                if (split.Length == 2)
                {
                    cachedFileHashes[split[0]] = split[1];
                }
            }
        }
        
  #endregion
    }
    
    private const float CheckInterval = 1f; // 1秒ごとにチェック
    private static float timer = 0f;
    private static void Update()
    {
        timer += Time.deltaTime;
        if (timer >= CheckInterval)
        {
            timer = 0f;
            CheckForChanges();
        }
    }
    
    // 変更のチェック
    [MenuItem("moorestech/Check Schema Changes")]
    public static void CheckForChanges()
    {
        var currentFileHashes = new Dictionary<string, string>();
        
        if (Directory.Exists(schemaFolderPath))
        {
            var files = Directory.GetFiles(schemaFolderPath, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = file.Substring(schemaFolderPath.Length + 1).Replace('\\', '/');
                var hash = ComputeHash(file);
                currentFileHashes[relativePath] = hash;
            }
        }
        else
        {
            Debug.LogWarning($"Schemaフォルダが見つかりません: {schemaFolderPath}");
            return;
        }
        
        bool hasChanged = HasFolderChanged(cachedFileHashes, currentFileHashes);
        
        if (hasChanged)
        {
            Debug.Log("Schemaフォルダに変更がありました。Core.Masterアセンブリを再コンパイルします。");
            
            // キャッシュを更新
            cachedFileHashes = currentFileHashes;
            SaveCache();
            
            // Core.Masterアセンブリを再コンパイルするためにDummy.csを更新
            UpdateDummyScript();
            CompilationPipeline.RequestScriptCompilation();
        }
        
        #region Internal
        
        // ファイルのハッシュ値を計算
        string ComputeHash(string filePath)
        {
            using var md5 = MD5.Create();
            
            var content = File.ReadAllBytes(filePath);
            var hash = md5.ComputeHash(content);
            return System.BitConverter.ToString(hash);
        }
        
        // キャッシュの保存
        void SaveCache()
        {
            var lines = new List<string>();
            foreach (var kvp in cachedFileHashes)
            {
                lines.Add($"{kvp.Key}|{kvp.Value}");
            }
            
            File.WriteAllLines(cacheFilePath, lines.ToArray());
        }
        
        // フォルダの変更を検出
        static bool HasFolderChanged(Dictionary<string, string> oldHashes, Dictionary<string, string> newHashes)
        {
            if (oldHashes.Count != newHashes.Count)
                return true;
            
            foreach (var kvp in newHashes)
            {
                if (!oldHashes.TryGetValue(kvp.Key, out string oldHash) || oldHash != kvp.Value)
                    return true;
            }
            
            return false;
        }
        
        // Dummy.csを更新してCore.Masterアセンブリを再コンパイル
        static void UpdateDummyScript()
        {
            // Core.Masterフォルダが存在するか確認
            if (!Directory.Exists(coreMasterFolderPath))
            {
                Debug.LogError($"Core.Masterフォルダが見つかりません: {coreMasterFolderPath}");
                return;
            }
            
            // Compile Requesterのパスを指定
            string dummyFilePath = Path.Combine(coreMasterFolderPath, "CompileRequester.cs");
            
            // 現在の日付を取得
            string currentDateTime = System.DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            
            // Dummy.csの内容を作成
            string dummyScriptContent = $@"
// このコードはCore.Masterアセンブリを再コンパイルするためのスクリプトです。SchemaWatcherによって更新されます。
// This code is a script to recompile the Core.Master assembly. It is updated by SchemaWatcher.
public class CompileRequester
{{
    private const string dummyText = ""{currentDateTime}"";
}}";
            
            // Dummy.csに書き込む
            File.WriteAllText(dummyFilePath, dummyScriptContent);
        }
        
        #endregion
    }
}