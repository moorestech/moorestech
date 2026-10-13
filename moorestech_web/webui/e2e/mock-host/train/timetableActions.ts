import { Topics, type ActionPayloads } from "../../../src/bridge/transport/protocol";
import { state, subscribersOf } from "../state";
import { clone, send } from "../wire";

type Result = { handled: false } | { handled: true; error?: string };

export function applyTrainTimetableAction(type: string, payload: unknown): Result {
  if (type !== "train_timetable.replace" && type !== "train_timetable.set_auto_run" && type !== "train_timetable.open") return { handled: false };
  const block = state.currentBlock;
  if (!block.open || block.source !== "train" || block.timetable.kind !== "ready") return { handled: true, error: "train_not_open" };
  const timetable = block.timetable.data;
  // 本番同様に置換後の一覧と現在駅を配信し、ackだけの成功で済ませない
  // Publish replaced stops and current index like production instead of acknowledging a no-op
  if (type === "train_timetable.replace") {
    const { stops } = payload as ActionPayloads["train_timetable.replace"];
    timetable.stops = stops.map(({ x, y, z, side }) => ({
      position: { x, y, z }, side,
      name: timetable.stations.find(s => s.position.x === x && s.position.y === y && s.position.z === z)?.name ?? "",
    }));
    timetable.currentIndex = stops.length > 0 ? 0 : -1;
    if (stops.length === 0) timetable.isAutoRun = false;
  }
  if (type === "train_timetable.set_auto_run") {
    const { enabled } = payload as ActionPayloads["train_timetable.set_auto_run"];
    if (enabled && timetable.stops.length === 0) return { handled: true, error: "set_auto_run_not_applied:False" };
    timetable.isAutoRun = enabled;
  }
  // ackとtopicの到着順の差をブラウザ検証でも通す
  // Exercise the separate ack and topic arrival paths in browser checks
  const published = clone(block);
  setTimeout(() => {
    for (const ws of subscribersOf(Topics.blockInventory)) send(ws, { op: "event", topic: Topics.blockInventory, data: published });
  }, 30);
  return { handled: true };
}
