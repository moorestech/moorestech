// 計測を止めて Tick 一覧を元に戻す
using System;
using System.Reflection;
using Unity.Profiling;
var dom = AppDomain.CurrentDomain;
var tick = dom.GetData("ab_item");
tick.GetType().GetField("entries", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(tick, (VContainer.Unity.ITickable[])dom.GetData("ab_original"));
((ProfilerRecorder)dom.GetData("ab_recorder")).Dispose();
return "restored";
