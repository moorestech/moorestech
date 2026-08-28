import { create } from "zustand";

// stage基準で設計された長さを実画面の長さへ直す倍率。CSSは--ui-scaleを、JSはこのstoreを読む
// The factor converting stage-authored lengths into real screen lengths; CSS reads --ui-scale and JS reads this store
type UiScaleState = { scale: number; setScale: (scale: number) => void };

// stage外のPortal層は寸法計算をJSで実画面座標に対して行うため、CSS変数の読み直しでは変化を追えない
// Portal layers outside the stage do their sizing math in JS against real screen coordinates, so re-reading the CSS variable cannot track changes
export const useUiScaleStore = create<UiScaleState>((set) => ({
  scale: 1,
  setScale: (scale) => set((current) => (current.scale === scale ? current : { scale })),
}));

export function useUiScale(): number {
  return useUiScaleStore((state) => state.scale);
}
