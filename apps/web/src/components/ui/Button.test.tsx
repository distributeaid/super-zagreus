import { render, screen } from "@testing-library/react";
import { Button } from "./Button";

function setup(ui: React.ReactNode) { return render(ui); }

test("renders its label with DA primary tokens", () => {
  setup(<Button>Go</Button>);
  const b = screen.getByRole("button", { name: "Go" });
  expect(b).toHaveClass("bg-da-blue");
  expect(b).toHaveClass("text-white");
});

test("uses secondary tokens when asked", () => {
  setup(<Button variant="secondary">Alt</Button>);
  expect(screen.getByRole("button", { name: "Alt" })).toHaveClass("bg-da-lavender");
});
