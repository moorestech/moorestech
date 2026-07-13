using UnityEditor;
using UnityEngine;

namespace MapGenerator.Editor
{
    /// <summary>
    /// LabelAttributeに対応するPropertyDrawer。RangeAttributeとの併用にも対応する。
    /// </summary>
    [CustomPropertyDrawer(typeof(LabelAttribute))]
    public class LabelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (LabelAttribute)attribute;
            label.text = attr.DisplayName;

            // RangeAttributeが同時に付いている場合はスライダーで描画する
            var rangeAttrs = fieldInfo.GetCustomAttributes(typeof(RangeAttribute), true);
            if (rangeAttrs.Length > 0)
            {
                var range = (RangeAttribute)rangeAttrs[0];
                if (property.propertyType == SerializedPropertyType.Float)
                    EditorGUI.Slider(position, property, range.min, range.max, label);
                else if (property.propertyType == SerializedPropertyType.Integer)
                    EditorGUI.IntSlider(position, property, (int)range.min, (int)range.max, label);
                else
                    DrawDefault(position, property, label);
            }
            else
            {
                DrawDefault(position, property, label);
            }
        }

        /// <summary>
        /// Range非対応フィールドの描画。float/intのリーフ値は型別コントロールで直接描画する。
        /// EditorGUI.PropertyField を PropertyDrawer の内側から呼ぶと、配列ネスト
        /// (OreBand[] の outerRadiusMeters 等) でプロパティハンドラが再入し、
        /// 「見た目はあるがクリック・入力できない」死んだフィールドになるため避ける。
        /// Range付きフィールドが EditorGUI.Slider を直接呼ぶのと同じ方針。
        /// Vector2 やネストクラス等の複合型は従来どおり PropertyField にフォールバックする
        /// （これらはトップレベル参照のみで再入問題が出ない）。
        /// </summary>
        private static void DrawDefault(Rect position, SerializedProperty property, GUIContent label)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    EditorGUI.BeginProperty(position, label, property);
                    EditorGUI.BeginChangeCheck();
                    float fv = EditorGUI.FloatField(position, label, property.floatValue);
                    if (EditorGUI.EndChangeCheck()) property.floatValue = fv;
                    EditorGUI.EndProperty();
                    break;
                case SerializedPropertyType.Integer:
                    EditorGUI.BeginProperty(position, label, property);
                    EditorGUI.BeginChangeCheck();
                    int iv = EditorGUI.IntField(position, label, property.intValue);
                    if (EditorGUI.EndChangeCheck()) property.intValue = iv;
                    EditorGUI.EndProperty();
                    break;
                default:
                    EditorGUI.PropertyField(position, property, label, true);
                    break;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
