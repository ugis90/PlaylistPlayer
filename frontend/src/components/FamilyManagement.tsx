// src/components/FamilyManagement.tsx
import React, { useState, useEffect, useRef } from "react";
import { toast } from "sonner";
import {
  Plus,
  Send,
  User,
  UserPlus,
  RefreshCw,
  Loader,
  MapPin,
  Compass,
} from "lucide-react";
import { apiClient } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { useNavigate } from "react-router-dom"; // Import useNavigate

interface FamilyMember {
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
}

export function FamilyManagement() {
  const [members, setMembers] = useState<FamilyMember[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [apiError, setApiError] = useState<string | null>(null); // State for API errors
  const [email, setEmail] = useState("");
  const [role, setRole] = useState("TEENAGER"); // Use uppercase default
  const [isInviting, setIsInviting] = useState(false);
  const [showInviteForm, setShowInviteForm] = useState(false);
  const { userInfo, hasRole } = useAuth(); // Get hasRole
  const isMounted = useRef(true);
  const navigate = useNavigate(); // Initialize navigate

  // Check permissions on mount and when userInfo changes
  useEffect(() => {
    isMounted.current = true;
    const canAccess = hasRole(["ADMIN", "PARENT"]);
    if (!canAccess && userInfo) {
      // Check only if userInfo is loaded
      toast.error("Access Denied: You need Admin or Parent permissions.");
      navigate("/");
    } else if (canAccess) {
      fetchFamilyMembers(); // Fetch only if user has access
    } else {
      setIsLoading(false); // Stop loading if access check is pending userInfo
    }
    return () => {
      isMounted.current = false;
    };
  }, [userInfo, hasRole, navigate]); // Add dependencies

  const fetchFamilyMembers = async () => {
    if (!isMounted.current) return;
    setIsLoading(true);
    setApiError(null); // Clear previous errors
    try {
      console.log("Fetching family members from /api/users...");
      const response = await apiClient.get<FamilyMember[]>("/users");
      console.log("Family members response data:", response.data);

      let fetchedMembers: FamilyMember[] = [];
      if (Array.isArray(response.data)) {
        fetchedMembers = response.data.map((member) => ({
          ...member,
          roles: Array.isArray(member.roles)
            ? member.roles
            : [member.roles].filter(Boolean),
        }));
        console.log("Formatted members:", fetchedMembers);
        if (isMounted.current) setMembers(fetchedMembers);
      } else {
        console.warn(
          "Unexpected data format for family members:",
          response.data,
        );
        if (isMounted.current) setMembers([]);
      }
    } catch (error: any) {
      console.error("Error fetching family members:", error);
      const errorMsg = error.response?.data || "Failed to load family members";
      if (isMounted.current) {
        toast.error(errorMsg);
        setApiError(errorMsg); // Set API error state
        setMembers([]);
      }
    } finally {
      if (isMounted.current) setIsLoading(false);
    }
  };

  const handleInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email || !role) {
      toast.error("Please provide both email and role.");
      return;
    }
    setIsInviting(true);

    try {
      await apiClient.post("/users/invite", { email, role }); // Role should be uppercase from select value
      toast.success(`Invitation sent to ${email}`);
      setEmail("");
      setRole("TEENAGER"); // Reset role
      setShowInviteForm(false);
      await fetchFamilyMembers(); // Refresh list
    } catch (error: any) {
      console.error("Error inviting user:", error);
      toast.error(
        error.response?.data?.message ||
          error.response?.data?.detail ||
          "Failed to send invitation",
      );
    } finally {
      setIsInviting(false);
    }
  };

  // Render loading or error state first
  if (isLoading) {
    return (
      <div className="p-6 text-center">
        <Loader className="h-6 w-6 animate-spin mx-auto text-blue-500" />{" "}
        Loading...
      </div>
    );
  }

  if (apiError) {
    return (
      <div className="p-6 text-center text-red-600">
        Error loading data: {apiError}
      </div>
    );
  }

  return (
    <div className="bg-white rounded-lg shadow-md p-6">
      {/* Header */}
      <div className="flex justify-between items-center mb-6">
        <h2 className="text-xl font-bold">Family Members</h2>
        <div className="flex space-x-2">
          <button
            onClick={fetchFamilyMembers}
            className="bg-gray-100 hover:bg-gray-200 text-gray-800 p-2 rounded-full"
            title="Refresh"
            disabled={isLoading}
          >
            <RefreshCw
              className={`h-4 w-4 ${isLoading ? "animate-spin" : ""}`}
            />
          </button>
          <button
            onClick={() => setShowInviteForm(!showInviteForm)}
            className="bg-blue-600 hover:bg-blue-700 text-white flex items-center px-3 py-1 rounded-md"
          >
            <UserPlus className="h-4 w-4 mr-1" /> Add Member
          </button>
        </div>
      </div>

      {/* Invite Form */}
      {showInviteForm && (
        <form
          onSubmit={handleInvite}
          className="mb-6 p-4 bg-blue-50 rounded-lg border border-blue-200"
        >
          <h3 className="font-medium mb-3 text-blue-800">
            Invite Family Member
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="md:col-span-2">
              <label className="block text-sm mb-1 font-medium text-gray-700">
                Email Address
              </label>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="Enter email address"
                className="w-full p-2 border border-gray-300 rounded focus:ring-blue-500 focus:border-blue-500"
                required
              />
            </div>
            <div>
              <label className="block text-sm mb-1 font-medium text-gray-700">
                Assign Role
              </label>
              <select
                value={role}
                onChange={(e) => setRole(e.target.value)}
                className="w-full p-2 border border-gray-300 rounded bg-white focus:ring-blue-500 focus:border-blue-500"
              >
                {/* Use Uppercase values */}
                <option value="TEENAGER">Child/Teenager</option>
                <option value="FLEETUSER">Regular User</option>
                <option value="PARENT">Parent</option>
                {/* <option value="ADMIN">Admin</option> */}
              </select>
            </div>
          </div>
          <div className="mt-4 flex justify-end space-x-2">
            <button
              type="button"
              onClick={() => setShowInviteForm(false)}
              className="text-gray-600 px-3 py-1 rounded hover:bg-gray-100"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isInviting}
              className="bg-blue-600 hover:bg-blue-700 text-white px-3 py-1 rounded flex items-center disabled:opacity-50"
            >
              {isInviting ? (
                <Loader className="animate-spin h-4 w-4 mr-1" />
              ) : (
                <Send className="h-4 w-4 mr-1" />
              )}
              {isInviting ? "Sending..." : "Send Invitation"}
            </button>
          </div>
        </form>
      )}

      {/* Member List */}
      {members.length === 0 ? (
        <div className="p-6 text-center text-gray-500">
          <User className="h-16 w-16 mx-auto mb-2 text-gray-300" />
          <p>No family members found.</p>
          <button
            onClick={() => setShowInviteForm(true)}
            className="mt-2 text-blue-600 hover:underline inline-flex items-center"
          >
            <Plus className="h-4 w-4 mr-1" /> Add your first family member
          </button>
        </div>
      ) : (
        <div className="divide-y divide-gray-100">
          {members.map((member) => (
            <div
              key={member.id}
              className="py-4 flex flex-col sm:flex-row justify-between sm:items-center hover:bg-gray-50 px-2 -mx-2 rounded"
            >
              {/* Left Side: User Info */}
              <div className="flex items-center mb-2 sm:mb-0">
                <div className="h-10 w-10 rounded-full bg-indigo-100 flex items-center justify-center mr-3 flex-shrink-0">
                  <User className="h-6 w-6 text-indigo-600" />
                </div>
                <div>
                  <h3 className="font-medium">{member.userName}</h3>
                  <p className="text-sm text-gray-600">{member.email}</p>
                  <div className="flex items-center mt-1 space-x-1">
                    {member.roles?.map((role) => (
                      <span
                        key={role}
                        className="text-xs bg-blue-100 text-blue-800 px-2 py-0.5 rounded-full font-medium"
                      >
                        {role} {/* Display role as fetched */}
                      </span>
                    ))}
                  </div>
                </div>
              </div>
              {/* Right Side: Location & Actions */}
              <div className="flex flex-col sm:items-end space-y-1 sm:space-y-0 sm:space-x-4 sm:flex-row">
                {member.lastLocation && (
                  <div className="text-xs text-gray-500 flex items-center flex-shrink-0">
                    <MapPin className="h-4 w-4 mr-1 text-red-500" />
                    <span>
                      {member.lastLocation.latitude.toFixed(4)},{" "}
                      {member.lastLocation.longitude.toFixed(4)}
                      {member.lastLocation.speed !== null &&
                        member.lastLocation.speed > 0 && (
                          <span className="ml-2 inline-flex items-center">
                            <Compass className="h-3 w-3 mr-0.5" />
                            {Math.round(
                              (member.lastLocation.speed ?? 0) * 2.237,
                            )}{" "}
                            mph
                          </span>
                        )}
                      <span className="ml-2">
                        (
                        {new Date(
                          member.lastLocation.timestamp,
                        ).toLocaleTimeString()}
                        )
                      </span>
                    </span>
                  </div>
                )}
                {/* Add actions if needed, e.g., remove member, change role */}
                {/* <button className="text-blue-600 hover:text-blue-800 flex items-center text-sm"> Manage <ChevronRight className="h-4 w-4" /> </button> */}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
