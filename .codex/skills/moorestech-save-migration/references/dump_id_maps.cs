// Step 3: 独立 v8 マスタから item/fluid の id->GUID 全マップ + 定数を取得する。
// グローバル MasterHolder には触れない（別 mod が載っていることがあるため）。
// 実行: uloop execute-dynamic-code --project-path ./moorestech_client --code "$(cat references/dump_id_maps.cs)"
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Core.Master;
using Mooresmaster.Model.TrainModule;
using Server.Boot;
using Mod.Loader;
using Mod.Config;

var dir = ServerDirectory.GetDirectory();
var modDir = dir.EndsWith("/") ? dir + "mods" : dir + "/mods";   // Path.Combine は uloop で禁止
var container = new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(new ModsResource(modDir)));
JToken Tok(string name) => (JToken)JsonConvert.DeserializeObject(container.ConfigJsons[0].JsonContents[new JsonFileName(name)]);

var im = new ItemMaster(Tok("items")); im.Initialize();
var fm = new FluidMaster(Tok("fluids")); fm.Initialize();

var itemMap = new Dictionary<string,string>();
foreach (var id in im.GetItemAllIds()) itemMap[id.AsPrimitive().ToString()] = im.GetItemGuid(id).ToString();
var fluidMap = new Dictionary<string,string>();
foreach (var fid in fm.GetAllFluidIds()) fluidMap[fid.AsPrimitive().ToString()] = fm.GetFluidGuid(fid).ToString();

return JsonConvert.SerializeObject(new Dictionary<string,object>{
    ["items"]=itemMap, ["fluids"]=fluidMap,
    ["itemConst"]=TrainCarMasterElement.DefaultContainerTypeConst.Item,
    ["fluidConst"]=TrainCarMasterElement.DefaultContainerTypeConst.Fluid,
});
// 結果 .Result(JSON文字列) を Python 側で /tmp/id_maps.json に保存して使う。
