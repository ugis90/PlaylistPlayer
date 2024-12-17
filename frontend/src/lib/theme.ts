import { createTheme } from "./create-theme";

export const theme = createTheme({
  colors: {
    primary: {
      50: "#f0f9ff",
      100: "#e0f2fe",
      500: "#0ea5e9",
      600: "#0284c7",
      700: "#0369a1",
    },
    secondary: {
      50: "#f8fafc",
      100: "#f1f5f9",
      500: "#64748b",
      600: "#475569",
      700: "#334155",
    },
    background: "#ffffff",
    foreground: "#0f172a",
  },
  fonts: {
    sans: '"Inter", system-ui, sans-serif',
    heading: '"Poppins", system-ui, sans-serif',
  },
});
