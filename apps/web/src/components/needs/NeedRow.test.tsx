import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NeedRow } from "./NeedRow";

const NEED = { id: "i1", itemTypeId: "soap", name: "Soap", category: "Hygiene", quantity: 3, unit: "item" };

describe("NeedRow", () => {
  it("shows the item name and unit", () => {
    render(<NeedRow need={NEED} onQuantityChange={() => {}} onRemove={() => {}} />);
    expect(screen.getByText("Soap")).toBeInTheDocument();
    expect(screen.getByText("item")).toBeInTheDocument();
  });

  it("reports a new quantity when the input changes", async () => {
    const onQuantityChange = vi.fn();
    render(<NeedRow need={NEED} onQuantityChange={onQuantityChange} onRemove={() => {}} />);
    const input = screen.getByLabelText(/quantity/i);
    await userEvent.clear(input);
    await userEvent.type(input, "8");
    expect(onQuantityChange).toHaveBeenCalledWith(8);
  });

  it("reports removal", async () => {
    const onRemove = vi.fn();
    render(<NeedRow need={NEED} onQuantityChange={() => {}} onRemove={onRemove} />);
    await userEvent.click(screen.getByRole("button", { name: /remove/i }));
    expect(onRemove).toHaveBeenCalled();
  });
});
