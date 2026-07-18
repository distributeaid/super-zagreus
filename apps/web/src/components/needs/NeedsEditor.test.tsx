import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NeedsEditor } from "./NeedsEditor";

const actions = vi.hoisted(() => ({
  addNeed: vi.fn(),
  updateQuantity: vi.fn(),
  removeNeed: vi.fn(),
  confirmNeeds: vi.fn(),
}));
vi.mock("@/data/needsActions", () => actions);

const CATALOG = [{ id: "soap", name: "Soap", category: "Hygiene", defaultUnit: { id: "u1", name: "item" } }];
const ITEMS = [{ id: "i1", itemTypeId: "soap", name: "Soap", category: "Hygiene", quantity: 3, unit: "item" }];

describe("NeedsEditor", () => {
  beforeEach(() => {
    for (const fn of Object.values(actions)) fn.mockReset();
  });

  it("renders needs grouped by category", () => {
    render(<NeedsEditor draftId="d1" items={ITEMS} catalog={CATALOG} />);
    expect(screen.getByText("Hygiene")).toBeInTheDocument();
    expect(screen.getByText("Soap")).toBeInTheDocument();
  });

  it("removing a row calls removeNeed with the draft and item ids", async () => {
    render(<NeedsEditor draftId="d1" items={ITEMS} catalog={CATALOG} />);
    await userEvent.click(screen.getByRole("button", { name: /remove/i }));
    expect(actions.removeNeed).toHaveBeenCalledWith("d1", "i1");
  });

  it("disables confirm when there are no needs", () => {
    render(<NeedsEditor draftId="d1" items={[]} catalog={CATALOG} />);
    expect(screen.getByRole("button", { name: /confirm current needs/i })).toBeDisabled();
  });
});
