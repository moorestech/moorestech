// 切り分けの順を「原始的な粉砕機だけ」→「粉砕機以外の全種類をまとめて」にする（enable-next.cs が使う）
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
const string Crusher = "e5a2b42a";
var all = ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Values.Select(b => b.BlockMasterElement.BlockGuid.ToString()).Distinct().ToList();
var crusher = all.First(g => g.StartsWith(Crusher));
System.AppDomain.CurrentDomain.SetData("moorestech.gpuBisectOrder", new List<string> { crusher, "REST" });
System.AppDomain.CurrentDomain.SetData("moorestech.gpuBisectExclude", crusher);
return "order: " + crusher.Substring(0, 8) + " then REST(" + (all.Count - 1) + " types)";
