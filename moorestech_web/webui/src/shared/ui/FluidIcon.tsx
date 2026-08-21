import { fluidIconUrl } from "@/bridge";
import GameIcon from "./GameIcon";
import { fluidNameKey, useI18n } from "@/shared/i18n";

type Props = {
  fluidGuid: string;
  className?: string;
};

// 液体アイコン。読み込み失敗時は背面フィルのみで表現するためフォールバック非表示
// Fluid icon; on load failure it falls back to nothing since the fill behind it already conveys the state
export default function FluidIcon({ fluidGuid, className }: Props) {
  const { t } = useI18n();
  return <GameIcon id={fluidGuid} src={fluidIconUrl(fluidGuid)} alt={t(fluidNameKey(fluidGuid))} fallback={null} className={className} />;
}
