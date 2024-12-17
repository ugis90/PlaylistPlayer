import { Navigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

export function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" />;
}
