import { useTopic, Topics } from "@/bridge";
import { percentWidth } from "./progressLogic";

// uGUI ProgressBarView を模した表示専用オーバーレイ。visible で Show/Hide を切り替える。
// Display-only overlay mirroring uGUI ProgressBarView; visible toggles Show/Hide.
export function ProgressBar() {
  const data = useTopic(Topics.progress);

  // 初回スナップショット前(null)や非表示時は何も描画しない。
  // Render nothing before the first snapshot (null) or while hidden.
  if (!data || !data.visible) return null;

  // 画面下部中央に固定し、任意ラベル・トラック・割合フィルを重ねる。
  // Pin to the bottom-center, layering the optional label, track, and proportional fill.
  return (
    <div
      data-testid="progress-bar"
      className="pointer-events-none fixed bottom-8 left-1/2 -translate-x-1/2 w-64 z-20"
    >
      {data.label !== null && (
        <div className="text-sm text-gray-300 mb-1">{data.label}</div>
      )}
      <div className="bg-gray-700 rounded h-3 overflow-hidden">
        <div
          data-testid="progress-fill"
          className="bg-green-500 h-full rounded"
          style={{ width: percentWidth(data.progress) }}
        />
      </div>
    </div>
  );
}
