// render-start.cs で溜めた値をフレーム平均にして JSON で返し、記録を止める。時間は ns を ms に直す
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
var d = System.AppDomain.CurrentDomain;
var names = (string[])d.GetData("bench.render.names");
var recs = (ProfilerRecorder[])d.GetData("bench.render.recs");
var ci = System.Globalization.CultureInfo.InvariantCulture;
var parts = new List<string>();
for (var i = 0; i < names.Length; i++)
{
    var r = recs[i];
    var samples = new List<ProfilerRecorderSample>(r.Capacity); r.CopyTo(samples);
    var values = samples.Select(s => (double)s.Value).ToList();
    var isTime = r.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
    var avg = values.Count == 0 ? 0 : values.Average() / (isTime ? 1e6 : 1);
    parts.Add("\"" + names[i] + "\":{\"avg\":" + avg.ToString("F3", ci) + ",\"frames\":" + values.Count + ",\"unit\":\"" + (isTime ? "ms" : "count") + "\"}");
    r.Dispose();
}
d.SetData("bench.render.recs", null);
return "{" + string.Join(",", parts) + "}";
