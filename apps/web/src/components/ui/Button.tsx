import type { ButtonHTMLAttributes, ReactNode } from "react";
const VARIANTS = { primary: "bg-da-blue text-white", secondary: "bg-da-lavender text-da-blue" } as const;

/**
 * DistributeAid-styled button primitive. Forwards all native button props.
 *
 * @param variant `"primary"` (da-blue) or `"secondary"` (da-lavender). Defaults to `"primary"`.
 * @param className Extra classes appended after the variant classes.
 */
export function Button({ children, variant = "primary", className = "", ...rest }:
  { children: ReactNode; variant?: keyof typeof VARIANTS } & ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button className={`rounded px-da-md py-da-sm font-sans font-medium ${VARIANTS[variant]} ${className}`} {...rest}>
      {children}
    </button>
  );
}
