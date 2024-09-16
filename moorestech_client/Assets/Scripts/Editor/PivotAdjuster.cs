using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>
/// ピボット簡易調整エディタ拡張
/// </summary>
public sealed class PivotAdjuster : EditorWindow
{
    private enum Genre
    {
        [InspectorName("現代")]
        Modern,
        SF,
    }
    
    private enum Route
    {
        [InspectorName("直進")]
        Straight,
        [InspectorName("L字")]
        L,
        [InspectorName("T字")]
        T,
        [InspectorName("十字")]
        Cross,
        [InspectorName("直進の斜め上がり")]
        StraightUp,
    }
    
    private enum Parts
    {
        [InspectorName("ベルトコンベア")]
        BeltConveyor,
        [InspectorName("アイテムシューター")]
        ItemShooter,
    }
    
    private const string DefaultSavePath = "Assets/Asset/Block/Prefab";
    
    [MenuItem("Tools/Pivot Adjuster")]
    private static void ShowWindow() => GetWindow<PivotAdjuster>(nameof(PivotAdjuster));
    
    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.paddingTop = 10;
        
        var objectField = new ObjectField("Target Object")
        {
            objectType = typeof(GameObject),
            allowSceneObjects = true,
        };
        objectField.RegisterCallback<ChangeEvent<Object>, ObjectField>(static (evt, field) =>
        {
            if (evt.newValue == null) return;
            if (PrefabUtility.GetPrefabAssetType(evt.newValue) != PrefabAssetType.NotAPrefab)
            {
                field.value = null;
                Debug.LogError("Prefab is not supported.");
            }
        }, objectField);
        // when the object is clicked, the object is zoomed in
        objectField.RegisterCallback<MouseDownEvent, ObjectField>(static (_, field) =>
        {
            var target = field.value as GameObject; 
            if (target== null) return;
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return;
            sceneView.LookAt(target.transform.position);
            Selection.activeGameObject = target;
        }, objectField);
        
        root.Add(objectField);
        
        // name parameters
        root.Add(CreateHeader("Name Parameter"));
        
        var nameField = new TextField{ isReadOnly = true, style = { opacity = 0.5f }};
        root.Add(nameField);
        
        var genreField = new EnumField("ジャンル", Genre.Modern);
        root.Add(genreField);
        
        var routeField = new EnumField("経路形状", Route.Straight);
        root.Add(routeField);
        
        var partsField = new EnumField("パーツ種別", Parts.BeltConveyor);
        root.Add(partsField);

        var fields = (genreField, routeField, partsField, nameField);
        OnNameParameterChanged(default, fields);
        genreField.RegisterCallback<ChangeEvent<Enum>, ValueTuple<EnumField, EnumField, EnumField, TextField>>(OnNameParameterChanged, fields);
        routeField.RegisterCallback<ChangeEvent<Enum>, ValueTuple<EnumField, EnumField, EnumField, TextField>>(OnNameParameterChanged, fields);
        partsField.RegisterCallback<ChangeEvent<Enum>, ValueTuple<EnumField, EnumField, EnumField, TextField>>(OnNameParameterChanged, fields);
        
        // other parameters
        root.Add(CreateHeader("Other Parameter"));
        
        var heightOffsetField = new FloatField("Height Offset") { value = 0.3f };
        root.Add(heightOffsetField);
        
        var pathView = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
        var pathField = new TextField
        {
            value = DefaultSavePath, 
            isReadOnly = true, 
            style = { opacity = 0.5f, flexGrow = 1 }
        };
        var button = new Button(() =>
        {
            var newPath = EditorUtility.OpenFolderPanel("Select output location", pathField.value, "");
            if (!string.IsNullOrEmpty(newPath))
            {
                if (!newPath.Contains(Application.dataPath))
                {
                    Debug.LogError($"Selected path {newPath} must be in the Unity Assets directory");
                    return;
                }
                pathField.value = newPath.Replace(Application.dataPath, "Assets");
            }
        }) { text = "..." };
        pathView.Add(pathField);
        pathView.Add(button);
        root.Add(pathView);
        
        root.Add(new Button(() =>
        {
            if (objectField.value == null || string.IsNullOrEmpty(pathField.value))
            {
                Debug.LogError("Please set the target object and path.");
                return;
            }
            var target = Instantiate(objectField.value as GameObject);
            if (target == null) return;
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length < 1)
            {
                Debug.LogException(new InvalidOperationException("Renderer not found."));
                return;
            }
            
            var activeTransform = target.transform;
            activeTransform.position -= GetPivotPos(renderers, heightOffsetField.value);
            activeTransform.Rotate(0, -90, 0);
            
            var parent = new GameObject(nameField.value).transform;
            activeTransform.SetParent(parent);

            PrefabUtility.SaveAsPrefabAsset(parent.gameObject, $"{pathField.value}/{nameField.value}.prefab", out var success);
            DestroyImmediate(parent.gameObject);
            if (success)
            {
                Debug.Log("Prefab generated.");
            }
            else
            {
                Debug.LogError("Failed to generate prefab.");
            }
        }) { text = "Prefab生成" } );
        
        return;
        
        static void OnNameParameterChanged(ChangeEvent<Enum> _, (EnumField genre, EnumField route, EnumField parts, TextField name) fields)
        {
            var genre = fields.genre.value;
            var route = fields.route.value;
            var parts = fields.parts.value;
            fields.name.value = $"{genre}_{route}_{parts}";
        }

        static Vector3 GetPivotPos(in ReadOnlySpan<Renderer> renderers, float heightOffset)
        {
            var fullyBounds = renderers[0].bounds;
            foreach (var renderer in renderers[1..])
            {
                fullyBounds.Encapsulate(renderer.bounds);
            }
            var (min, max) = (fullyBounds.min, fullyBounds.max);
            return new Vector3(min.x, max.y - heightOffset, min.z);
        }
        
        static VisualElement CreateHeader(string text) 
            => new Label(text) { style = { unityFontStyleAndWeight = FontStyle.Bold } };
    }
}