import { render, screen } from "@testing-library/react";
import { CurrentNeeds } from "./CurrentNeeds";

const noop = async () => {};

describe("CurrentNeeds", () => {
  it("groups items by category and shows the last-confirmed date", () => {
    render(<CurrentNeeds lastConfirmedAt="2026-07-01T00:00:00Z" onReview={noop} items={[
      { id: "i1", itemTypeId: "soap", name: "Soap", category: "Hygiene", quantity: 3, unit: "item" },
    ]} />);
    expect(screen.getByText("Hygiene")).toBeInTheDocument();
    expect(screen.getByText("Soap")).toBeInTheDocument();
    expect(screen.getByText(/Last confirmed/)).toBeInTheDocument();
  });

  it("shows an empty state when there are no needs yet", () => {
    render(<CurrentNeeds lastConfirmedAt={null} onReview={noop} items={[]} />);
    expect(screen.getByText(/no needs recorded yet/i)).toBeInTheDocument();
  });

  it("renders the review CTA", () => {
    render(<CurrentNeeds lastConfirmedAt={null} onReview={noop} items={[]} />);
    expect(screen.getByRole("button", { name: /review & update needs/i })).toBeInTheDocument();
  });
});
