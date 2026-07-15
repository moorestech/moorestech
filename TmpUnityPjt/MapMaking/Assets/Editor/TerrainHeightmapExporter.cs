using System;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

public class TerrainHeightmapExporter : EditorWindow
{
    private enum ExportMode { Individual, Combined }

    private GameObject target;
    private ExportMode exportMode = ExportMode.Individual;
    private int outputResolution = 1025;
    private bool normalizeHeight = true;
    private bool autoImportSettings = true;

    private static readonly int[] resolutionOptions = { 513, 1025, 2049, 4097 };
    private static readonly string[] resolutionLabels = { "513", "1025", "2049", "4097" };

    [MenuItem("Tools/Export Terrain Heightmap")]
    public static void ShowWindow()
    {
        GetWindow<TerrainHeightmapExporter>("Terrain Heightmap Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Terrain Heightmap Exporter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        target = (GameObject)EditorGUILayout.ObjectField("Target", target, typeof(GameObject), true);
        exportMode = (ExportMode)EditorGUILayout.EnumPopup("Export Mode", exportMode);
        outputResolution = DrawPopup("Output Resolution", outputResolution, resolutionOptions, resolutionLabels);
        normalizeHeight = EditorGUILayout.Toggle("Normalize Height", normalizeHeight);
        autoImportSettings = EditorGUILayout.Toggle("Auto Import Settings", autoImportSettings);

        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(target == null);
        if (GUILayout.Button("Export Heightmap", GUILayout.Height(30)))
        {
            if (exportMode == ExportMode.Individual)
                ExportIndividual();
            else
                ExportCombined();
        }
        EditorGUI.EndDisabledGroup();

        if (target == null)
            EditorGUILayout.HelpBox("Assign a Target GameObject containing Terrain(s).", MessageType.Info);
    }

    private static int DrawPopup(string label, int currentValue, int[] options, string[] labels)
    {
        int index = Array.IndexOf(options, currentValue);
        if (index < 0) index = 1;
        index = EditorGUILayout.Popup(label, index, labels);
        return options[index];
    }

    private void ExportIndividual()
    {
        var terrains = target.GetComponentsInChildren<Terrain>();
        if (terrains.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "No Terrain found in target.", "OK");
            return;
        }

        string folder = EditorUtility.SaveFolderPanel("Select Output Folder", "Assets", "");
        if (string.IsNullOrEmpty(folder)) return;

        float maxSizeY = 1f;
        if (normalizeHeight)
        {
            foreach (var t in terrains)
                maxSizeY = Mathf.Max(maxSizeY, t.terrainData.size.y);
        }

        try
        {
            for (int i = 0; i < terrains.Length; i++)
            {
                var terrain = terrains[i];
                var terrainData = terrain.terrainData;
                EditorUtility.DisplayProgressBar("Exporting Heightmaps",
                    $"Processing {terrain.name} ({i + 1}/{terrains.Length})", (float)i / terrains.Length);

                float scaleFactor = normalizeHeight ? terrainData.size.y / maxSizeY : 1f;
                var heights = terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);

                string fileName = $"{terrain.name}_heightmap.png";
                string filePath = Path.Combine(folder, fileName);

                SaveHeightmapPNG(heights, terrainData.heightmapResolution, terrainData.heightmapResolution,
                    outputResolution, outputResolution, scaleFactor, filePath);

                if (autoImportSettings)
                    ApplyImportSettings(filePath);
            }

            Debug.Log($"[TerrainHeightmapExporter] Exported {terrains.Length} heightmap(s) to {folder}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void ExportCombined()
    {
        var terrains = target.GetComponentsInChildren<Terrain>();
        if (terrains.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "No Terrain found in target.", "OK");
            return;
        }

        string filePath = EditorUtility.SaveFilePanel("Save Combined Heightmap", "Assets",
            target.name + "_heightmap", "png");
        if (string.IsNullOrEmpty(filePath)) return;

        try
        {
            EditorUtility.DisplayProgressBar("Exporting Combined Heightmap", "Calculating grid layout...", 0f);

            // Determine grid layout from terrain positions
            float minX = float.MaxValue, minZ = float.MaxValue;
            float sizeX = terrains[0].terrainData.size.x;
            float sizeZ = terrains[0].terrainData.size.z;

            foreach (var t in terrains)
            {
                var pos = t.transform.position;
                minX = Mathf.Min(minX, pos.x);
                minZ = Mathf.Min(minZ, pos.z);
            }

            int gridCols = 1, gridRows = 1;
            var gridPositions = new (int col, int row, Terrain terrain)[terrains.Length];

            for (int i = 0; i < terrains.Length; i++)
            {
                var pos = terrains[i].transform.position;
                int col = Mathf.RoundToInt((pos.x - minX) / sizeX);
                int row = Mathf.RoundToInt((pos.z - minZ) / sizeZ);
                gridPositions[i] = (col, row, terrains[i]);
                gridCols = Mathf.Max(gridCols, col + 1);
                gridRows = Mathf.Max(gridRows, row + 1);
            }

            int res = outputResolution;
            int combinedW = gridCols * (res - 1) + 1;
            int combinedH = gridRows * (res - 1) + 1;

            if (combinedW > 8192 || combinedH > 8192)
            {
                if (!EditorUtility.DisplayDialog("Warning",
                    $"Combined size is {combinedW}x{combinedH} (exceeds 8192). Continue?", "Yes", "Cancel"))
                    return;
            }

            float maxSizeY = 1f;
            if (normalizeHeight)
            {
                foreach (var t in terrains)
                    maxSizeY = Mathf.Max(maxSizeY, t.terrainData.size.y);
            }

            // Allocate combined height array
            var combined = new float[combinedH, combinedW];

            for (int i = 0; i < gridPositions.Length; i++)
            {
                var (col, row, terrain) = gridPositions[i];
                var terrainData = terrain.terrainData;
                EditorUtility.DisplayProgressBar("Exporting Combined Heightmap",
                    $"Reading {terrain.name} ({i + 1}/{terrains.Length})",
                    (float)i / terrains.Length * 0.8f);

                float scaleFactor = normalizeHeight ? terrainData.size.y / maxSizeY : 1f;
                int hmRes = terrainData.heightmapResolution;
                var heights = terrainData.GetHeights(0, 0, hmRes, hmRes);

                int offsetX = col * (res - 1);
                int offsetZ = row * (res - 1);

                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        // Sample from terrain heightmap with bilinear interpolation
                        float srcX = (float)x / (res - 1) * (hmRes - 1);
                        float srcZ = (float)z / (res - 1) * (hmRes - 1);

                        int x0 = Mathf.FloorToInt(srcX);
                        int z0 = Mathf.FloorToInt(srcZ);
                        int x1 = Mathf.Min(x0 + 1, hmRes - 1);
                        int z1 = Mathf.Min(z0 + 1, hmRes - 1);
                        float fx = srcX - x0;
                        float fz = srcZ - z0;

                        float h = Mathf.Lerp(
                            Mathf.Lerp(heights[z0, x0], heights[z0, x1], fx),
                            Mathf.Lerp(heights[z1, x0], heights[z1, x1], fx),
                            fz
                        ) * scaleFactor;

                        combined[offsetZ + z, offsetX + x] = h;
                    }
                }
            }

            EditorUtility.DisplayProgressBar("Exporting Combined Heightmap", "Encoding PNG...", 0.9f);

            SaveCombinedHeightmapPNG(combined, combinedW, combinedH, filePath);

            if (autoImportSettings)
                ApplyImportSettings(filePath);

            Debug.Log($"[TerrainHeightmapExporter] Exported combined heightmap ({combinedW}x{combinedH}) to {filePath}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void SaveHeightmapPNG(float[,] heights, int srcW, int srcH, int dstW, int dstH,
        float scaleFactor, string filePath)
    {
        Texture2D tex = null;
        try
        {
            tex = new Texture2D(dstW, dstH, TextureFormat.R16, false, true);
            var data = tex.GetRawTextureData<ushort>();

            for (int y = 0; y < dstH; y++)
            {
                for (int x = 0; x < dstW; x++)
                {
                    // GetHeights is [z, x] indexed
                    float srcX = (float)x / (dstW - 1) * (srcW - 1);
                    float srcZ = (float)y / (dstH - 1) * (srcH - 1);

                    int x0 = Mathf.FloorToInt(srcX);
                    int z0 = Mathf.FloorToInt(srcZ);
                    int x1 = Mathf.Min(x0 + 1, srcW - 1);
                    int z1 = Mathf.Min(z0 + 1, srcH - 1);
                    float fx = srcX - x0;
                    float fz = srcZ - z0;

                    float h = Mathf.Lerp(
                        Mathf.Lerp(heights[z0, x0], heights[z0, x1], fx),
                        Mathf.Lerp(heights[z1, x0], heights[z1, x1], fx),
                        fz
                    ) * scaleFactor;

                    data[y * dstW + x] = (ushort)(Mathf.Clamp01(h) * 65535);
                }
            }

            tex.Apply();
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
        }
        finally
        {
            if (tex != null)
                DestroyImmediate(tex);
        }
    }

    private static void SaveCombinedHeightmapPNG(float[,] combined, int width, int height, string filePath)
    {
        Texture2D tex = null;
        try
        {
            tex = new Texture2D(width, height, TextureFormat.R16, false, true);
            var data = tex.GetRawTextureData<ushort>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    data[y * width + x] = (ushort)(Mathf.Clamp01(combined[y, x]) * 65535);
                }
            }

            tex.Apply();
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
        }
        finally
        {
            if (tex != null)
                DestroyImmediate(tex);
        }
    }

    private static void ApplyImportSettings(string filePath)
    {
        // Convert to Assets-relative path
        string dataPath = Application.dataPath;
        if (!filePath.StartsWith(dataPath)) return;

        string assetPath = "Assets" + filePath.Substring(dataPath.Length);
        AssetDatabase.ImportAsset(assetPath);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureShape = TextureImporterShape.Texture2D;
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        TextureImporterPlatformSettings platformSettings = importer.GetDefaultPlatformTextureSettings();
        platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
        platformSettings.maxTextureSize = 4096;
        platformSettings.overridden = true;
        platformSettings.format = TextureImporterFormat.R16;
        importer.SetPlatformTextureSettings(platformSettings);

        importer.SaveAndReimport();
    }
}
