import { Text, Title } from "@mantine/core";
import { dispatchAction, Topics, useTopic, type SprintInputMode } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";

// 走りの操作方式の択一。現在値はsettings.sprintInputModeトピックを正とする
// Exclusive selector for the sprint input mode; the settings.sprintInputMode topic is authoritative
export function SprintModeSelect() {
  const { t } = useI18n();
  const preference = useTopic(Topics.sprintInputMode);
  const mode = preference?.mode;
  const label = t(L.ui.settings.sprintMode);

  return (
    <section aria-label={label}>
      <Title order={2}>{label}</Title>
      {preference?.recoveredInvalidPreference && <Text role="status">{t(L.ui.settings.sprintModeRecovered)}</Text>}
      {mode !== undefined && (
        <ModeSwitch<SprintInputMode>
          value={mode}
          options={[
            { value: "toggle", label: t(L.ui.settings.sprintModeToggle), testId: "sprint-mode-option-toggle" },
            { value: "hold", label: t(L.ui.settings.sprintModeHold), testId: "sprint-mode-option-hold" },
          ]}
          onChange={(next) => void dispatchAction("settings.setSprintInputMode", { mode: next })}
          orientation="horizontal"
          testId="sprint-mode-select"
        />
      )}
    </section>
  );
}
