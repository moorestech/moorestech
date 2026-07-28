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
        public readonly BuildToolMasterElement[] BuildTools;

        // buildToolGuid→要素の索引
        // buildToolGuid → element index
        private Dictionary<Guid, BuildToolMasterElement> _elementByGuid;

        public BuildToolMaster(JToken buildMenuJToken)
        {
            BuildTools = BuildMenuLoader.Load(buildMenuJToken).BuildTools;
        }

        public bool Validate(out string errorLogs)
        {
            return BuildToolMasterUtil.Validate(BuildTools, out errorLogs);
        }

        public void Initialize()
        {
            _elementByGuid = new Dictionary<Guid, BuildToolMasterElement>();
            foreach (var element in BuildTools)
            {
                _elementByGuid.Add(element.BuildToolGuid, element);
            }
        }

        public IReadOnlyList<BuildToolMasterElement> All => BuildTools;

        public BuildToolMasterElement GetBuildTool(Guid buildToolGuid)
        {
            return _elementByGuid[buildToolGuid];
        }
    }
}
