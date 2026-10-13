import { useEffect, useRef, useState } from "react";
import { DEFAULT_ACTION_TIMEOUT_MS, dispatchAction } from "@/bridge";
import type { TrainTimetableStop } from "@/bridge";
import { sameStopOrder, toReplacePayload } from "./timetableEditLogic";

type PendingReplacement = { stops: TrainTimetableStop[]; accepted: boolean };

export function useImmediateTimetable(serverStops: TrainTimetableStop[]) {
  const [pending, setPending] = useState<PendingReplacement | null>(null);
  const sending = useRef(false);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  // ackと配信の両方を待ち、古い一覧を次の編集の起点にしない
  // Wait for both acceptance and publication before allowing another edit against the list
  useEffect(() => {
    if (!pending?.accepted || !sameStopOrder(serverStops, pending.stops)) return;
    sending.current = false;
    setPending(null);
  }, [pending, serverStops]);

  // 配信欠落で操作を永久にロックせず、既存の通信待ち時間後に権威へ戻す
  // Return to authority after the existing action timeout instead of locking editing forever
  useEffect(() => {
    if (!pending?.accepted) return;
    const timer = setTimeout(() => {
      console.warn("[TrainTimetable] accepted replacement was not published: showing the server timetable");
      sending.current = false;
      setPending(null);
    }, DEFAULT_ACTION_TIMEOUT_MS);
    return () => clearTimeout(timer);
  }, [pending]);

  async function replace(stops: TrainTimetableStop[]) {
    if (sending.current) {
      console.warn("[TrainTimetable] edit ignored while waiting for the previous replacement");
      return;
    }
    if (sameStopOrder(stops, serverStops)) return;
    sending.current = true;
    setPending({ stops, accepted: false });
    const accepted = await dispatchAction("train_timetable.replace", toReplacePayload(stops));
    if (!mounted.current) return;
    // 拒否時は最新propsへ戻す。通知はdispatchActionが担当する
    // Revert to the latest props on rejection; dispatchAction supplies the error notification
    if (!accepted) {
      console.warn("[TrainTimetable] replacement not accepted: showing the server timetable");
      sending.current = false;
      setPending(null);
      return;
    }
    setPending({ stops, accepted: true });
  }

  return { stops: pending?.stops ?? serverStops, isPending: pending !== null, replace };
}
