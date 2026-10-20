import { Text, Title } from "@mantine/core";
import { dispatchAction, Topics, useTopic, type GraphicsQualityPresetChoice, type GraphicsQualitySettings as Settings } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";

type BoolKey = "shadows" | "ssao" | "postProcessing" | "effects" | "mapObjects" | "ocean";
type OnOff = "on" | "off";

// 画質のプリセットと各項目。現在値はsettings.graphicsQualityトピックを正とする
// Graphics preset and items; the settings.graphicsQuality topic is authoritative
export function GraphicsQualitySettings() {
  const { t } = useI18n();
  const data = useTopic(Topics.graphicsQuality);
  const label = t(L.ui.settings.graphics);
  // topic未着では項目を描かない（既定値はUnity側が持つ）
  // Render no items before the topic arrives (Unity owns the default)
  if (!data) return <section aria-label={label}><Title order={2}>{label}</Title></section>;
  const settings = data.settings;
  const setItem = (next: Settings) => void dispatchAction("settings.setGraphicsQuality", { settings: next });
  const boolSwitch = (key: BoolKey, title: string) => (
    <div key={key}>
      <Text>{title}</Text>
      <ModeSwitch<OnOff>
        value={settings[key] ? "on" : "off"}
        options={[
          { value: "on", label: t(L.ui.settings.on), testId: `graphics-${key}-on` },
          { value: "off", label: t(L.ui.settings.off), testId: `graphics-${key}-off` },
        ]}
        onChange={(next) => setItem({ ...settings, [key]: next === "on" })}
        orientation="horizontal"
        testId={`graphics-${key}`}
      />
    </div>
  );

  return (
    <section aria-label={label}>
      <Title order={2}>{label}</Title>
      <Text>{t(L.ui.settings.graphicsPreset)}</Text>
      {/* カスタムはどの選択肢にも一致しないのでvalueをnull（無選択）にする */}
      {/* Custom matches no option, so the value is null (no selection) */}
      <ModeSwitch<GraphicsQualityPresetChoice>
        value={data.preset === "custom" ? null : data.preset}
        options={[
          { value: "high", label: t(L.ui.settings.graphicsPresetHigh), testId: "graphics-preset-high" },
          { value: "medium", label: t(L.ui.settings.graphicsPresetMedium), testId: "graphics-preset-medium" },
          { value: "low", label: t(L.ui.settings.graphicsPresetLow), testId: "graphics-preset-low" },
        ]}
        onChange={(next) => void dispatchAction("settings.applyGraphicsQualityPreset", { preset: next })}
        orientation="horizontal"
        testId="graphics-preset"
      />
      {data.preset === "custom" && <Text role="status" data-testid="graphics-preset-custom">{t(L.ui.settings.graphicsPresetCustom)}</Text>}
      <Text>{t(L.ui.settings.graphicsDrawDistance)}</Text>
      <ModeSwitch<string>
        value={String(settings.drawDistanceMeters)}
        options={[300, 1000, 5000].map((m) => ({ value: String(m), label: `${m}m`, testId: `graphics-draw-distance-${m}` }))}
        onChange={(next) => setItem({ ...settings, drawDistanceMeters: Number(next) as Settings["drawDistanceMeters"] })}
        orientation="horizontal"
        testId="graphics-draw-distance"
      />
      {boolSwitch("shadows", t(L.ui.settings.graphicsShadows))}
      <Text>{t(L.ui.settings.graphicsRenderScale)}</Text>
      <ModeSwitch<string>
        value={String(settings.renderScalePercent)}
        options={[100, 75, 50].map((p) => ({ value: String(p), label: `${p}%`, testId: `graphics-render-scale-${p}` }))}
        onChange={(next) => setItem({ ...settings, renderScalePercent: Number(next) as Settings["renderScalePercent"] })}
        orientation="horizontal"
        testId="graphics-render-scale"
      />
      {boolSwitch("ssao", t(L.ui.settings.graphicsSsao))}
      {boolSwitch("postProcessing", t(L.ui.settings.graphicsPostProcessing))}
      {boolSwitch("effects", t(L.ui.settings.graphicsEffects))}
      {boolSwitch("mapObjects", t(L.ui.settings.graphicsMapObjects))}
      <Text>{t(L.ui.settings.graphicsTerrainDetail)}</Text>
      <ModeSwitch<Settings["terrainDetail"]>
        value={settings.terrainDetail}
        options={[
          { value: "fine", label: t(L.ui.settings.graphicsTerrainFine), testId: "graphics-terrain-fine" },
          { value: "coarse", label: t(L.ui.settings.graphicsTerrainCoarse), testId: "graphics-terrain-coarse" },
        ]}
        onChange={(next) => setItem({ ...settings, terrainDetail: next })}
        orientation="horizontal"
        testId="graphics-terrain"
      />
      {boolSwitch("ocean", t(L.ui.settings.graphicsOcean))}
    </section>
  );
}
