using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using UnityEngine.Rendering;

public class HierarchyFBXExporter : EditorWindow
{
    private GameObject targetObject;
    private int terrainMeshResolution = 256;
    private int bakedTextureResolution = 2048;
    private bool exportTerrainTrees = true;
    private bool exportChildMeshes = true;
    private bool bakeSplatmapTexture = true;
    private bool exportMaterialTextures = true;

    private static readonly int[] resolutionOptions = { 64, 128, 256, 512 };
    private static readonly string[] resolutionLabels = { "64", "128", "256", "512" };
    private static readonly int[] textureResOptions = { 512, 1024, 2048, 4096 };
    private static readonly string[] textureResLabels = { "512", "1024", "2048", "4096" };

    // Maps custom shader texture properties to Standard shader equivalents
    private static readonly Dictionary<string, string> TexturePropMapping = new Dictionary<string, string>
    {
        { "_MainTex", "_MainTex" },
        { "_BaseMap", "_MainTex" },
        { "_BumpMap", "_BumpMap" },
        { "_MetallicGlossMap", "_MetallicGlossMap" },
        { "_OcclusionMap", "_OcclusionMap" },
        { "_EmissionMap", "_EmissionMap" },
        { "_DetailAlbedoMap", "_DetailAlbedoMap" },
        { "_DetailNormalMap", "_DetailNormalMap" },
        { "_ParallaxMap", "_ParallaxMap" },
        // BK Pure Nature custom shaders
        { "_Diffuse", "_MainTex" },
        { "_Normal", "_BumpMap" },
        { "_MetallicROcclusionGSmoothnessA", "_MetallicGlossMap" },
        { "_MaskMap", "_MetallicGlossMap" },
        { "_SpecGlossMap", "_MetallicGlossMap" },
        { "_LayerAlbedoMap", "_DetailAlbedoMap" },
        { "_LayerNormalMap", "_DetailNormalMap" },
    };

