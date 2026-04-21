// Based on https://github.com/baba-s/Unity-SerializeReferenceExtensions

#if UNITY_2019_3_OR_NEWER

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
public class SubclassSelectorDrawer : PropertyDrawer
{
    // PropertyDrawerインスタンスはリスト要素間で使い回されるため、
    // 型配列はベース型をキーにしてキャッシュし、現在インデックスは毎OnGUIで再計算する
    // PropertyDrawer instances are reused across list elements, so cache type
    // arrays keyed by base type and recompute the current index every OnGUI
    Type cachedBaseType;
    Type[] inheritedTypes;
    string[] typePopupNameArray;
    string[] typeFullNameArray;
    int currentTypeIndex;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference) return;

        var baseType = GetType(property);
        if (cachedBaseType != baseType)
        {
            GetAllInheritedTypes(baseType);
            GetInheritedTypeNameArrays();
            cachedBaseType = baseType;
        }
        GetCurrentTypeIndex(property.managedReferenceFullTypename);

        int selectedTypeIndex = EditorGUI.Popup(GetPopupPosition(position), currentTypeIndex, typePopupNameArray);
        UpdatePropertyToSelectedTypeIndex(property, selectedTypeIndex);
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, true);
    }

    private void GetCurrentTypeIndex(string typeFullName)
    {
        currentTypeIndex = Array.IndexOf(typeFullNameArray, typeFullName);
    }

    // SerializeReferenceはUnityEngine.Object派生型を格納できない
    // SerializeReference cannot store UnityEngine.Object-derived instances
    void GetAllInheritedTypes(Type baseType)
    {
        Type unityObjectType = typeof(UnityEngine.Object);
        inheritedTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => baseType.IsAssignableFrom(p) && p.IsClass && !p.IsAbstract && !unityObjectType.IsAssignableFrom(p))
            .Prepend(null)
            .ToArray();
    }

    private void GetInheritedTypeNameArrays()
    {
        typePopupNameArray = inheritedTypes.Select(type =>
        {
            if (type == null) return "<null>";
            var nameAttr = type.GetCustomAttribute<SubclassSelectorNameAttribute>(inherit: false);
            return nameAttr != null ? nameAttr.DisplayName : type.Name;
        }).ToArray();
        typeFullNameArray = inheritedTypes.Select(type => type == null ? "" : string.Format("{0} {1}", type.Assembly.ToString().Split(',')[0], type.FullName)).ToArray();
    }

    public void UpdatePropertyToSelectedTypeIndex(SerializedProperty property, int selectedTypeIndex)
    {
        if (currentTypeIndex == selectedTypeIndex) return;
        currentTypeIndex = selectedTypeIndex;
        Type selectedType = inheritedTypes[selectedTypeIndex];
        property.managedReferenceValue =
            selectedType == null ? null : Activator.CreateInstance(selectedType);
    }

    Rect GetPopupPosition(Rect currentPosition)
    {
        Rect popupPosition = new Rect(currentPosition);
        popupPosition.width -= EditorGUIUtility.labelWidth;
        popupPosition.x += EditorGUIUtility.labelWidth;
        popupPosition.height = EditorGUIUtility.singleLineHeight;
        return popupPosition;
    }

    // ネストされたプロパティパスを完全に走査して型を解決する
    // Fully traverse nested property paths to resolve the field type
    public static Type GetType(SerializedProperty property)
    {
        const BindingFlags bindingAttr =
            BindingFlags.NonPublic |
            BindingFlags.Public |
            BindingFlags.FlattenHierarchy |
            BindingFlags.Instance;

        var propertyPaths = property.propertyPath.Split('.');
        var currentType = property.serializedObject.targetObject.GetType();

        for (int i = 0; i < propertyPaths.Length; i++)
        {
            // 配列セグメントの場合、要素型を取得してdata[N]をスキップ
            // For array segments, get element type and skip data[N]
            if (propertyPaths[i] == "Array")
            {
                if (currentType.IsArray)
                {
                    currentType = currentType.GetElementType();
                }
                else
                {
                    currentType = currentType.GetGenericArguments()[0];
                }
                i++;
                continue;
            }

            var fieldInfo = currentType.GetField(propertyPaths[i], bindingAttr);
            if (fieldInfo == null) return currentType;
            currentType = fieldInfo.FieldType;
        }

        return currentType;
    }
}
#endif
