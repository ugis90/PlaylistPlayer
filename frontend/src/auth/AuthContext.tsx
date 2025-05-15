import {
  createContext,
  useContext,
  useState,
  ReactNode,
  useEffect,
  useCallback,
} from "react";
import { apiClient } from "../api/client";
import { toast } from "sonner";

interface UserInfo {
  username: string;
  email: string;
  role: string;
}

interface AuthContextType {
  isAuthenticated: boolean;
  login: (
    username: string,
    password: string,
    skipApiCall?: boolean,
  ) => Promise<void>;
  logout: () => Promise<void>;
  refreshToken: () => Promise<void>;
  userInfo: UserInfo | null;
  hasRole: (roleOrRoles: string | string[]) => boolean;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(() => {
    return !!localStorage.getItem("token");
  });

  const [userInfo, setUserInfo] = useState<UserInfo | null>(() => {
    const storedUserInfo = localStorage.getItem("userInfo");
    try {
      const parsed = storedUserInfo ? JSON.parse(storedUserInfo) : null;
      if (parsed?.role) {
        parsed.role = parsed.role.toUpperCase();
      }
      return parsed;
    } catch (e) {
      console.error("Failed to parse stored user info:", e);
      localStorage.removeItem("userInfo");
      return null;
    }
  });

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (token) {
      apiClient.defaults.headers.common["Authorization"] = `Bearer ${token}`;
    } else {
      delete apiClient.defaults.headers.common["Authorization"];
    }
  }, [isAuthenticated]);

  const hasRole = useCallback(
    (roleOrRoles: string | string[]): boolean => {
      const requiredRoles = Array.isArray(roleOrRoles)
        ? roleOrRoles
        : [roleOrRoles];
      const requiredRolesUpper = requiredRoles.map((r) => r.toUpperCase());

      console.log(
        `hasRole Check: User Role='${userInfo?.role ?? "N/A"}', Required Roles='${JSON.stringify(requiredRolesUpper)}'`,
      );

      if (!userInfo?.role) {
        console.log("hasRole Result: Fail (No user role)");
        return false;
      }

      const userRoleUpper = userInfo.role;

      const result = requiredRolesUpper.includes(userRoleUpper);
      console.log(`hasRole Result: ${result}`);
      return result;
    },
    [userInfo],
  );

  const login = async (
    username: string,
    password: string,
    skipApiCall = false,
  ) => {
    try {
      let apiUserInfo: UserInfo | null = null;
      let token: string | null = null;

      if (!skipApiCall) {
        const response = await apiClient.post("/login", {
          userName: username,
          password,
        });
        token = response.data.accessToken;
        apiUserInfo = response.data.userInfo;

        if (!token || !apiUserInfo || !apiUserInfo.role) {
          throw new Error(
            "Invalid login response from server (missing token or user info/role).",
          );
        }

        localStorage.setItem("token", token);
        apiClient.defaults.headers.common["Authorization"] = `Bearer ${token}`;
      } else {
        token = localStorage.getItem("token");
        const storedUserInfo = localStorage.getItem("userInfo");
        apiUserInfo = storedUserInfo ? JSON.parse(storedUserInfo) : null;
        if (token)
          apiClient.defaults.headers.common["Authorization"] =
            `Bearer ${token}`;
      }

      if (apiUserInfo) {
        apiUserInfo.role = apiUserInfo.role.toUpperCase();
        localStorage.setItem("userInfo", JSON.stringify(apiUserInfo));
        setUserInfo(apiUserInfo);
        setIsAuthenticated(true);
        toast.success(`Logged in successfully as ${apiUserInfo.role}`);
        console.log("User Info after login:", apiUserInfo);
      } else {
        await logout();
        throw new Error("User information not available after login.");
      }
    } catch (error: any) {
      console.error("Login error:", error);
      localStorage.removeItem("token");
      localStorage.removeItem("userInfo");
      delete apiClient.defaults.headers.common["Authorization"];
      setUserInfo(null);
      setIsAuthenticated(false);
      const errorMessage =
        error.response?.data?.detail || error.message || "Login failed.";
      toast.error(errorMessage);
      throw error;
    }
  };

  const refreshToken = async () => {
    try {
      const response = await apiClient.post("/accessToken");
      const { accessToken, userInfo: refreshedUserInfo } = response.data;

      if (!accessToken || !refreshedUserInfo || !refreshedUserInfo.role) {
        throw new Error("Invalid refresh token response from server.");
      }

      localStorage.setItem("token", accessToken);
      apiClient.defaults.headers.common["Authorization"] =
        `Bearer ${accessToken}`;
      refreshedUserInfo.role = refreshedUserInfo.role.toUpperCase();
      localStorage.setItem("userInfo", JSON.stringify(refreshedUserInfo));
      setUserInfo(refreshedUserInfo);
      setIsAuthenticated(true);
    } catch (error) {
      console.error("Token refresh error:", error);
      await logout();
    }
  };

  const logout = async () => {
    try {
      await apiClient.post("/logout");
      console.log("Logout API call successful or handled.");
    } catch (error: any) {
      console.error(
        "Logout API call failed:",
        error.response?.data || error.message,
      );
    } finally {
      localStorage.removeItem("token");
      localStorage.removeItem("userInfo");
      delete apiClient.defaults.headers.common["Authorization"];
      setUserInfo(null);
      setIsAuthenticated(false);
      toast.info("Logged out");
      console.log("Client-side logout completed.");
      window.location.href = "/login";
    }
  };

  return (
    <AuthContext.Provider
      value={{
        isAuthenticated,
        login,
        logout,
        refreshToken,
        userInfo,
        hasRole,
      }}
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
