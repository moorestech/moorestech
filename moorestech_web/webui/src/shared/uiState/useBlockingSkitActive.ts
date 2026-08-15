import { useTopicSelector, Topics } from "@/bridge";

// blocking スキット中は HUD を退け、会話演出に画面を明け渡す
// During a blocking skit the HUD withdraws so the dialogue presentation owns the screen
export function useBlockingSkitActive(): boolean {
  return useTopicSelector(Topics.skitPresentation, (value) => value?.presentationState.mode === "blocking");
}
