import type { BlockInventoryOpen } from "@/bridge";
import { ElectricNetworkSection } from "../details/NetworkSections";

export default function ElectricPoleInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <div data-testid="electric-pole-view">
      <ElectricNetworkSection data={data} />
    </div>
  );
}
