import { blockIconUrl } from "@/bridge";
import GameIcon from "./GameIcon";
import { L, useI18n } from "@/shared/i18n";

type Props = {
  blockId: number;
  alt?: string;
  className?: string;
};

export default function BlockIcon({ blockId, alt, className }: Props) {
  const { t } = useI18n();
  return (
    <GameIcon
      id={blockId}
      src={blockIconUrl(blockId)}
      alt={alt ?? t(L.ui.common.blockFallback, { blockId })}
      className={className}
      fallback={{ kind: "idText" }}
    />
  );
}
