using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Biomes;
using UnityEditor;
using UnityEngine;

namespace MapGenerator.Editor
{
    /// <summary>
    /// JSONバックアップからScriptableObjectアセットを生成するマイグレーションツール。
    /// migration_backup.json の値を読み込み、ルートSO + 8バイオームSOを作成する。
    /// </summary>
    public static class ConfigMigrationTool
    {
        const string PresetDir = "Assets/MapGenerator/Presets";
        const string BiomeDir = "Assets/MapGenerator/Presets/Biomes";
        const string BackupPath = "Assets/MapGenerator/Presets/migration_backup.json";

        [MenuItem("Tools/MapGenerator/Migrate Config to ScriptableObjects")]
        static void Migrate()
        {
            EnsureDirectories();

            // JSONバックアップを読み込んでパース用の一時構造体に変換
            var backupAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(BackupPath);
            if (backupAsset == null)
            {
                Debug.LogError($"[Migration] バックアップが見つかりません: {BackupPath}");
                return;
            }

            // バイオームSOを先に作成（ルートSOから参照するため）
            var grassland = CreateBiomeSO<GrasslandBiomeConfig>("Grassland");
            var forest = CreateBiomeSO<ForestBiomeConfig>("Forest");
            var savanna = CreateBiomeSO<SavannaBiomeConfig>("Savanna");
            var desert = CreateBiomeSO<DesertBiomeConfig>("Desert");
            var mesa = CreateBiomeSO<MesaBiomeConfig>("Mesa");
            var alpine = CreateBiomeSO<AlpineBiomeConfig>("Alpine");
            var jungle = CreateBiomeSO<JungleBiomeConfig>("Jungle");
            var woods = CreateBiomeSO<WoodsBiomeConfig>("Woods");

            // ルートSOを作成
            var rootConfig = ScriptableObject.CreateInstance<TerrainGenerationConfig>();

            // JSONからバックアップ値を復元。JsonUtilityはSOフィールドのネスト参照を復元できないため
            // まずルートの非SOフィールドをOverwrite、その後バイオームは個別にJsonOverwrite
            var json = backupAsset.text;

            // ルートSOのスカラーフィールドをJSONから復元
            JsonUtility.FromJsonOverwrite(json, rootConfig);

            // バイオームSOをJSONの該当部分から復元
            OverwriteBiomeFromJson(json, "grassland", grassland);
            OverwriteBiomeFromJson(json, "forest", forest);
            OverwriteBiomeFromJson(json, "savanna", savanna);
            OverwriteBiomeFromJson(json, "desert", desert);
            OverwriteBiomeFromJson(json, "mesa", mesa);
            OverwriteBiomeFromJson(json, "alpine", alpine);
            OverwriteBiomeFromJson(json, "jungle", jungle);
            OverwriteBiomeFromJson(json, "woods", woods);

            // ルートSOにバイオームSO参照をセット
            rootConfig.grassland = grassland;
            rootConfig.forest = forest;
            rootConfig.savanna = savanna;
            rootConfig.desert = desert;
            rootConfig.mesa = mesa;
            rootConfig.alpine = alpine;
            rootConfig.jungle = jungle;
            rootConfig.woods = woods;

            // アセットとして保存
            AssetDatabase.CreateAsset(rootConfig, $"{PresetDir}/DefaultConfig.asset");

            // 全アセットを保存
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // シーン上のInfiniteTerrainManagerに割り当て
            AssignToScene(rootConfig);

            Debug.Log("[Migration] ScriptableObjectアセットの作成が完了しました。");
            Debug.Log($"[Migration] ルート: {PresetDir}/DefaultConfig.asset");
            Debug.Log($"[Migration] バイオーム: {BiomeDir}/{{BiomeName}}.asset × 8");
        }

