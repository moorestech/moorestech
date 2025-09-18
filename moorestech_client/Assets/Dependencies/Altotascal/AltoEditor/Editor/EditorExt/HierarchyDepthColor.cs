#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace AltoEditor
{
    [InitializeOnLoad]
    public class HierarchyDepthColor
    {
        const string MenuPath = AltoMenuPath.EditorExt + "Show Depth Color in Hierarchy";
        static string PrefsKey => "AltoEditor-HierarchyDepthColor";
        const int OffsetX = -3;
        const int LineWidth = 3;

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

        static HierarchyDepthColor()
        {
            EditorApplication.delayCall += () => Remember();
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGUI;
        }

        static void HierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            if (!Menu.GetChecked(MenuPath)) { return; }

            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (obj == null) { return; }

            int depth = GetDepth(obj.transform);
            Rect rect = new(
              selectionRect.x + OffsetX,
              selectionRect.y,
              LineWidth,
              selectionRect.height
            );
            Color color = Color.HSVToRGB(GetHue(depth), 0.8f, 0.8f);
            EditorGUI.DrawRect(rect, color);
        }

        static int GetDepth(Transform transform)
        {
          int depth = 0;
          while (transform.parent != null)
          {
            ++depth;
            transform = transform.parent;

            // Safety
            if (depth >= 99) { return depth; }
          }
          return depth;
        }

        static float GetHue(int depth)
        {
          float hue = (0.7f - depth * 0.1f) % 1.0f;
          return hue - Mathf.Floor(hue);
        }
    }
}
#endif
