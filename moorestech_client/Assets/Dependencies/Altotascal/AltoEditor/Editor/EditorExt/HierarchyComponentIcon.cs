#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace AltoEditor
{
    [InitializeOnLoad]
    public class HierarchyComponentIcon
    {
        const string MenuPath = AltoMenuPath.EditorExt + "Show Component Icons in Hierarchy";
        static string PrefsKey => "AltoEditor-HierarchyComponentIcon";
        const int IconSize = 16;

        static readonly Type[] _targetTypes =
        {
            typeof(Animator),
            typeof(Camera),
            typeof(AudioListener),
            typeof(Light),
            typeof(Volume),
            typeof(EventSystem),
            typeof(Canvas),
            typeof(Image),
            typeof(Text),
            typeof(TextMeshProUGUI),
            typeof(Button),
            typeof(SkinnedMeshRenderer),
            typeof(Collider),
            typeof(Rigidbody),
            typeof(ParticleSystem),
        };

        [MenuItem(MenuPath)]
        static void ToggleEnabled()
        {
            bool isChecked = !Menu.GetChecked(MenuPath);
            Menu.SetChecked(MenuPath, isChecked);
            EditorSettingsUtil.SaveBool(PrefsKey, isChecked);
        }

        [MenuItem(MenuPath, true)]
        static bool Remember()
        {
            bool isChecked = EditorSettingsUtil.LoadBool(PrefsKey, false);
            Menu.SetChecked(MenuPath, isChecked);
            return true;
        }

        static HierarchyComponentIcon()
        {
            EditorApplication.delayCall += () => Remember();
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGUI;
        }

        static void HierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            if (!Menu.GetChecked(MenuPath)) { return; }

            // 対象のコンポーネント情報取得
            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (obj == null) { return; }

            Component[] allComponents = obj.GetComponents<Component>();
            HashSet<Texture> textures = new();

            // アイコンのテクスチャ取得
            foreach (Component component in allComponents)
            {
                if (component == null) { continue; }

                foreach (Type targetType in _targetTypes)
                {
                    Type type = component.GetType();
                    if (type == targetType || type.IsSubclassOf(targetType))
                    {
                        textures.Add(AssetPreview.GetMiniThumbnail(component));
                    }
                }
            }

            // アイコン描画
            Rect rect = selectionRect;
            rect.x += rect.width;
            rect.width = IconSize;

            foreach (Texture texture in textures)
            {
                if (texture == null) { continue; }

                rect.x -= IconSize;
                GUI.DrawTexture(rect, texture);
            }
        }
    }
}
#endif
