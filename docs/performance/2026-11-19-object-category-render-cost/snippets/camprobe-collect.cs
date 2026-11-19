// camprobe-start.cs の結果を回収し、終わっていれば後始末（戻し・PlayerLoop から外す）をして JSON で返す
var d = System.AppDomain.CurrentDomain;
var results = d.GetData("camprobe.results") as System.Collections.Generic.List<string>;
if (results == null) return "{\"error\":\"not running\"}";
var done = (bool)d.GetData("camprobe.done");
var err = (string)d.GetData("camprobe.error");
if (!done) return "{\"running\":true,\"windows\":" + results.Count + "}";
((System.Action)d.GetData("camprobe.cleanup"))();
var stats = (System.Collections.Generic.List<string>)d.GetData("camprobe.stats");
d.SetData("camprobe.results", null);
return "{\"batch\":" + (UnityEngine.Application.isBatchMode ? "true" : "false") + ",\"error\":\"" + err.Replace("\\", "/").Replace("\"", "'").Replace("\n", " | ") + "\",\"stats\":[" + string.Join(",", stats) + "],\"windows\":[" + string.Join(",", results) + "]}";
