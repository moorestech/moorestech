// Based on https://github.com/baba-s/Unity-SerializeReferenceExtensions

using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class SubclassSelectorAttribute : PropertyAttribute
{
}

// サブクラス選択ポップアップ上に表示する名前をクラスごとに上書きする
// Overrides the display name shown in the subclass-selector popup per class
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class SubclassSelectorNameAttribute : Attribute
{
    public string DisplayName { get; }

    public SubclassSelectorNameAttribute(string displayName)
    {
        DisplayName = displayName;
    }
}
