import { Topics } from "@/bridge";
import { DictionaryIndependentText, L, useI18n } from "@/shared/i18n";
import { FullScreenGate } from "@/shared/ui";
import { PlaytestConsentGateBody } from "./PlaytestConsentGateBody";

// 外殻（不透明面・z層・待機の購読）はFullScreenGateが持ち、本体は待機中だけマウントされる
// FullScreenGate owns the shell (opaque face, z layer, waiting subscription); the body mounts only while waiting
export function PlaytestConsentGate() {
  const { t } = useI18n();

  return (
    <FullScreenGate
      topic={Topics.consentGate}
      testId="playtest-consent-gate"
      title={t(L.ui.playtest.consent.title, {}, DictionaryIndependentText.playtestConsentTitle)}
    >
      <PlaytestConsentGateBody />
    </FullScreenGate>
  );
}
