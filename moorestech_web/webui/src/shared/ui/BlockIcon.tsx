import { blockIconUrl } from "@/bridge";
import GameIcon, { type IconFallback } from "./GameIcon";
import { L, useI18n } from "@/shared/i18n";

type Props = {
  blockId: number;
  alt?: string;
  // 失敗時に何を残すかは置かれる面が決める。液体と同じく呼び出し側が明示する
  // The face that hosts the icon decides what survives a failure; like fluids, the caller states it
  fallback: IconFallback;
  className?: string;
};

export default function BlockIcon({ blockId, alt, fallback, className }: Props) {
  const { t } = useI18n();
  return (
    <GameIcon
      id={blockId}
      src={blockIconUrl(blockId)}
      alt={alt ?? t(L.ui.common.blockFallback, { blockId })}
      className={className}
      fallback={fallback}
    />
  );
}
