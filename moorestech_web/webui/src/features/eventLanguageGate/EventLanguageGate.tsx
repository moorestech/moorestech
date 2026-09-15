import { FullScreenGate } from "@/shared/ui";
import { EventLanguageGateBody } from "./EventLanguageGateBody";

// 選ばせる対象が辞書そのものなのでt()を通さない。英語固定はADR 0040の裁定
// The dictionary itself is what gets chosen, so this bypasses t(); English-only is the ADR 0040 ruling
const HeadingText = "Select Language";

// 外殻（不透明面・z層）はFullScreenGate、見せるかはapp層が持ち、本体は見せる間だけマウントされる
// FullScreenGate owns the shell (opaque face, z layer) and the app layer decides visibility; the body mounts only while shown
export function EventLanguageGate({ visible }: { visible: boolean }) {
  return (
    <FullScreenGate visible={visible} testId="event-language-gate" title={HeadingText}>
      <EventLanguageGateBody />
    </FullScreenGate>
  );
}