        /// <summary>
        /// SOアセット一式を新規作成する（JSONバックアップなしでデフォルト値を使用）
        /// </summary>
        [MenuItem("Tools/MapGenerator/Create Default Config Assets")]
        static void CreateDefaultAssets()
        {
            EnsureDirectories();

            var grassland = CreateBiomeSO<GrasslandBiomeConfig>("Grassland");
            var forest = CreateBiomeSO<ForestBiomeConfig>("Forest");
            var savanna = CreateBiomeSO<SavannaBiomeConfig>("Savanna");
            var desert = CreateBiomeSO<DesertBiomeConfig>("Desert");
            var mesa = CreateBiomeSO<MesaBiomeConfig>("Mesa");
            var alpine = CreateBiomeSO<AlpineBiomeConfig>("Alpine");
            var jungle = CreateBiomeSO<JungleBiomeConfig>("Jungle");
            var woods = CreateBiomeSO<WoodsBiomeConfig>("Woods");

            var rootConfig = ScriptableObject.CreateInstance<TerrainGenerationConfig>();
            rootConfig.grassland = grassland;
            rootConfig.forest = forest;
            rootConfig.savanna = savanna;
            rootConfig.desert = desert;
            rootConfig.mesa = mesa;
            rootConfig.alpine = alpine;
            rootConfig.jungle = jungle;
            rootConfig.woods = woods;

            AssetDatabase.CreateAsset(rootConfig, $"{PresetDir}/DefaultConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AssignToScene(rootConfig);
            Debug.Log("[Migration] デフォルト値でScriptableObjectアセットを作成しました。");
        }

        static T CreateBiomeSO<T>(string name) where T : ScriptableObject
        {
            string path = $"{BiomeDir}/{name}.asset";
            // 既存アセットがあればスキップ
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                Debug.Log($"[Migration] 既存アセットを使用: {path}");
                return existing;
            }

            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, path);
            return instance;
        }

        /// <summary>
        /// JSONの特定キー配下のオブジェクトを抽出してSOに上書きする。
        /// JsonUtilityはネストしたJSONオブジェクトを直接パースできるため、
        /// キーを手動で抽出してFromJsonOverwriteに渡す。
        /// </summary>
        static void OverwriteBiomeFromJson(string fullJson, string key, ScriptableObject target)
        {
            // "key": { ... } の部分を抽出
            string searchKey = $"\"{key}\": {{";
            int startIdx = fullJson.IndexOf(searchKey);
            if (startIdx < 0)
            {
                Debug.LogWarning($"[Migration] JSONに '{key}' が見つかりません。デフォルト値を使用します。");
                return;
            }

            // 開き波括弧の位置から対応する閉じ波括弧を探す
            int braceStart = fullJson.IndexOf('{', startIdx + searchKey.Length - 1);
            int depth = 0;
            int braceEnd = -1;
            for (int i = braceStart; i < fullJson.Length; i++)
            {
                if (fullJson[i] == '{') depth++;
                else if (fullJson[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        braceEnd = i;
                        break;
                    }
                }
            }

            if (braceEnd < 0)
            {
                Debug.LogWarning($"[Migration] '{key}' のJSON構造が不正です。デフォルト値を使用します。");
                return;
            }

            string biomeJson = fullJson.Substring(braceStart, braceEnd - braceStart + 1);
            JsonUtility.FromJsonOverwrite(biomeJson, target);
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// シーン上のMapGeneratorFacade/InfiniteTerrainManagerにconfig参照を割り当て
        /// </summary>
        static void AssignToScene(TerrainGenerationConfig config)
        {
            // MapGeneratorFacade
            var facades = Object.FindObjectsByType<MapGeneratorFacade>(FindObjectsSortMode.None);
            foreach (var f in facades)
            {
                f.config = config;
                EditorUtility.SetDirty(f);
            }

            // InfiniteTerrainManager
            var managers = Object.FindObjectsByType<InfiniteTerrainManager>(FindObjectsSortMode.None);
            foreach (var m in managers)
            {
                m.baseConfig = config;
                EditorUtility.SetDirty(m);
            }
        }

        static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/MapGenerator/Presets"))
                AssetDatabase.CreateFolder("Assets/MapGenerator", "Presets");
            if (!AssetDatabase.IsValidFolder("Assets/MapGenerator/Presets/Biomes"))
                AssetDatabase.CreateFolder("Assets/MapGenerator/Presets", "Biomes");
        }
    }
}
