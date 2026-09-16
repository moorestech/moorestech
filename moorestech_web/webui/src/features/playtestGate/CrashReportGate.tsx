import { L, useI18n } from "@/shared/i18n";
import { FullScreenGate } from "@/shared/ui";
import { CrashReportGateBody } from "./CrashReportGateBody";

// 外殻（不透明面・z層）はFullScreenGate、見せるかはapp層が持ち、本体は見せる間だけマウントされる
// FullScreenGate owns the shell (opaque face, z layer) and the app layer decides visibility; the body mounts only while shown
export function CrashReportGate({ visible }: { visible: boolean }) {
  const { t } = useI18n();

  return (
    <FullScreenGate
      visible={visible}
      testId="crash-report-gate"
      title={t(L.ui.playtest.crashGate.title)}
    >
      <CrashReportGateBody />
    </FullScreenGate>
  );
}
