// src/components/AdminDashboard.tsx
import React, { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import {
  Users,
  Car,
  Map,
  UserCheck,
  UserX,
  Eye,
  Search,
  Shield,
  RefreshCw,
  ChevronRight,
  Settings,
  Droplet,
  Clock,
  AlertTriangle,
  Filter,
  Loader,
  Compass,
  Navigation,
  MapPin,
} from "lucide-react";
import apiClient from "../api/client";
import { Vehicle, Trip } from "../types";
import { useAuth } from "../auth/AuthContext";

interface UserData {
  id: string;
  userName: string;
  email: string;
  roles: string[];
  lastLocation?: {
    latitude: number;
    longitude: number;
    timestamp: string;
    speed?: number | null;
    heading?: number | null;
  };
  vehicles?: Vehicle[];
  activeTrips?: Trip[];
  lastSeen?: string;
}

const AdminDashboard = () => {
  const [users, setUsers] = useState<UserData[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState<string | null>(null); // Error state
  const [searchTerm, setSearchTerm] = useState("");
  const [lastRefreshed, setLastRefreshed] = useState<Date>(new Date());
  const [filterActive, setFilterActive] = useState<boolean>(false);
  const { isAuthenticated, userInfo } = useAuth();
  const navigate = useNavigate();
  const isMounted = useRef(true);

  useEffect(() => {
    isMounted.current = true;
    // Check role immediately if userInfo is available
    if (userInfo && userInfo.role !== "ADMIN") {
      toast.error("Access denied. Admin privileges required.");
      navigate("/");
      return; // Prevent fetching if not admin
    }
    // If userInfo is not yet available, PrivateRoute should handle redirection
    // Fetch data only if authenticated and potentially admin
    if (isAuthenticated && userInfo?.role === "ADMIN") {
      fetchUsersAndData();
      const interval = setInterval(fetchUsersAndData, 30000);
      return () => {
        isMounted.current = false;
        clearInterval(interval);
      };
    } else if (!isAuthenticated) {
      // If not authenticated, stop loading (PrivateRoute will redirect)
      setIsLoading(false);
    }
    // If authenticated but role is not ADMIN (and check passed initially),
    // this effect won't run fetch, isLoading remains true until redirect
    return () => {
      isMounted.current = false;
    }; // Cleanup mount status
  }, [isAuthenticated, userInfo, navigate]); // Add navigate dependency

  const fetchUsersAndData = async () => {
    if (!isMounted.current) return;
    setIsLoading(true);
    setApiError(null); // Clear previous error
    try {
      console.log("Fetching users from /api/users...");
      const usersResponse = await apiClient.get<UserData[]>("/users");
      console.log("Users response data:", usersResponse.data);

      let fetchedUsers: UserData[] = [];
      if (Array.isArray(usersResponse.data)) {
        fetchedUsers = usersResponse.data.map((user) => ({
          ...user,
          roles: Array.isArray(user.roles)
            ? user.roles
            : [user.roles].filter(Boolean),
          lastSeen: user.lastLocation?.timestamp
            ? new Date(user.lastLocation.timestamp).toLocaleString()
            : undefined,
        }));
        console.log("Formatted users:", fetchedUsers);
      } else {
        console.warn(
          "Unexpected data format from /api/users:",
          usersResponse.data,
        );
      }

      if (isMounted.current) {
        setUsers(fetchedUsers);
        setLastRefreshed(new Date());
      }
    } catch (error: any) {
      console.error("Error fetching admin dashboard data:", error);
      const errorMsg = error.response?.data || "Failed to load admin data";
      if (isMounted.current) {
        toast.error(errorMsg);
        setApiError(errorMsg); // Set error state
        setUsers([]);
      }
    } finally {
      if (isMounted.current) setIsLoading(false);
    }
  };

  const viewUserLocation = (userId: string) => {
    navigate("/family-tracking", { state: { selectedUserId: userId } });
  };

  const isActive = (user: UserData): boolean => {
    if (!user.lastLocation?.timestamp) return false;
    const lastSeenDate = new Date(user.lastLocation.timestamp);
    const fiveMinutesAgo = new Date(Date.now() - 5 * 60 * 1000);
    return (
      lastSeenDate > fiveMinutesAgo ||
      (user.lastLocation.speed ?? 0) * 2.237 > 7
    );
  };

  const filteredUsers = users.filter(
    (user) =>
      (user.userName.toLowerCase().includes(searchTerm.toLowerCase()) ||
        user.email.toLowerCase().includes(searchTerm.toLowerCase())) &&
      (!filterActive || isActive(user)),
  );

  const formatTimeSince = (dateString?: string): string => {
    if (!dateString) return "Never";
    const now = new Date();
    const date = new Date(dateString);
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 1) return "Just now";
    if (diffMins < 60) return `${diffMins}m ago`;
    const hours = Math.floor(diffMins / 60);
    if (hours < 24) return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  };

  return (
    <div className="container mx-auto px-4 py-8">
      {/* Header */}
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="text-3xl font-bold">Admin Dashboard</h1>
          <p className="text-gray-600">Monitor and manage your fleet users</p>
        </div>
        <div className="flex items-center space-x-3">
          <div className="text-sm text-gray-500">
            <Clock className="h-4 w-4 inline mr-1" />
            Last updated: {lastRefreshed.toLocaleTimeString()}
          </div>
          <button
            onClick={fetchUsersAndData}
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg flex items-center"
            disabled={isLoading}
          >
            <RefreshCw
              className={`h-5 w-5 mr-2 ${isLoading ? "animate-spin" : ""}`}
            />{" "}
            Refresh Data
          </button>
        </div>
      </div>

      {/* Search and filter */}
      <div className="bg-white rounded-lg shadow-md mb-8 p-4">
        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
          <div className="relative flex-grow max-w-md">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <Search className="h-5 w-5 text-gray-400" />
            </div>
            <input
              type="text"
              placeholder="Search users (name, email)..."
              className="pl-10 pr-4 py-2 border rounded-lg w-full focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>
          <button
            className={`flex items-center ${filterActive ? "bg-blue-100 text-blue-700" : "text-gray-600"} px-3 py-2 border rounded-lg hover:bg-gray-50`}
            onClick={() => setFilterActive(!filterActive)}
          >
            <Filter className="h-5 w-5 mr-2" />{" "}
            {filterActive ? "Show All Users" : "Show Active Only"}
          </button>
        </div>
      </div>

      {/* Stats overview - Simplified */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
        <div className="bg-white rounded-lg shadow-md p-6">
          <div className="flex justify-between items-center mb-4">
            <h3 className="text-gray-500 font-medium">Total Users</h3>
            <Users className="h-8 w-8 text-blue-500" />
          </div>
          <p className="text-2xl font-bold">{users.length}</p>
        </div>
        <div className="bg-white rounded-lg shadow-md p-6">
          <div className="flex justify-between items-center mb-4">
            <h3 className="text-gray-500 font-medium">Active Users</h3>
            <Map className="h-8 w-8 text-red-500" />
          </div>
          <p className="text-2xl font-bold">{users.filter(isActive).length}</p>
        </div>
        <div className="bg-white rounded-lg shadow-md p-6">
          <div className="flex justify-between items-center mb-4">
            <h3 className="text-gray-500 font-medium">Your Role</h3>
            <Shield className="h-8 w-8 text-indigo-500" />
          </div>
          <p className="text-2xl font-bold">{userInfo?.role}</p>
        </div>
      </div>

      {/* Users list */}
      <div className="bg-white rounded-lg shadow-md">
        <div className="px-6 py-4 border-b">
          <h2 className="text-lg font-semibold flex items-center">
            <Users className="h-5 w-5 mr-2 text-blue-600" /> Fleet Users
          </h2>
        </div>

        {isLoading ? (
          <div className="p-6 text-center">
            <Loader className="h-6 w-6 animate-spin mx-auto text-blue-500" />
          </div>
        ) : apiError ? (
          <div className="p-6 text-center text-red-600">Error: {apiError}</div> // Show API error
        ) : filteredUsers.length === 0 ? (
          <div className="p-6 text-center text-gray-500">
            No users found matching criteria.
          </div>
        ) : (
          <div className="divide-y divide-gray-200">
            {filteredUsers.map((user) => (
              <div key={user.id} className="p-6 hover:bg-gray-50">
                <div className="flex flex-col md:flex-row md:items-center justify-between">
                  {/* User Info */}
                  <div className="flex-grow mb-4 md:mb-0">
                    <div className="flex items-center mb-2">
                      <div className="h-10 w-10 rounded-full bg-indigo-100 flex items-center justify-center mr-3 flex-shrink-0">
                        <span className="text-indigo-600 font-medium">
                          {user.userName.charAt(0).toUpperCase()}
                        </span>
                      </div>
                      <div>
                        <h3 className="font-medium text-lg">{user.userName}</h3>
                        <p className="text-sm text-gray-600">{user.email}</p>
                      </div>
                    </div>
                    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-gray-600 mt-1 pl-13">
                      <span className="inline-flex items-center bg-blue-100 text-blue-800 px-2 py-0.5 rounded-full text-xs font-medium">
                        {user.roles?.join(", ") || "No Role"}
                      </span>
                      <div
                        className={`flex items-center ${isActive(user) ? "text-green-600" : "text-gray-500"}`}
                      >
                        {isActive(user) ? (
                          <UserCheck className="h-4 w-4 mr-1" />
                        ) : (
                          <UserX className="h-4 w-4 mr-1" />
                        )}
                        {isActive(user) ? "Active" : "Inactive"}
                      </div>
                      {user.lastLocation && (
                        <div className="flex items-center text-blue-600">
                          <MapPin className="h-4 w-4 mr-1" />
                          Last seen: {formatTimeSince(user.lastSeen)}
                        </div>
                      )}
                    </div>
                  </div>
                  {/* Actions */}
                  <div className="flex space-x-2 flex-shrink-0 self-start md:self-center">
                    <button
                      onClick={() => viewUserLocation(user.id)}
                      className={`${user.lastLocation ? "bg-green-100 text-green-700 hover:bg-green-200" : "bg-gray-100 text-gray-400 cursor-not-allowed"} px-3 py-1 rounded-lg flex items-center text-sm`}
                      disabled={!user.lastLocation}
                    >
                      {" "}
                      <Eye className="h-4 w-4 mr-1" /> Location{" "}
                    </button>
                    {/* Add more admin actions like Edit Role, Disable User etc. */}
                  </div>
                </div>
                {/* Location data snippet */}
                {user.lastLocation && (
                  <div className="mt-3 pl-13 pt-2 border-t border-gray-100">
                    <div className="flex items-center text-xs text-gray-500">
                      <MapPin className="h-3 w-3 mr-1 text-red-500" />
                      {user.lastLocation.latitude.toFixed(4)},{" "}
                      {user.lastLocation.longitude.toFixed(4)}
                      {user.lastLocation.speed !== null &&
                        user.lastLocation.speed > 0 && (
                          <span className="ml-3 inline-flex items-center">
                            <Compass className="h-3 w-3 mr-0.5" />
                            {Math.round(
                              (user.lastLocation.speed ?? 0) * 2.237,
                            )}{" "}
                            mph
                          </span>
                        )}
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Other Admin Sections (Placeholder) */}
      <div className="mt-8 grid grid-cols-1 md:grid-cols-2 gap-6">
        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-lg font-semibold mb-4">System Settings</h2>
          <p className="text-gray-600 text-sm">
            Placeholder for global application settings.
          </p>
        </div>
        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-lg font-semibold mb-4">Audit Log</h2>
          <p className="text-gray-600 text-sm">
            Placeholder for viewing admin actions.
          </p>
        </div>
      </div>
    </div>
  );
};

export default AdminDashboard;
