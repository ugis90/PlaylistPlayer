// src/components/PrivateRoute.tsx
import React, { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { toast } from "sonner";

interface PrivateRouteProps {
  children: ReactNode;
  roles?: string[]; // Expecting Uppercase roles now
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
    // Redirect to login, saving the intended location
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  // Check roles only if isAuthenticated is true
  if (roles && roles.length > 0) {
    const userHasAccess = hasRole(roles); // hasRole already logs internally
    if (!userHasAccess) {
      const requiredRolesString = roles.join(" or ");
      const userRoleString = userInfo?.role ?? "Unknown";
      console.log(
        ` - Access Denied: User role '${userRoleString}' does not match required roles [${requiredRolesString}]`,
      );
      toast.error(
        `Access Denied: You need ${requiredRolesString} permissions. Your role: ${userRoleString}`,
        { duration: 5000 }, // Increase duration
      );
      // Redirect to the main dashboard (or a dedicated unauthorized page)
      return <Navigate to="/" replace />;
    } else {
      console.log(
        ` - Access Granted: User role '${userInfo?.role}' matches required roles.`,
      );
    }
  } else {
    console.log(` - Access Granted: No specific roles required.`);
  }

  // If authenticated and role check passes (or no roles required), render the children
  return <>{children}</>;
};

export default PrivateRoute;
