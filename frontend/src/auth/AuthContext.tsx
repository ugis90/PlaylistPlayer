import {
  createContext,
  useContext,
  useState,
  ReactNode,
  useEffect,
} from "react";
import { apiClient } from "../api/client";

interface AuthContextType {
  isAuthenticated: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
  refreshToken: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(() => {
    return !!localStorage.getItem("token");
  });

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (token) {
      apiClient.defaults.headers.common["Authorization"] = `Bearer ${token}`;
    }
  }, []);

  const login = async (username: string, password: string) => {
    const response = await apiClient.post("/login", {
      userName: username,
      password,
    });
    const { accessToken } = response.data;
    localStorage.setItem("token", accessToken);
    apiClient.defaults.headers.common["Authorization"] =
      `Bearer ${accessToken}`;
    setIsAuthenticated(true);
  };

  const refreshToken = async () => {
    try {
      const response = await apiClient.post("/accessToken");
      const { accessToken } = response.data;
      localStorage.setItem("token", accessToken);
      apiClient.defaults.headers.common["Authorization"] =
        `Bearer ${accessToken}`;
    } catch (error) {
      await logout();
      throw error;
    }
  };

  const logout = async () => {
    try {
      await apiClient.post("/logout");
    } finally {
      localStorage.removeItem("token");
      delete apiClient.defaults.headers.common["Authorization"];
      setIsAuthenticated(false);
    }
  };

  return (
    <AuthContext.Provider
      value={{ isAuthenticated, login, logout, refreshToken }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within AuthProvider");
  return context;
};
