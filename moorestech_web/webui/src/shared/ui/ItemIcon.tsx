import { itemIconUrl } from "@/bridge";
import GameIcon from "./GameIcon";
import { useItemDisplayName } from "@/shared/i18n";

type Props = {
  itemId: number;
  className?: string;
};

// altは常に表示名解決へ寄せ、本文とアイコンの呼び名を食い違わせない
// The alt always comes from the shared display name so icon and text never disagree
export default function ItemIcon({ itemId, className }: Props) {
  const itemDisplayName = useItemDisplayName();
  return (
    <GameIcon
      id={itemId}
      src={itemIconUrl(itemId)}
      alt={itemDisplayName(itemId)}
      className={className}
      fallback={{ kind: "idText" }}
    />
  );
}