    [MenuItem("Tools/Export Hierarchy to FBX")]
    public static void ShowWindow()
    {
        GetWindow<HierarchyFBXExporter>("Hierarchy FBX Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Hierarchy to Textured FBX Exporter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);

        EditorGUILayout.Space();
        GUILayout.Label("Terrain Settings", EditorStyles.boldLabel);

        terrainMeshResolution = DrawPopup("Terrain Mesh Resolution", terrainMeshResolution, resolutionOptions, resolutionLabels);
        bakedTextureResolution = DrawPopup("Baked Texture Resolution", bakedTextureResolution, textureResOptions, textureResLabels);

        bakeSplatmapTexture = EditorGUILayout.Toggle("Bake Splatmap Texture", bakeSplatmapTexture);
        exportTerrainTrees = EditorGUILayout.Toggle("Export Terrain Trees", exportTerrainTrees);
        exportChildMeshes = EditorGUILayout.Toggle("Export Child Meshes", exportChildMeshes);
        exportMaterialTextures = EditorGUILayout.Toggle("Export Material Textures", exportMaterialTextures);

        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(targetObject == null);
        if (GUILayout.Button("Export FBX", GUILayout.Height(30)))
        {
            string path = EditorUtility.SaveFilePanel("Save FBX", "Assets", targetObject.name, "fbx");
            if (!string.IsNullOrEmpty(path))
                ExportHierarchyToFBX(path);
        }
        EditorGUI.EndDisabledGroup();

        if (targetObject == null)
            EditorGUILayout.HelpBox("Assign a Target Object to export.", MessageType.Info);
    }

    private static int DrawPopup(string label, int currentValue, int[] options, string[] labels)
    {
        int index = Array.IndexOf(options, currentValue);
        if (index < 0) index = 2;
        index = EditorGUILayout.Popup(label, index, labels);
        return options[index];
    }

    private void ExportHierarchyToFBX(string path)
    {
        var tempObjects = new List<GameObject>();
        const string tempAssetDir = "Assets/_FBXExportTemp";

        try
        {
            EditorUtility.DisplayProgressBar("Exporting FBX", "Preparing...", 0f);

            var tempRoot = CreateTempObject(targetObject.name + "_Export", null, tempObjects);

            string outputDir = Path.GetDirectoryName(path);
            string baseName = Path.GetFileNameWithoutExtension(path);

            // Setup temp asset directory for textures
            if (AssetDatabase.IsValidFolder(tempAssetDir))
                AssetDatabase.DeleteAsset(tempAssetDir);
            AssetDatabase.CreateFolder("Assets", "_FBXExportTemp");

            string tempTexDir = $"{tempAssetDir}/{baseName}_textures";
            AssetDatabase.CreateFolder(tempAssetDir, $"{baseName}_textures");
            string tempTexDiskDir = Path.Combine(Application.dataPath, $"_FBXExportTemp/{baseName}_textures");

            ProcessTerrains(tempRoot.transform, tempTexDir, tempTexDiskDir, tempObjects);

            if (exportChildMeshes)
                ProcessChildMeshes(tempRoot.transform, tempObjects);

            if (exportMaterialTextures)
            {
                EditorUtility.DisplayProgressBar("Exporting FBX", "Exporting material textures...", 0.8f);
                ExportMaterialTextures(tempRoot, tempTexDir, tempTexDiskDir);
            }

            EditorUtility.DisplayProgressBar("Exporting FBX", "Converting materials for FBX...", 0.85f);
            ConvertMaterialsToStandard(tempRoot, tempTexDir);

            // Export Binary FBX to temp location (so relative paths to textures are correct)
            EditorUtility.DisplayProgressBar("Exporting FBX", "Writing FBX file...", 0.9f);
            string tempFbxDiskPath = Path.Combine(Application.dataPath, $"_FBXExportTemp/{baseName}.fbx");
            ModelExporter.ExportObject(tempFbxDiskPath, tempRoot, new ExportModelOptions
            {
                ExportFormat = ExportFormat.Binary
            });

            // Move FBX and textures to final output location
            EditorUtility.DisplayProgressBar("Exporting FBX", "Moving files to output...", 0.95f);

            if (File.Exists(path)) File.Delete(path);
            File.Move(tempFbxDiskPath, path);

            string outputTexDir = Path.Combine(outputDir, baseName + "_textures");
            if (Directory.Exists(outputTexDir))
                Directory.Delete(outputTexDir, true);
            CopyDirectoryExcludingMeta(tempTexDiskDir, outputTexDir);

            int texCount = Directory.GetFiles(outputTexDir, "*.png").Length;
            Debug.Log($"[HierarchyFBXExporter] Export complete: {path}\n" +
                      $"Binary FBX: {new FileInfo(path).Length / 1024 / 1024} MB\n" +
                      $"Textures: {texCount} files in {baseName}_textures/");
        }
        catch (Exception e)
        {
            Debug.LogError($"[HierarchyFBXExporter] Export failed: {e}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            foreach (var obj in tempObjects)
            {
                if (obj != null)
                    DestroyImmediate(obj);
            }
            if (AssetDatabase.IsValidFolder(tempAssetDir))
                AssetDatabase.DeleteAsset(tempAssetDir);
        }
    }

    private void ProcessTerrains(Transform tempRoot, string tempTexDir, string tempTexDiskDir, List<GameObject> tempObjects)
    {
        var terrains = targetObject.GetComponentsInChildren<Terrain>();

        for (int i = 0; i < terrains.Length; i++)
        {
            var terrain = terrains[i];
            float progress = (float)i / Mathf.Max(terrains.Length, 1);
            EditorUtility.DisplayProgressBar("Exporting FBX",
                $"Processing terrain {i + 1}/{terrains.Length}: {terrain.name}", progress * 0.5f);

            var terrainData = terrain.terrainData;
            if (terrainData == null) continue;

            // Convert terrain heightmap to mesh
            var mesh = ConvertTerrainToMesh(terrain, terrainMeshResolution);
            var terrainGO = CreateTempObject(terrain.name + "_Mesh", tempRoot, tempObjects);
            terrainGO.transform.position = terrain.transform.position;

            var mf = terrainGO.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = terrainGO.AddComponent<MeshRenderer>();

            // Bake splatmap texture
            if (bakeSplatmapTexture && terrainData.terrainLayers is { Length: > 0 })
            {
                EditorUtility.DisplayProgressBar("Exporting FBX",
                    $"Baking splatmap for {terrain.name}...", progress * 0.5f + 0.1f);

                var bakedTex = BakeTerrainSplatmap(terrain, bakedTextureResolution);
                if (bakedTex != null)
                {
                    string splatFileName = SanitizeFileName($"{terrain.name}_splatmap") + ".png";
                    File.WriteAllBytes(Path.Combine(tempTexDiskDir, splatFileName), bakedTex.EncodeToPNG());
                    DestroyImmediate(bakedTex);

                    string splatAssetPath = $"{tempTexDir}/{splatFileName}";
                    AssetDatabase.ImportAsset(splatAssetPath, ImportAssetOptions.ForceSynchronousImport);

                    var mat = new Material(Shader.Find("Standard"));
                    mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(splatAssetPath));
                    mr.sharedMaterial = mat;
                }
            }

            // Instantiate terrain trees
            if (exportTerrainTrees)
            {
                EditorUtility.DisplayProgressBar("Exporting FBX",
                    $"Processing trees for {terrain.name}...", progress * 0.5f + 0.2f);

                var treeParent = CreateTempObject(terrain.name + "_Trees", tempRoot, tempObjects);
                treeParent.transform.position = terrain.transform.position;
                InstantiateTerrainTrees(terrain, treeParent.transform, tempObjects);
            }
        }
    }

    private void ProcessChildMeshes(Transform tempRoot, List<GameObject> tempObjects)
    {
        EditorUtility.DisplayProgressBar("Exporting FBX", "Processing child meshes...", 0.7f);

        var processedRenderers = new HashSet<Renderer>();

        // Build LOD exclusion set (skip non-LOD0 renderers)
        var lodExcluded = new HashSet<Renderer>();
        foreach (var lodGroup in targetObject.GetComponentsInChildren<LODGroup>())
        {
            var lods = lodGroup.GetLODs();
            for (int lodIndex = 1; lodIndex < lods.Length; lodIndex++)
            {
                foreach (var renderer in lods[lodIndex].renderers)
                {
                    if (renderer != null)
                        lodExcluded.Add(renderer);
                }
            }
        }

        // MeshFilter-based objects
        foreach (var mf in targetObject.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            if (mf.GetComponent<Terrain>() != null) continue;

            var renderer = mf.GetComponent<MeshRenderer>();
            if (renderer != null && (lodExcluded.Contains(renderer) || !processedRenderers.Add(renderer)))
                continue;

            var copyGO = CreateMeshCopy(mf.gameObject.name, mf.transform, mf.sharedMesh,
                renderer?.sharedMaterials, tempRoot, tempObjects);
        }

        // SkinnedMeshRenderer objects
        foreach (var smr in targetObject.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr.sharedMesh == null) continue;
            if (lodExcluded.Contains(smr) || !processedRenderers.Add(smr))
                continue;

            var bakedMesh = new Mesh();
            smr.BakeMesh(bakedMesh);
            var copyGO = CreateMeshCopy(smr.gameObject.name, smr.transform, bakedMesh,
                smr.sharedMaterials, tempRoot, tempObjects);
        }
    }

