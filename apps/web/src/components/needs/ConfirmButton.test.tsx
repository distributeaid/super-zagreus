import { render, screen } from "@testing-library/react";
import { ConfirmButton } from "./ConfirmButton";

describe("ConfirmButton", () => {
  it("is enabled when there are needs", () => {
    render(<ConfirmButton disabled={false} onConfirm={() => {}} />);
    expect(screen.getByRole("button", { name: /confirm current needs/i })).toBeEnabled();
  });

  it("is disabled when the list is empty", () => {
    render(<ConfirmButton disabled={true} onConfirm={() => {}} />);
    expect(screen.getByRole("button", { name: /confirm current needs/i })).toBeDisabled();
  });
});
