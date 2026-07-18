import type { Config } from "tailwindcss";
const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  theme: {
    container: { center: true, padding: { DEFAULT: "16px", md: "24px", lg: "32px" } },
    extend: {
      colors: { "da-blue": "#051E5D", "da-lavender": "#DFCDE8", "da-teal": "#98BEC6", "da-green": "#5AC597" },
      spacing: { "da-sm": "8px", "da-md": "16px", "da-lg": "32px", "da-xl": "64px" },
      fontFamily: {
        sans: ["var(--font-roboto)", "system-ui", "sans-serif"],
        marker: ["var(--font-permanent-marker)", "cursive"],
      },
    },
  },
  plugins: [],
};
export default config;
