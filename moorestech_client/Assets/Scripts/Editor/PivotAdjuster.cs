using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

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
    
    private const string UndoAlignPivot = nameof(UndoAlignPivot);
    private const float HeightOffsetDefault = 0.3f;
    
    [MenuItem("Tools/Pivot Adjuster")]
    private static void ShowWindow() => GetWindow<PivotAdjuster>(nameof(PivotAdjuster));
    
    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.paddingTop = 10;
 
        var objectsListView = new ListView
        {
            reorderMode = ListViewReorderMode.Animated,
            showAddRemoveFooter = true,
            showBorder = true,
            showFoldoutHeader = true,
            headerTitle = "Target Objects",
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            viewController =
            {
                
            },
            makeItem = () =>
            {
                Debug.Log("makeItem");
                var objectField = new ObjectField()
                {
                    objectType = typeof(GameObject),
                    allowSceneObjects = true,
                };
                objectField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue == null) return;
                    if (PrefabUtility.GetPrefabAssetType(evt.newValue) != PrefabAssetType.NotAPrefab)
                    {
                        objectField.value = null;
                        Debug.LogError("Prefab is not supported.");
                    }
                });
                return objectField;
            },
        };
        
        root.Add(objectsListView);
        
        // header
        root.Add(new Label("Name Parameter") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        
        var nameField = new TextField
        {
            isReadOnly = true,
            style = { opacity = 0.5f },
        };
        root.Add(nameField);
        
        var genreField = new EnumField("ジャンル", Genre.Modern);
        root.Add(genreField);
        
        var routeField = new EnumField("経路形状", Route.Straight);
        root.Add(routeField);
        
        var partsField = new EnumField("パーツ種別", Parts.BeltConveyor);
        root.Add(partsField);

        OnNameParameterChanged(default);
        genreField.RegisterValueChangedCallback(OnNameParameterChanged);
        routeField.RegisterValueChangedCallback(OnNameParameterChanged);
        partsField.RegisterValueChangedCallback(OnNameParameterChanged);
        
        root.Add(new Button() { name = "Prefab生成" } );
        
        return;
        
        void OnNameParameterChanged(ChangeEvent<Enum> _)
        {
            var genre = genreField.value;
            var route = routeField.value;
            var parts = partsField.value;
            nameField.value = $"{genre}_{route}_{parts}";
        }
    }
    
    /// <summary>
    /// ゲームシーン上の選択中のゲームオブジェクトのピボットを調整します.
    /// </summary>
    /// <exception cref="Exception">プレハブはサポートされていません.</exception>
    [MenuItem("GameObject/Adjust Pivot")]
    private static void AdjustPivot()
    {
        // 複数選択対応
        foreach (var activeTransform in Selection.transforms)
        {
            var renderers = activeTransform.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length < 1)
            {
                throw new InvalidOperationException("Renderer not found.");
            }
            
            var root = new GameObject(activeTransform.name).transform;
            root.position = GetPivotPos(renderers, HeightOffsetDefault);
            
            Undo.RegisterCreatedObjectUndo(root.gameObject, UndoAlignPivot);
            Undo.SetTransformParent(activeTransform, root, UndoAlignPivot);
            
            activeTransform.SetParent(root);
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
    }
}