using UnityEngine;

namespace Client.Common.PrefabPostProcess
{
    /// <summary>
    /// ロードしたprefabを使う前に加工する後処理1つ分
    /// One post-process step applied to a loaded prefab before it is used
    /// </summary>
    public interface IPrefabPostProcessor
    {
        // 加工が要るprefabかを返す。どの後処理もfalseなら複製しない
        // Whether this prefab needs processing; nothing is cloned when every step returns false
        bool IsTarget(GameObject prefab);

        // 非アクティブの隠しルート下の複製を加工する
        // Modify the clone that lives under the inactive hidden root
        void Process(GameObject template, PrefabPostProcessContext context);
    }
}
