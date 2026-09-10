import { fluidIconUrl } from "@/bridge";
import GameIcon, { type IconFallback, type IconRenderResult } from "./GameIcon";
import { fluidNameKey, useI18n } from "@/shared/i18n";

type Props = {
  fluidGuid: string;
  // 失敗時に何を残すかは置かれる面が決める。背面フィルのある面は none、白面だけの面は識別子を残す
  // The face that hosts the icon decides what survives a failure: none where a fill remains, an identifier where only a white face does
  fallback: IconFallback;
  className?: string;
  onRenderResult?: (result: IconRenderResult) => void;
};

export default function FluidIcon({ fluidGuid, fallback, className, onRenderResult }: Props) {
  const { t } = useI18n();
  return (
    <GameIcon
      id={fluidGuid}
      src={fluidIconUrl(fluidGuid)}
      alt={t(fluidNameKey(fluidGuid))}
      fallback={fallback}
      className={className}
      onRenderResult={onRenderResult}
    />
  );
}
