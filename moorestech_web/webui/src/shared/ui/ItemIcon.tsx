import { itemIconUrl } from "@/bridge";
import GameIcon, { type IconFallback } from "./GameIcon";
import { useItemDisplayName } from "@/shared/i18n";

type Props = {
  itemId: number;
  // 失敗時に何を残すかは置かれる面が決める。液体と同じく呼び出し側が明示する
  // The face that hosts the icon decides what survives a failure; like fluids, the caller states it
  fallback: IconFallback;
  className?: string;
};

// altは常に表示名解決へ寄せ、本文とアイコンの呼び名を食い違わせない
// The alt always comes from the shared display name so icon and text never disagree
export default function ItemIcon({ itemId, fallback, className }: Props) {
  const itemDisplayName = useItemDisplayName();
  return (
    <GameIcon
      id={itemId}
      src={itemIconUrl(itemId)}
      alt={itemDisplayName(itemId)}
      className={className}
      fallback={fallback}
    />
  );
}
