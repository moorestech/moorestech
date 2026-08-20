using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.BuildMenuModule;
using Mooresmaster.Model.BuildMenuModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // ビルドツール（ブループリントコピー等）のマスタ。生ロードとGuid索引のみを保持する
    // Master for build tools (blueprint copy, etc.); holds only raw load and a Guid index
    public class BuildToolMaster : IMasterValidator
    {
        // BP機能全体の初期解放フラグ（buildMenuルート・C5b裁定）
        // Initial-unlock flag for the whole blueprint feature (buildMenu root key, C5b ruling)
        public bool BlueprintInitialUnlocked { get; }

        private readonly BuildToolMasterElement[] _buildTools;

        // buildToolGuid→要素の索引
        // buildToolGuid → element index
        private Dictionary<Guid, BuildToolMasterElement> _elementByGuid;

        public BuildToolMaster(JToken buildMenuJToken)
        {
            var buildMenu = BuildMenuLoader.Load(buildMenuJToken);
            _buildTools = buildMenu.BuildTools;
            BlueprintInitialUnlocked = buildMenu.BlueprintInitialUnlocked;
        }

        public bool Validate(out string errorLogs)
        {
            return BuildToolMasterUtil.Validate(_buildTools, out errorLogs);
        }

        public void Initialize()
        {
            _elementByGuid = new Dictionary<Guid, BuildToolMasterElement>();
            foreach (var element in _buildTools)
            {
                _elementByGuid.Add(element.BuildToolGuid, element);
            }
        }

        public IReadOnlyList<BuildToolMasterElement> All => _buildTools;

        public BuildToolMasterElement GetBuildTool(Guid buildToolGuid)
        {
            return _elementByGuid[buildToolGuid];
        }
    }
}
