using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.BuildMenuModule;
using Mooresmaster.Model.BuildMenuModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // 接続ツール（電線・レール・歯車チェーン）のマスタ。生ロードとGuid索引のみを保持する
    // Master for connect tools (electric wire, rail, gear chain); holds only raw load and a Guid index
    public class ConnectToolMaster : IMasterValidator
    {
        public readonly ConnectToolMasterElement[] ConnectTools;

        // connectToolGuid→要素の索引
        // connectToolGuid → element index
        private Dictionary<Guid, ConnectToolMasterElement> _elementByGuid;

        public ConnectToolMaster(JToken buildMenuJToken)
        {
            ConnectTools = BuildMenuLoader.Load(buildMenuJToken).ConnectTools;
        }

        public bool Validate(out string errorLogs)
        {
            return ConnectToolMasterUtil.Validate(ConnectTools, out errorLogs);
        }

        public void Initialize()
        {
            _elementByGuid = new Dictionary<Guid, ConnectToolMasterElement>();
            foreach (var element in ConnectTools)
            {
                _elementByGuid.Add(element.ConnectToolGuid, element);
            }
        }

        public IReadOnlyList<ConnectToolMasterElement> All => ConnectTools;

        public ConnectToolMasterElement GetElementOrNull(Guid connectToolGuid)
        {
            return _elementByGuid.GetValueOrDefault(connectToolGuid);
        }
    }
}
