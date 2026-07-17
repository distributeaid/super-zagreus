import { render, screen } from "@testing-library/react";
import { ProjectCard } from "./ProjectCard";

function setup(props: { name: string; region: string | null; lastConfirmedAt: string | null }) {
  return render(<ProjectCard {...props} />);
}

test("shows 'Needs updating' when never confirmed", () => {
  setup({ name: "Aegean Hub", region: "Greece", lastConfirmedAt: null });
  expect(screen.getByText("Needs updating")).toBeInTheDocument();
  expect(screen.getByText("Not yet confirmed")).toBeInTheDocument();
});

test("shows 'Up to date' when recently confirmed", () => {
  const recent = new Date(Date.now() - 5 * 24 * 3600 * 1000).toISOString();
  setup({ name: "Aegean Hub", region: "Greece", lastConfirmedAt: recent });
  expect(screen.getByText("Up to date")).toBeInTheDocument();
});

test("renders the review CTA", () => {
  setup({ name: "Aegean Hub", region: null, lastConfirmedAt: null });
  expect(screen.getByRole("button", { name: /review & confirm needs/i })).toBeInTheDocument();
});
