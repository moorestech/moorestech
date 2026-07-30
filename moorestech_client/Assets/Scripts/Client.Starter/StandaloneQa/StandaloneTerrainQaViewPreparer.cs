using System.Collections.Generic;
using UnityEngine;

namespace Client.Starter.StandaloneQa
{
    public static class StandaloneTerrainQaViewPreparer
    {
        private const string GeneratedTerrainNamePrefix = "Terrain_";
        private const string DebugObjectsInstanceName = "DebugObjects(Clone)";
        private const string IngameDebugConsoleName = "IngameDebugConsole";
        private const string DebugLogManagerTypeName = "IngameDebugConsole.DebugLogManager";

        public static Terrain[] GetGeneratedTerrains()
        {
            var generatedTerrains = new List<Terrain>();
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain.name.StartsWith(GeneratedTerrainNamePrefix))
                    generatedTerrains.Add(terrain);
            }
            return generatedTerrains.ToArray();
        }

        public static void Prepare(Terrain terrain)
        {
            // 既存カメラとデバッグUIを外し、実行時生成TerrainだけをPlayer描画で撮影する
            // Remove existing cameras and debug UI so the Player renders only the runtime-generated Terrain
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                camera.enabled = false;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                canvas.enabled = false;
            var debugObjects = GameObject.Find(DebugObjectsInstanceName);
            if (debugObjects != null) debugObjects.SetActive(false);
            var debugConsole = GameObject.Find(IngameDebugConsoleName);
            if (debugConsole != null) debugConsole.SetActive(false);
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour.GetType().FullName == DebugLogManagerTypeName)
                    behaviour.gameObject.SetActive(false);
            }

            // タイル全体と起伏を同時に見渡せる斜め上の正投影カメラを生成する
            // Create an elevated orthographic camera that frames the whole tile and its relief
            var terrainSize = terrain.terrainData.size;
            var frameSize = Mathf.Max(terrainSize.x, terrainSize.z);
            var terrainCenter = terrain.transform.position +
                                new Vector3(terrainSize.x * 0.5f, terrainSize.y * 0.25f, terrainSize.z * 0.5f);
            var cameraObject = new GameObject("StandaloneTerrainQaCamera");
            var qaCamera = cameraObject.AddComponent<Camera>();
            qaCamera.transform.position = terrainCenter + new Vector3(frameSize * 0.55f, frameSize * 0.8f, -frameSize * 0.55f);
            qaCamera.transform.LookAt(terrainCenter);
            qaCamera.orthographic = true;
            qaCamera.orthographicSize = frameSize * 0.6f;
            qaCamera.farClipPlane = frameSize * 4f;
            qaCamera.depth = 100f;
        }
    }
}