    private GameObject CreateMeshCopy(string name, Transform source, Mesh mesh,
        Material[] materials, Transform parent, List<GameObject> tempObjects)
    {
        var go = CreateTempObject(name, parent, tempObjects);
        go.transform.position = source.position;
        go.transform.rotation = source.rotation;
        go.transform.localScale = source.lossyScale;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (materials != null)
            mr.sharedMaterials = materials;

        return go;
    }

    private static GameObject CreateTempObject(string name, Transform parent, List<GameObject> tempObjects)
    {
        var go = new GameObject(name);
        go.hideFlags = HideFlags.HideAndDontSave;
        tempObjects.Add(go);
        if (parent != null)
            go.transform.SetParent(parent, false);
        return go;
    }

    private static void ExportMaterialTextures(GameObject root, string tempTexAssetDir, string tempTexDiskDir)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        var exportedKeys = new HashSet<string>();
        int count = 0;

        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat == null) continue;

                int propCount = mat.shader.GetPropertyCount();
                for (int p = 0; p < propCount; p++)
                {
                    if (mat.shader.GetPropertyType(p) != ShaderPropertyType.Texture)
                        continue;

                    string propName = mat.shader.GetPropertyName(p);
                    if (propName.StartsWith("unity_") || propName == "_texcoord")
                        continue;

                    var tex = mat.GetTexture(propName) as Texture2D;
                    if (tex == null) continue;

                    string assetPath = AssetDatabase.GetAssetPath(tex);
                    string key = string.IsNullOrEmpty(assetPath) ? tex.GetInstanceID().ToString() : assetPath;
                    if (!exportedKeys.Add(key)) continue;

                    try
                    {
                        string safeName = SanitizeFileName(tex.name);
                        string outputFileName = safeName + ".png";
                        string outputDiskPath = Path.Combine(tempTexDiskDir, outputFileName);

                        // Handle name collision
                        if (File.Exists(outputDiskPath))
                        {
                            outputFileName = SanitizeFileName($"{mat.name}_{tex.name}") + ".png";
                            outputDiskPath = Path.Combine(tempTexDiskDir, outputFileName);
                        }

                        var readable = MakeReadable(tex);
                        File.WriteAllBytes(outputDiskPath, readable.EncodeToPNG());
                        if (readable != tex)
                            DestroyImmediate(readable);

                        AssetDatabase.ImportAsset($"{tempTexAssetDir}/{outputFileName}",
                            ImportAssetOptions.ForceSynchronousImport);
                        count++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[HierarchyFBXExporter] Failed to export texture {tex.name}: {e.Message}");
                    }
                }
            }
        }

        if (count > 0)
            Debug.Log($"[HierarchyFBXExporter] Exported {count} material textures");
    }

    private static void ConvertMaterialsToStandard(GameObject root, string tempTexAssetDir)
    {
        var standardShader = Shader.Find("Standard");
        if (standardShader == null) return;

        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            var materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                var srcMat = materials[i];
                if (srcMat == null || srcMat.shader == standardShader) continue;

                var newMat = new Material(standardShader);
                newMat.name = srcMat.name;
                newMat.color = srcMat.HasProperty("_Color") ? srcMat.color : Color.white;

                int propCount = srcMat.shader.GetPropertyCount();
                for (int p = 0; p < propCount; p++)
                {
                    if (srcMat.shader.GetPropertyType(p) != ShaderPropertyType.Texture)
                        continue;

                    string srcProp = srcMat.shader.GetPropertyName(p);
                    var tex = srcMat.GetTexture(srcProp);
                    if (tex == null) continue;

                    if (!TexturePropMapping.TryGetValue(srcProp, out string dstProp)) continue;
                    if (!newMat.HasProperty(dstProp)) continue;

                    // Prefer temp asset version for correct FBX relative paths
                    string tempPath = $"{tempTexAssetDir}/{SanitizeFileName(tex.name)}.png";
                    var tempTex = AssetDatabase.LoadAssetAtPath<Texture2D>(tempPath);
                    newMat.SetTexture(dstProp, tempTex != null ? tempTex : tex);
                }

                materials[i] = newMat;
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    #region Terrain Conversion

    private static Mesh ConvertTerrainToMesh(Terrain terrain, int resolution)
    {
        var terrainData = terrain.terrainData;
        int vertexCount = (resolution + 1) * (resolution + 1);

        var mesh = new Mesh();
        mesh.name = terrain.name + "_TerrainMesh";
        mesh.indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

        var heights = terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);
        var terrainSize = terrainData.size;
        int hmRes = terrainData.heightmapResolution;

        var vertices = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];

        int index = 0;
        for (int y = 0; y <= resolution; y++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                float normX = (float)x / resolution;
                float normY = (float)y / resolution;

                // Bilinear interpolation of heightmap
                float hmX = normX * (hmRes - 1);
                float hmY = normY * (hmRes - 1);
                int x0 = Mathf.FloorToInt(hmX);
                int y0 = Mathf.FloorToInt(hmY);
                int x1 = Mathf.Min(x0 + 1, hmRes - 1);
                int y1 = Mathf.Min(y0 + 1, hmRes - 1);
                float fx = hmX - x0;
                float fy = hmY - y0;

                float height = Mathf.Lerp(
                    Mathf.Lerp(heights[y0, x0], heights[y0, x1], fx),
                    Mathf.Lerp(heights[y1, x0], heights[y1, x1], fx),
                    fy
                );

                vertices[index] = new Vector3(normX * terrainSize.x, height * terrainSize.y, normY * terrainSize.z);
                uvs[index] = new Vector2(normX, normY);
                index++;
            }
        }

        int hCount = resolution + 1;
        var triangles = new int[resolution * resolution * 6];
        index = 0;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int topLeft = y * hCount + x;
                int bottomLeft = (y + 1) * hCount + x;

                triangles[index]     = topLeft;
                triangles[index + 1] = bottomLeft;
                triangles[index + 2] = topLeft + 1;
                triangles[index + 3] = bottomLeft;
                triangles[index + 4] = bottomLeft + 1;
                triangles[index + 5] = topLeft + 1;
                index += 6;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return mesh;
    }

    private static Texture2D BakeTerrainSplatmap(Terrain terrain, int resolution)
    {
        var terrainData = terrain.terrainData;
        var layers = terrainData.terrainLayers;
        if (layers == null || layers.Length == 0) return null;

        var alphamaps = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
        int alphaW = terrainData.alphamapWidth;
        int alphaH = terrainData.alphamapHeight;

        // Make readable copies of layer textures
        var readableTextures = new Texture2D[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null && layers[i].diffuseTexture != null)
                readableTextures[i] = MakeReadable(layers[i].diffuseTexture);
        }

        try
        {
            var result = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            var pixels = new Color[resolution * resolution];
            var terrainSize = terrainData.size;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float normX = (float)x / resolution;
                    float normY = (float)y / resolution;

                    int ax = Mathf.Clamp(Mathf.RoundToInt(normX * (alphaW - 1)), 0, alphaW - 1);
                    int ay = Mathf.Clamp(Mathf.RoundToInt(normY * (alphaH - 1)), 0, alphaH - 1);

                    Color blended = Color.black;
                    for (int l = 0; l < layers.Length; l++)
                    {
                        float weight = alphamaps[ay, ax, l];
                        if (weight < 0.001f || readableTextures[l] == null) continue;

                        var layer = layers[l];
                        var tex = readableTextures[l];

                        float tileU = (normX * terrainSize.x - layer.tileOffset.x) / layer.tileSize.x;
                        float tileV = (normY * terrainSize.z - layer.tileOffset.y) / layer.tileSize.y;
                        tileU -= Mathf.Floor(tileU);
                        tileV -= Mathf.Floor(tileV);

                        int texX = Mathf.Clamp(Mathf.FloorToInt(tileU * tex.width), 0, tex.width - 1);
                        int texY = Mathf.Clamp(Mathf.FloorToInt(tileV * tex.height), 0, tex.height - 1);

                        blended += tex.GetPixel(texX, texY) * weight;
                    }

                    blended.a = 1f;
                    pixels[y * resolution + x] = blended;
                }
            }

            result.SetPixels(pixels);
            result.Apply();
            return result;
        }
        finally
        {
            // Only destroy copies we created, not original asset textures
            for (int i = 0; i < readableTextures.Length; i++)
            {
                if (readableTextures[i] != null && readableTextures[i] != layers[i]?.diffuseTexture)
                    DestroyImmediate(readableTextures[i]);
            }
        }
    }

    private static void InstantiateTerrainTrees(Terrain terrain, Transform parent, List<GameObject> tempObjects)
    {
        var terrainData = terrain.terrainData;
        var treeInstances = terrainData.treeInstances;
        var treePrototypes = terrainData.treePrototypes;
        var terrainSize = terrainData.size;

        for (int i = 0; i < treeInstances.Length; i++)
        {
            var tree = treeInstances[i];
            if (tree.prototypeIndex < 0 || tree.prototypeIndex >= treePrototypes.Length) continue;

            var prototype = treePrototypes[tree.prototypeIndex];
            if (prototype.prefab == null) continue;

            var prefabMF = prototype.prefab.GetComponentInChildren<MeshFilter>();
            if (prefabMF == null || prefabMF.sharedMesh == null) continue;

            var treeGO = CreateTempObject($"Tree_{i}_{prototype.prefab.name}", parent, tempObjects);
            treeGO.transform.localPosition = new Vector3(
                tree.position.x * terrainSize.x,
                tree.position.y * terrainSize.y,
                tree.position.z * terrainSize.z
            );
            treeGO.transform.localRotation = Quaternion.Euler(0, tree.rotation * Mathf.Rad2Deg, 0);
            treeGO.transform.localScale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);

            treeGO.AddComponent<MeshFilter>().sharedMesh = prefabMF.sharedMesh;
            var mr = treeGO.AddComponent<MeshRenderer>();
            var prefabMR = prototype.prefab.GetComponentInChildren<MeshRenderer>();
            if (prefabMR != null)
                mr.sharedMaterials = prefabMR.sharedMaterials;
        }
    }

    #endregion

    #region Utilities

    private static Texture2D MakeReadable(Texture2D source)
    {
        if (source == null) return null;
        if (source.isReadable) return source;

        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;

        var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.name = source.name;
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return readable;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static void CopyDirectoryExcludingMeta(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            if (file.EndsWith(".meta")) continue;
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        }
    }

    #endregion
}
