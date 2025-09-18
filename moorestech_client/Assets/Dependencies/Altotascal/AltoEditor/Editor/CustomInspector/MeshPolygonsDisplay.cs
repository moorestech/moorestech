using UnityEngine;
using UnityEditor;

/// <summary>
/// Mesh のインスペクタ上部にポリゴン数を表示する
/// （Prefab や GameObject 選択時にポリゴン数をパッと確認できて便利）
/// </summary>
namespace AltoEditor
{
    [CustomEditor(typeof(MeshFilter))]
    public class MeshPolygonsDisplay : Editor
    {
        const string MenuPath = AltoMenuPath.EditorExt + "Show Mesh Polygons Count";
        static string PrefsKey => "AltoEditor-MeshPolygonsDisplay";

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

        public MeshPolygonsDisplay()
        {
            EditorApplication.delayCall += () => Remember();
        }

        public override void OnInspectorGUI()
        {
            if (!Menu.GetChecked(MenuPath))
            {
                base.OnInspectorGUI();
                return;
            }

            MeshFilter filter = target as MeshFilter;
            string polygons = $"{filter.sharedMesh.triangles.Length / 3} Tris";
            EditorGUILayout.LabelField( polygons );

            base.OnInspectorGUI();
        }
    }

    [CustomEditor(typeof(SkinnedMeshRenderer))]
    public class SkinnedMeshPolygonsDisplay : Editor
    {
        const string MenuPath = "Alto/Editor/Show Mesh Polygons Count";

        public override void OnInspectorGUI()
        {
            if (!Menu.GetChecked(MenuPath))
            {
                base.OnInspectorGUI();
                return;
            }

            SkinnedMeshRenderer skin = target as SkinnedMeshRenderer;
            string polygons = $"{skin.sharedMesh.triangles.Length / 3} Tris";
            EditorGUILayout.LabelField( polygons );

            base.OnInspectorGUI();
        }
    }
}
