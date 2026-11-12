// Profiler の記録を止める。重いフレームを含む記録は止めた後のフレームで読めるようになるので、集計は少し待ってから別に呼ぶ
// Stop profiler recording; frames from a heavy recording become readable on later frames, so collect separately after a short wait
using UnityEditorInternal;
ProfilerDriver.enabled = false;
return "profiler stopped frame=" + UnityEngine.Time.frameCount;
