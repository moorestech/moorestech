using System.Collections.Generic;
using UnityEngine;

namespace Client.Common.PrefabPostProcess
{
    /// <summary>
    /// ロードしたprefabを非アクティブの隠しルートへ複製し、登録順に後処理をかける
    /// 元のassetは書き換えない(EditorのAssetDatabase読み込みではprefabファイル自体が変わるため)
    /// Clones loaded prefabs under an inactive hidden root and runs the registered steps in order
    /// The source asset is never modified, because with AssetDatabase loading in the Editor that would rewrite the prefab file
    /// </summary>
    public sealed class PrefabPostProcessPipeline
    {
        private readonly string _rootName;
        private readonly IReadOnlyList<IPrefabPostProcessor> _processors;
        private readonly PrefabPostProcessContext _context = new();
        private Transform _hiddenRoot;

        public PrefabPostProcessPipeline(string rootName, IReadOnlyList<IPrefabPostProcessor> processors)
        {
            _rootName = rootName;
            _processors = processors;
        }

        public GameObject Process(GameObject source)
        {
            // 対象の後処理が無ければ元のassetをそのまま使う
            // Use the source asset as is when no step targets it
            var targets = CollectTargets();
            if (targets.Count == 0) return source;

            // 親が非アクティブなのでAwake/OnEnableは走らない。以後のInstantiateで初めて有効になる
            // The inactive parent keeps Awake/OnEnable from running; instances made from it later activate normally
            var template = Object.Instantiate(source, GetOrCreateHiddenRoot(), false);
            template.name = source.name;
            foreach (var processor in targets) processor.Process(template, _context);
            return template;

            #region Internal

            List<IPrefabPostProcessor> CollectTargets()
            {
                var result = new List<IPrefabPostProcessor>();
                foreach (var processor in _processors)
                {
                    if (processor.IsTarget(source)) result.Add(processor);
                }
                return result;
            }

            Transform GetOrCreateHiddenRoot()
            {
                if (_hiddenRoot != null) return _hiddenRoot;
                var root = new GameObject(_rootName);
                root.SetActive(false);
                // シーン切り替えで消えないようにする。EditModeのテストではDontDestroyOnLoadが使えない
                // Survive scene switches; DontDestroyOnLoad is unavailable in EditMode tests
                if (Application.isPlaying) Object.DontDestroyOnLoad(root);
                _hiddenRoot = root.transform;
                return _hiddenRoot;
            }

            #endregion
        }
    }
}
