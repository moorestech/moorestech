import { Topics } from "@/bridge";
import { FullScreenGate } from "@/shared/ui";
import { EventLanguageGateBody } from "./EventLanguageGateBody";

// 選ばせる対象が辞書そのものなのでt()を通さない。英語固定はADR 0040の裁定
// The dictionary itself is what gets chosen, so this bypasses t(); English-only is the ADR 0040 ruling
const HeadingText = "Select Language";

// 外殻（不透明面・z層・待機の購読）はFullScreenGateが持ち、本体は待機中だけマウントされる
// FullScreenGate owns the shell (opaque face, z layer, waiting subscription); the body mounts only while waiting
export function EventLanguageGate() {
  return (
    <FullScreenGate topic={Topics.eventLanguageGate} testId="event-language-gate" title={HeadingText}>
      <EventLanguageGateBody />
    </FullScreenGate>
  );
}
