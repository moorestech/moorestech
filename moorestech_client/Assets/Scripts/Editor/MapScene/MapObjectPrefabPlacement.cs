#if UNITY_EDITOR
using Client.Game.InGame.Map.MapObject;
using Game.Map.Interface.Json;
using UnityEditor;
using UnityEngine;

namespace Client.MapScene.Editor
{
    public static class MapObjectPrefabPlacement
    {
        public static GameObject Instantiate(MapObjectInfoJson info, GameObject prefab, Transform parent)
        {
            // Prefabリンクと世界姿勢を保ち、Exportと同じローカルスケールを戻す
            // Preserve the prefab link and world pose, restoring the local scale written by Export
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null)
            {
                Debug.LogError($"Map object prefab could not be instantiated: InstanceId:{info.InstanceId} MapObjectGuid:{info.MapObjectGuidStr}");
                return null;
            }
            instance.transform.SetPositionAndRotation(info.Position, info.Rotation);
            instance.transform.localScale = info.Scale;

            var component = instance.GetComponent<MapObjectGameObject>();
            if (component == null)
            {
                Debug.LogError($"Map object prefab root missing: InstanceId:{info.InstanceId} MapObjectGuid:{info.MapObjectGuidStr}");
                Object.DestroyImmediate(instance);
                return null;
            }

            // オーサリングとプレビューが同じID/GUID注入を使う
            // Authoring and preview share the same identity injection
            component.SetRuntimeIdentity(info.InstanceId, info.MapObjectGuidStr);
            return instance;
        }
    }
}
#endif
