using System;
using Client.Game.GameDebug;
using UnityDebugSheet.Runtime.Core.Scripts;

namespace Client.DebugSystem
{
    public static class DebugSheetControllerExtension
    {
        public static void AddEnumPickerWithSave<TEnum>(this DebugPage debugPage, TEnum defaultValue, string label, string key, Action<TEnum> valueChangedOrInitialize) where TEnum : Enum
        {
            var value = (TEnum)Enum.ToObject(typeof(TEnum), DebugParameters.GetInt(key, Convert.ToInt32(defaultValue)));
            valueChangedOrInitialize(value);
            
            debugPage.AddEnumPicker(value, label, activeValueChanged: d =>
            {
                DebugParameters.SaveInt(key, Convert.ToInt32(d));
                valueChangedOrInitialize((TEnum)d);
            });
        }
    }
}