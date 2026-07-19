import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AddNeed } from "./AddNeed";

const CATALOG = [
  { id: "soap", name: "Soap", category: "Hygiene", defaultUnit: { id: "u1", name: "item" } },
  { id: "rice", name: "Rice", category: "Food", defaultUnit: { id: "u2", name: "kg" } },
];

describe("AddNeed", () => {
  it("filters the catalog as the user types and adds the chosen item", async () => {
    const onAdd = vi.fn();
    render(<AddNeed catalog={CATALOG} onAdd={onAdd} />);

    await userEvent.type(screen.getByRole("searchbox", { name: /add a need/i }), "ric");
    expect(screen.queryByRole("button", { name: /Soap/ })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /Rice/ }));

    expect(onAdd).toHaveBeenCalledWith(CATALOG[1]);
  });

  it("shows no results list until the user types", () => {
    render(<AddNeed catalog={CATALOG} onAdd={() => {}} />);
    expect(screen.queryByRole("button", { name: "Rice" })).not.toBeInTheDocument();
  });
});
