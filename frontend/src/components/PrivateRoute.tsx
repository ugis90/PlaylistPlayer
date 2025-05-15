import React, { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { toast } from "sonner";

interface PrivateRouteProps {
  children: ReactNode;
  roles?: string[];
}

const PrivateRoute: React.FC<PrivateRouteProps> = ({ children, roles }) => {
  const { isAuthenticated, hasRole, userInfo } = useAuth();
  const location = useLocation();

  console.log(`PrivateRoute Check for path: ${location.pathname}`);
  console.log(` - IsAuthenticated: ${isAuthenticated}`);
  console.log(` - UserInfo:`, userInfo);
  console.log(` - Required Roles:`, roles);

  if (!isAuthenticated) {
    console.log(` - Redirecting to /login (Not Authenticated)`);
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (roles && roles.length > 0) {
    const userHasAccess = hasRole(roles);
    if (!userHasAccess) {
      const requiredRolesString = roles.join(" or ");
      const userRoleString = userInfo?.role ?? "Unknown";
      console.log(
        ` - Access Denied: User role '${userRoleString}' does not match required roles [${requiredRolesString}]`,
      );
      toast.error(
        `Access Denied: You need ${requiredRolesString} permissions. Your role: ${userRoleString}`,
        { duration: 5000 },
      );
      return <Navigate to="/" replace />;
    } else {
      console.log(
        ` - Access Granted: User role '${userInfo?.role}' matches required roles.`,
      );
    }
  } else {
    console.log(` - Access Granted: No specific roles required.`);
  }

  return <>{children}</>;
};

export default PrivateRoute;
