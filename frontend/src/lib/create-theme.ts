export interface ThemeColors {
  primary: {
    50: string;
    100: string;
    500: string;
    600: string;
    700: string;
  };
  secondary: {
    50: string;
    100: string;
    500: string;
    600: string;
    700: string;
  };
  background: string;
  foreground: string;
}

export interface ThemeFonts {
  sans: string;
  heading: string;
}

export interface Theme {
  colors: ThemeColors;
  fonts: ThemeFonts;
}

export function createTheme(theme: Theme): Theme {
  return theme;
}
