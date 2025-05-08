// src/components/FamilyTracking.tsx
import React, { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import {
  Car,
  Map as MapIconLucide,
  MapPin,
  Phone,
  Calendar,
  Clock,
  AlertTriangle,
  RefreshCw,
  Compass,
  ArrowRight,
  Navigation,
  User,
  Settings,
  Shield,
  Loader,
} from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import { apiClient } from "../api/client";
import { Vehicle, Trip } from "../types";
import { useGoogleMapsApi } from "../hooks/useGoogleMapsApi";
import MapErrorBoundary from "./maps/MapErrorBoundary";
import GoogleMapsWrapper from "./maps/GoogleMapsWrapper";

interface LocationData {
  userId: string;
  vehicleId: number;
  latitude: number;
  longitude: number;
  speed: number | null;
  heading: number | null;
  timestamp: string;
  tripId?: number;
  accuracy?: number;
}

interface FamilyMember {
  id: string;
  userName: string;
  name: string;
  email: string;
  role: string;
  roles: string[];
  phone?: string;
  vehicle?: Vehicle;
  currentTrip?: Trip;
  location?: LocationData;
  lastSeen?: string;
  lastLocation?: LocationData;
}

const FamilyTracking: React.FC = () => {
  const [familyMembers, setFamilyMembers] = useState<FamilyMember[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isMapOpen, setIsMapOpen] = useState(false);
  const [selectedMember, setSelectedMember] = useState<FamilyMember | null>(
    null,
  );
  const [refreshInterval, setRefreshInterval] = useState<number>(30);
  const [lastRefresh, setLastRefresh] = useState<Date>(new Date());

  const navigate = useNavigate();
  const { userInfo } = useAuth();
  const refreshTimerRef = useRef<number | null>(null);
  const isMounted = useRef(true);
  const {
    isLoaded: isMapsApiLoaded,
    loadScript: loadMapsScript,
    error: mapApiError,
  } = useGoogleMapsApi();

  // Refs for direct map manipulation
  const mapInstanceRef = useRef<google.maps.Map | null>(null);
  const markersRef = useRef<Map<string, google.maps.Marker>>(new Map());

  const hasViewAccess =
    userInfo?.role === "ADMIN" || userInfo?.role === "PARENT";

  // --- Logging ---
  const addToDebugLog = useCallback((message: string) => {
    if (!isMounted.current) return;
    console.log(`[Family Tracking] ${message}`);
  }, []);

  // --- Map Marker Update Logic ---
  const updateMapMarkers = useCallback(() => {
    if (!mapInstanceRef.current || !window.google?.maps || !isMounted.current) {
      addToDebugLog(
        "Update Markers skipped: Map not ready or component unmounted.",
      );
      return;
    }

    addToDebugLog(
      `Updating family map markers for ${familyMembers.length} members...`,
    );

    const currentMarkerIds = new Set(markersRef.current.keys());
    let markersUpdated = 0;
    let markersCreated = 0;

    familyMembers.forEach((member) => {
      if (!member.location) {
        // If member previously had a marker, remove it
        const existingMarker = markersRef.current.get(member.id);
        if (existingMarker) {
          existingMarker.setMap(null);
          markersRef.current.delete(member.id);
          addToDebugLog(`Removed marker for ${member.name} (no location).`);
        }
        return; // Skip members without location
      }

      const position = {
        lat: member.location.latitude,
        lng: member.location.longitude,
      };
      const existingMarker = markersRef.current.get(member.id);

      if (existingMarker) {
        // Only update position if it actually changed (optimization)
        const currentPosition = existingMarker.getPosition?.();
        if (
          !currentPosition ||
          currentPosition.lat() !== position.lat ||
          currentPosition.lng() !== position.lng
        ) {
          existingMarker.setPosition(position);
          markersUpdated++;
        }
        // Update InfoWindow content
        const infoWindow = (existingMarker as any).get?.("infoWindow");
        if (infoWindow) {
          const infoContent = `<div><h3>${member.name} (${member.role})</h3><p>Speed: ${getSpeed(member)}</p><p>Last: ${getTimeSinceUpdate(member.location.timestamp)}</p></div>`;
          infoWindow.setContent(infoContent);
        }
      } else {
        // Create new marker
        try {
          const marker = new window.google.maps.Marker({
            position,
            map: mapInstanceRef.current,
            title: member.name,
            icon: {
              path: window.google.maps.SymbolPath.CIRCLE,
              scale: 8,
              fillColor: getMarkerColor(member.role),
              fillOpacity: 0.8,
              strokeWeight: 2,
              strokeColor: "white",
            },
          });

          const infoContent = `<div><h3>${member.name} (${member.role})</h3><p>Speed: ${getSpeed(member)}</p><p>Last: ${getTimeSinceUpdate(member.location.timestamp)}</p></div>`;
          const infoWindow = new window.google.maps.InfoWindow({
            content: infoContent,
          });

          marker.addListener("click", () => {
            infoWindow.open({
              anchor: marker,
              map: mapInstanceRef.current,
            });
          });

          (marker as any).set("infoWindow", infoWindow);
          markersRef.current.set(member.id, marker);
          markersCreated++;
        } catch (e) {
          console.error(`Error creating marker for ${member.name}:`, e);
          addToDebugLog(`Error creating marker for ${member.name}`);
        }
      }
      currentMarkerIds.delete(member.id); // Mark as processed
    });

    // Remove markers for members no longer in the list
    let markersRemoved = 0;
    currentMarkerIds.forEach((markerId) => {
      const markerToRemove = markersRef.current.get(markerId);
      if (markerToRemove) {
        if (window.google?.maps?.event) {
          window.google.maps.event.clearListeners(markerToRemove, "click");
        }
        markerToRemove.setMap(null);
        markersRef.current.delete(markerId);
        markersRemoved++;
      }
    });

    addToDebugLog(
      `Map Markers Update: ${markersCreated} created, ${markersUpdated} updated, ${markersRemoved} removed.`,
    );

    // Center map logic - but only if the user explicitly selected a member
    if (selectedMember?.location && mapInstanceRef.current) {
      mapInstanceRef.current.panTo({
        lat: selectedMember.location.latitude,
        lng: selectedMember.location.longitude,
      });
      mapInstanceRef.current.setZoom(15);
    } else if (
      markersRef.current.size > 0 &&
      mapInstanceRef.current &&
      !selectedMember
    ) {
      // Fit bounds only if multiple markers exist and no specific member is selected
      if (markersRef.current.size > 1) {
        const bounds = new window.google.maps.LatLngBounds();
        markersRef.current.forEach((marker) => {
          const position = marker.getPosition?.();
          if (position) bounds.extend(position);
        });
        mapInstanceRef.current.fitBounds(bounds);
      } else if (markersRef.current.size === 1) {
        // Center on the single marker
        const firstMarker = markersRef.current.values().next().value;
        if (firstMarker) {
          const position = firstMarker.getPosition?.();
          if (position) mapInstanceRef.current.setCenter(position);
        }
        mapInstanceRef.current.setZoom(14); // Zoom in a bit for single marker
      }
    }
  }, [familyMembers, selectedMember, addToDebugLog]);

  const handleMapLoaded = useCallback(
    (mapInstance: google.maps.Map) => {
      addToDebugLog("Map instance loaded from wrapper.");
      mapInstanceRef.current = mapInstance;

      // Create markers with a slight delay to ensure map is fully ready
      setTimeout(() => {
        if (isMounted.current && mapInstanceRef.current) {
          updateMapMarkers();
        }
      }, 100);
    },
    [addToDebugLog, updateMapMarkers],
  );

  // --- Effects ---
  useEffect(() => {
    isMounted.current = true;
    if (!hasViewAccess) {
      toast.error("You need admin or parent permissions to view this page");
      navigate("/");
      return;
    }
    fetchFamilyMembers();
    startRefreshTimer();
    return () => {
      isMounted.current = false;
      if (refreshTimerRef.current)
        window.clearInterval(refreshTimerRef.current);

      // Clean up all markers on unmount
      markersRef.current.forEach((m) => m.setMap(null));
      markersRef.current.clear();
      mapInstanceRef.current = null;
    };
  }, [refreshInterval, hasViewAccess, navigate]);

  // Load map script if needed when map is opened
  useEffect(() => {
    if (isMapOpen && !isMapsApiLoaded && !mapApiError) {
      loadMapsScript().catch((err) =>
        console.error("Map script load promise rejected:", err),
      );
    }
  }, [isMapOpen, isMapsApiLoaded, loadMapsScript, mapApiError]);

  // Update markers when family members data changes
  useEffect(() => {
    if (isMapOpen && mapInstanceRef.current && isMapsApiLoaded) {
      // Only update markers if the map is visible
      updateMapMarkers();
    }
  }, [familyMembers, isMapOpen, isMapsApiLoaded, updateMapMarkers]);

  // Update markers when selected member changes
  useEffect(() => {
    if (
      isMapOpen &&
      mapInstanceRef.current &&
      isMapsApiLoaded &&
      selectedMember
    ) {
      // Focus on the selected member
      if (selectedMember.location) {
        mapInstanceRef.current.panTo({
          lat: selectedMember.location.latitude,
          lng: selectedMember.location.longitude,
        });
        mapInstanceRef.current.setZoom(15);
      }
    }
  }, [selectedMember, isMapOpen, isMapsApiLoaded]);

  const fetchFamilyMembers = async () => {
    if (!isMounted.current) return;
    // Only set loading on initial fetch
    if (familyMembers.length === 0) setIsLoading(true);
    try {
      const usersResponse =
        await apiClient.get<
          Omit<
            FamilyMember,
            "vehicle" | "currentTrip" | "lastSeen" | "name" | "role"
          >[]
        >("/users");
      let fetchedUsers: FamilyMember[] = [];

      if (Array.isArray(usersResponse.data)) {
        fetchedUsers = usersResponse.data.map((user) => ({
          ...user,
          name: user.userName,
          roles: Array.isArray(user.roles)
            ? user.roles
            : [user.roles].filter(Boolean),
          role:
            (Array.isArray(user.roles)
              ? user.roles[0]
              : user.roles
            )?.toUpperCase() || "UNKNOWN",
          location: user.lastLocation,
          lastSeen: user.lastLocation?.timestamp
            ? new Date(user.lastLocation.timestamp).toLocaleString()
            : undefined,
        }));
        console.log(
          "FamilyTracking: Formatted members from API:",
          fetchedUsers,
        );
      } else {
        console.warn(
          "Unexpected data format from /api/users:",
          usersResponse.data,
        );
      }

      // TODO: Fetch vehicles/trips if needed

      if (isMounted.current) {
        setFamilyMembers(fetchedUsers);
        setLastRefresh(new Date());

        // Update markers if map is already open
        if (isMapOpen && mapInstanceRef.current) {
          updateMapMarkers();
        }
      }
    } catch (error: unknown) {
      console.error("Error fetching family members:", error);
      const errorMsg =
        (error as any)?.response?.data || "Failed to load family data";
      if (isMounted.current) {
        toast.error(errorMsg);
        setFamilyMembers([]);
      }
    } finally {
      if (isMounted.current) setIsLoading(false);
    }
  };

  const startRefreshTimer = () => {
    if (refreshTimerRef.current) window.clearInterval(refreshTimerRef.current);
    const intervalMs = refreshInterval * 1000;
    refreshTimerRef.current = window.setInterval(() => {
      fetchFamilyMembers();
    }, intervalMs);
  };

  const getMarkerColor = (role: string): string => {
    switch (role?.toUpperCase()) {
      case "PARENT":
        return "#4285F4";
      case "TEENAGER":
        return "#EA4335";
      case "SPOUSE":
        return "#34A853";
      case "FLEETUSER":
        return "#FBBC05";
      case "ADMIN":
        return "#7E57C2";
      default:
        return "#9E9E9E";
    }
  };

  const showOnMap = (member: FamilyMember) => {
    addToDebugLog(`showOnMap called for ${member.name}`);
    setSelectedMember(member);
    setIsMapOpen(true);
  };

  const formatTripDuration = (trip: Trip): string => {
    if (!trip.startTime || !trip.endTime) return "N/A";
    try {
      const start = new Date(trip.startTime);
      const end = new Date(trip.endTime);
      const diffMs = end.getTime() - start.getTime();
      if (diffMs < 0) return "Invalid";
      const diffMins = Math.floor(diffMs / 60000);
      const hours = Math.floor(diffMins / 60);
      const mins = diffMins % 60;
      return hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
    } catch (error) {
      return "Error";
    }
  };

  function getSpeed(member: FamilyMember): string {
    if (member.location?.speed === null || member.location?.speed === undefined)
      return "N/A";
    const speedMph = Math.round(member.location.speed * 2.237);
    return `${speedMph} mph`;
  }

  const isMoving = (member: FamilyMember): boolean =>
    (member.location?.speed ?? 0) > 2.23;

  const getFamilyStatusSummary = (): string => {
    const moving = familyMembers.filter(isMoving).length;
    const total = familyMembers.length;
    return `${moving} of ${total} family members currently on the move`;
  };

  function getTimeSinceUpdate(timestamp?: string): string {
    if (!timestamp) return "Never";
    const now = new Date();
    const date = new Date(timestamp);
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 1) return "Just now";
    if (diffMins < 60) return `${diffMins}m ago`;
    const hours = Math.floor(diffMins / 60);
    if (hours < 24) return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  }

  // Render loading or error state first
  if (isLoading && familyMembers.length === 0) {
    // Show loading only on initial load
    return (
      <div className="p-6 text-center">
        <Loader className="h-8 w-8 animate-spin mx-auto text-blue-500" />{" "}
        Loading Family Data...
      </div>
    );
  }

  // If not loading but still no access (e.g., userInfo loaded late)
  if (!hasViewAccess) {
    return (
      <div className="p-6 text-center text-red-600">
        Access Denied. Requires Admin or Parent role.
      </div>
    );
  }

  // Get map center coordinates based on selected member or first family member
  const getMapCenter = () => {
    if (selectedMember?.location) {
      return {
        lat: selectedMember.location.latitude,
        lng: selectedMember.location.longitude,
      };
    } else if (familyMembers.length > 0) {
      const memberWithLocation = familyMembers.find((m) => m.location);
      if (memberWithLocation?.location) {
        return {
          lat: memberWithLocation.location.latitude,
          lng: memberWithLocation.location.longitude,
        };
      }
    }
    // Default to New York
    return { lat: 40.7128, lng: -74.006 };
  };

  return (
    <div className="container mx-auto px-4 py-8">
      {/* Header */}
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="text-3xl font-bold">Family Tracking</h1>
          <p className="text-gray-600">{getFamilyStatusSummary()}</p>
        </div>
        <div className="flex gap-4">
          <div className="text-sm text-gray-500 flex items-center">
            <Clock className="h-4 w-4 mr-1" />
            Last updated: {lastRefresh.toLocaleTimeString()}
          </div>
          <button
            onClick={fetchFamilyMembers}
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg flex items-center"
            disabled={isLoading}
          >
            <RefreshCw
              className={`h-5 w-5 mr-2 ${isLoading ? "animate-spin" : ""}`}
            />{" "}
            Refresh
          </button>
        </div>
      </div>

      {/* Admin mode indicator */}
      {userInfo?.role === "ADMIN" && (
        <div className="mb-6 p-4 bg-indigo-50 border border-indigo-200 rounded-lg">
          <div className="flex items-center text-indigo-800">
            <Shield className="h-5 w-5 mr-2" />
            <span className="font-medium">Admin Mode</span>
          </div>
          <p className="text-sm text-indigo-600 mt-1">
            Viewing all family members' locations.
          </p>
        </div>
      )}

      {/* Map Modal */}
      {isMapOpen && (
        <div className="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-lg w-full max-w-6xl h-[90vh] flex flex-col overflow-hidden">
            <div className="p-4 border-b flex justify-between items-center">
              <h2 className="text-xl font-bold flex items-center">
                <MapIconLucide className="h-5 w-5 mr-2 text-blue-600" />
                {selectedMember
                  ? `${selectedMember.name}'s Location`
                  : "Family Map"}
              </h2>
              <button
                onClick={() => setIsMapOpen(false)}
                className="px-3 py-1 bg-gray-200 hover:bg-gray-300 rounded"
              >
                Close Map
              </button>
            </div>
            <div className="flex flex-1 overflow-hidden">
              {/* Map Container */}
              <div className="w-3/4 h-full">
                <MapErrorBoundary>
                  {isMapsApiLoaded ? (
                    <GoogleMapsWrapper
                      height="100%"
                      apiKey={import.meta.env.VITE_GOOGLE_MAPS_API_KEY}
                      mapOptions={{
                        center: getMapCenter(),
                        zoom: selectedMember ? 15 : 11,
                        mapTypeControl: true,
                        streetViewControl: false,
                        fullscreenControl: true,
                        zoomControl: true,
                      }}
                      onMapLoaded={handleMapLoaded}
                    />
                  ) : mapApiError ? (
                    <div className="p-4 m-4 bg-red-100 text-red-700 border border-red-300 rounded flex items-center justify-center h-full">
                      {" "}
                      Map Error: {mapApiError}{" "}
                    </div>
                  ) : (
                    <div className="flex items-center justify-center h-full">
                      <Loader className="h-8 w-8 animate-spin text-blue-500" />{" "}
                      Loading Map...
                    </div>
                  )}
                </MapErrorBoundary>
              </div>
              {/* Sidebar */}
              <div className="w-1/4 p-4 overflow-y-auto border-l">
                <h3 className="font-bold mb-4">Family Members</h3>
                <div className="space-y-4">
                  {familyMembers.map((member) => (
                    <div
                      key={member.id}
                      className={`p-3 rounded-lg cursor-pointer ${selectedMember?.id === member.id ? "bg-blue-100 border border-blue-300" : "hover:bg-gray-100"}`}
                      onClick={() => setSelectedMember(member)}
                    >
                      <div className="flex items-center mb-1">
                        <div
                          className="w-3 h-3 rounded-full mr-2"
                          style={{
                            backgroundColor: getMarkerColor(member.role),
                          }}
                        ></div>
                        <span className="font-medium">{member.name}</span>
                      </div>
                      {member.location ? (
                        <>
                          <p className="text-sm text-gray-600">
                            Speed: {getSpeed(member)}
                          </p>
                          <p className="text-xs text-gray-500">
                            Updated{" "}
                            {getTimeSinceUpdate(member.location.timestamp)}
                          </p>
                        </>
                      ) : (
                        <p className="text-xs text-gray-500">
                          Location unavailable
                        </p>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Family Members List */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-8">
        {isLoading && familyMembers.length === 0 ? (
          Array(3)
            .fill(0)
            .map((_, index) => (
              <div
                key={index}
                className="bg-white rounded-lg shadow-md p-6 animate-pulse"
              >
                <div className="h-8 w-8 rounded-full bg-gray-300 mb-4"></div>
                <div className="h-5 w-3/4 bg-gray-300 mb-2"></div>
                <div className="h-4 w-1/2 bg-gray-300"></div>
              </div>
            ))
        ) : familyMembers.length === 0 ? (
          <div className="col-span-full text-center p-8 text-gray-500">
            No family members found.
          </div>
        ) : (
          familyMembers.map((member) => (
            <div key={member.id} className="bg-white rounded-lg shadow-md p-6">
              <div className="flex justify-between items-center mb-4">
                <div className="flex items-center">
                  <div className="h-10 w-10 rounded-full bg-blue-100 flex items-center justify-center mr-3">
                    <User className="h-6 w-6 text-blue-600" />
                  </div>
                  <div>
                    <h3 className="font-medium text-lg">{member.name}</h3>
                    <p className="text-sm text-gray-600">{member.role}</p>
                  </div>
                </div>
                {member.phone && (
                  <div className="flex items-center text-gray-600 text-sm">
                    <Phone className="h-4 w-4 mr-1" />
                    {member.phone}
                  </div>
                )}
              </div>
              <div
                className={`mb-4 p-3 rounded-lg ${member.location ? (isMoving(member) ? "bg-green-100 text-green-800" : "bg-blue-100 text-blue-800") : "bg-gray-100 text-gray-800"}`}
              >
                <div className="flex justify-between items-center">
                  <div className="flex items-center">
                    {member.location ? (
                      <>
                        <MapPin className="h-5 w-5 mr-2" />
                        <span>
                          {isMoving(member)
                            ? "Currently driving"
                            : "Stationary"}
                        </span>
                      </>
                    ) : (
                      <>
                        <AlertTriangle className="h-5 w-5 mr-2" />
                        <span>Location unavailable</span>
                      </>
                    )}
                  </div>
                  {member.location && (
                    <span className="text-sm">
                      {getTimeSinceUpdate(member.location.timestamp)}
                    </span>
                  )}
                </div>
                {member.location && (
                  <div className="mt-2 text-sm">
                    <div className="flex justify-between">
                      <span>Speed: {getSpeed(member)}</span>
                      {member.location.heading != null && (
                        <span className="flex items-center">
                          <Compass className="h-4 w-4 mr-1" />
                          {member.location.heading.toFixed(0)}°
                        </span>
                      )}
                    </div>
                  </div>
                )}
              </div>
              {member.vehicle && (
                <div className="mb-4">
                  <div className="flex items-center text-gray-700 mb-2">
                    <Car className="h-4 w-4 mr-1" />
                    <h4 className="font-medium">Vehicle</h4>
                  </div>
                  <p className="text-sm text-gray-600">
                    {member.vehicle.make} {member.vehicle.model} (
                    {member.vehicle.year})
                  </p>
                  <p className="text-xs text-gray-500 mt-1">
                    {member.vehicle.licensePlate} •{" "}
                    {member.vehicle.currentMileage?.toLocaleString()} miles
                  </p>
                </div>
              )}
              {member.currentTrip && (
                <div className="mb-4">
                  <div className="flex items-center text-gray-700 mb-2">
                    <Navigation className="h-4 w-4 mr-1" />
                    <h4 className="font-medium">Current/Recent Trip</h4>
                  </div>
                  <div className="text-sm">
                    <div className="flex items-center text-gray-800">
                      {member.currentTrip.startLocation}
                      <ArrowRight className="h-3 w-3 mx-1" />
                      {member.currentTrip.endLocation}
                    </div>
                    <div className="flex justify-between text-gray-600 mt-1">
                      <span>
                        {member.currentTrip.distance?.toFixed(1) || "0"} miles
                      </span>
                      <span>{formatTripDuration(member.currentTrip)}</span>
                    </div>
                    <div className="text-xs text-gray-500 mt-1">
                      <span>
                        Started:{" "}
                        {new Date(
                          member.currentTrip.startTime,
                        ).toLocaleTimeString()}
                      </span>
                    </div>
                  </div>
                </div>
              )}
              <div className="flex space-x-2 mt-6">
                <button
                  onClick={() => showOnMap(member)}
                  disabled={!member.location}
                  className={`flex-1 py-2 px-3 rounded-lg text-sm flex items-center justify-center ${member.location ? "bg-blue-600 hover:bg-blue-700 text-white" : "bg-gray-200 text-gray-500 cursor-not-allowed"}`}
                >
                  <MapIconLucide className="h-4 w-4 mr-1" /> View on Map
                </button>
                <button
                  onClick={() => navigate(`/vehicles/${member.vehicle?.id}`)}
                  disabled={!member.vehicle}
                  className={`flex-1 py-2 px-3 rounded-lg text-sm flex items-center justify-center ${member.vehicle ? "bg-green-600 hover:bg-green-700 text-white" : "bg-gray-200 text-gray-500 cursor-not-allowed"}`}
                >
                  <Calendar className="h-4 w-4 mr-1" /> View History
                </button>
              </div>
            </div>
          ))
        )}
      </div>

      {/* Settings */}
      <div className="bg-white rounded-lg shadow-md p-6 mb-8">
        <h2 className="text-xl font-bold mb-4 flex items-center">
          <Settings className="h-5 w-5 mr-2" /> Location Tracking Settings
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Refresh interval (seconds)
            </label>
            <div className="flex items-center">
              <input
                type="range"
                min="5"
                max="120"
                value={refreshInterval}
                onChange={(e) => setRefreshInterval(parseInt(e.target.value))}
                className="w-full mr-4"
              />
              <span className="text-gray-700 font-medium">
                {refreshInterval}s
              </span>
            </div>
            <p className="text-xs text-gray-500 mt-2">
              How often to refresh family members' locations
            </p>
          </div>
          <div>
            <h3 className="text-sm font-medium text-gray-700 mb-2">
              About GPS Tracking
            </h3>
            <p className="text-sm text-gray-600">
              This feature allows you to monitor the real-time location of
              family members based on data from the API.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default FamilyTracking;
