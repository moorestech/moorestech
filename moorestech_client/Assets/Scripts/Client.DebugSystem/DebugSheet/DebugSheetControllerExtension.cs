using System;
using Common.Debug;
using UnityDebugSheet.Runtime.Core.Scripts;
using UnityDebugSheet.Runtime.Core.Scripts.DefaultImpl.Cells;
using UnityEngine;

namespace Client.DebugSystem
{
    public static class DebugSheetControllerExtension
    {
        public static void AddEnumPickerWithSave<TEnum>(this DebugPage debugPage, TEnum defaultValue, string label, string key, Action<TEnum> valueChangedOrInitialize) where TEnum : Enum
        {
            // 保存値が現在のenum定義に存在しない場合はデフォルトへフォールバックする
            // Fall back to default if the saved value isn't a defined member of the current enum
            var savedInt = DebugParameters.GetValueOrDefaultInt(key, Convert.ToInt32(defaultValue));
            var value = Enum.IsDefined(typeof(TEnum), savedInt) ? (TEnum)Enum.ToObject(typeof(TEnum), savedInt) : defaultValue;
            valueChangedOrInitialize(value);

            // モデルを生成し、値変更時の保存と再通知を購読する
            // Build the model and subscribe save + re-notify on value change
            var model = new EnumPickerCellModel(value) { Text = label };
            model.ActiveValueChanged += d =>
            {
                DebugParameters.SaveInt(key, Convert.ToInt32(d));
                valueChangedOrInitialize((TEnum)d);
            };

            // enum型専用のプレハブキー（=専用プール）に登録し、型混在によるEnumPickerCellの添字例外を防ぐ
            // Register under an enum-type-specific prefab key (= dedicated pool) to prevent EnumPickerCell's index exception from mixed types
            var prefabKey = RegisterPerTypePrefabKey();
            debugPage.AddItem(prefabKey, model);

            #region Internal

            string RegisterPerTypePrefabKey()
            {
                // EnumPickerCellは値リストをインスタンスに一度だけキャッシュするため、1プールにつき単一enum型のみを割り当てる
                // EnumPickerCell caches its value list once per instance, so we allocate only a single enum type per pool
                var typeKey = "EnumPickerCell_" + typeof(TEnum).FullName;
                var container = debugPage.GetComponent<PrefabContainer>();
                if (container.TryGetPrefab(typeKey, out _)) return typeKey;

                // ベースのEnumPickerCellプレハブを複製し、専用キー名の非アクティブテンプレートとして登録する
                // Clone the base EnumPickerCell prefab and register it as an inactive template under the dedicated key
                var basePrefab = container.GetPrefab("EnumPickerCell");
                var template = UnityEngine.Object.Instantiate(basePrefab);
                template.transform.SetParent(debugPage.transform, false);
                template.name = typeKey;
                template.SetActive(false);
                container.AddPrefab(template);

                return typeKey;
            }

            #endregion
        }
        
        public static void AddBoolWithSave(this DebugPage debugPage, bool defaultValue, string label, string key, Action<bool> valueChangedOrInitialize = null)
        {
            var value = DebugParameters.GetValueOrDefaultBool(key, defaultValue);
            valueChangedOrInitialize?.Invoke(value);
            
            debugPage.AddSwitch(value, label, valueChanged: d =>
            {
                DebugParameters.SaveBool(key, d);
                valueChangedOrInitialize?.Invoke(d);
            });
        }
    }
}