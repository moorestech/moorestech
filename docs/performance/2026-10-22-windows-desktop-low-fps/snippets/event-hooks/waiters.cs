using System;
using System.Collections;
using System.Reflection;
using System.Text;
var bf = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
var api = Client.Game.InGame.Context.ClientContext.VanillaApi;
var pem = api.Response.GetType().GetField("_packetExchangeManager", bf).GetValue(api.Response);
var waiters = (IDictionary)pem.GetType().GetField("_responseWaiters", bf).GetValue(pem);
var sb = new StringBuilder();
Action<object, int> dump = null;
var seen = new System.Collections.Generic.HashSet<object>();
dump = (o, depth) => {
  if (o == null || depth > 6 || !seen.Add(o)) return;
  var t = o.GetType();
  if (t.FullName.Contains("d__") || t.FullName.Contains(">d")) sb.Append(" SM:" + t.FullName);
  if (o is Delegate d) { dump(d.Target, depth + 1); return; }
  if (t.IsPrimitive || o is string) return;
  foreach (var f in t.GetFields(bf)) { if (f.FieldType.IsPrimitive) continue; object v; v = f.GetValue(o); if (v is Array arr) { foreach (var e in arr) dump(e, depth + 1); } else dump(v, depth + 1); }
};
foreach (DictionaryEntry kv in waiters) { sb.Append("seq " + kv.Key + ":"); dump(kv.Value, 0); sb.AppendLine(); }
return sb.ToString();
