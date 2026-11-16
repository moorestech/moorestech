using System.Collections.Generic;
using UnityEngine;

namespace Client.Localization
{
    /// <summary>
    /// 辞書の snapshot 1 つぶんの、三層とも空だったキーの報告済み集合。同じキーは 1 回だけ警告する（Web の i18nStore の欠けの警告の写し）
    /// Keys already reported as missing in every layer for one dictionary snapshot; each key warns once (mirrors the Web i18nStore warning)
    /// </summary>
    internal sealed class LocalizationMissingTextReport
    {
        private readonly HashSet<string> _reportedKeys = new();

        public void Report(string key)
        {
            // 文言は別スレッドからも引かれうるので、集合の出し入れだけ lock で守る
            // Text can be looked up off the main thread, so only the set access is locked
            lock (_reportedKeys)
            {
                if (!_reportedKeys.Add(key)) return;
            }
            Debug.LogWarning($"[Localize] missing text in every layer: {key}");
        }
    }
}
