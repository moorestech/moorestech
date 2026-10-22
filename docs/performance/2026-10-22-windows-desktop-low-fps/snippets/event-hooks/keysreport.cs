using System;
using System.Linq;
using System.Collections.Generic;
foreach (var s in (List<IDisposable>)AppDomain.CurrentDomain.GetData("k_subs")) s.Dispose();
var st = (Dictionary<string, int[]>)AppDomain.CurrentDomain.GetData("k_stats");
return string.Join("\n", st.OrderByDescending(k => k.Value[1]).Select(k => k.Key + " events=" + k.Value[0] + " valueChanged=" + k.Value[1]));
