import type { ButtonHTMLAttributes, ReactNode } from "react";
const VARIANTS = { primary: "bg-da-blue text-white", secondary: "bg-da-lavender text-da-blue" } as const;
export function Button({ children, variant = "primary", className = "", ...rest }:
  { children: ReactNode; variant?: keyof typeof VARIANTS } & ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button className={`rounded px-da-md py-da-sm font-sans font-medium ${VARIANTS[variant]} ${className}`} {...rest}>
      {children}
    </button>
  );
}
