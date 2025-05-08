// src/components/FamilyDashboard.tsx
import React, { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import {
  Users,
  Car,
  Map,
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
} from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import LocationService, { LocationData } from "../services/LocationService";
import apiClient from "../api/client";
import { Vehicle, Trip } from "../types";

interface FamilyMember {
  id: string;
  name: string;
  role: string;
  phone?: string;
  vehicle?: Vehicle;
  currentTrip?: Trip;
  location?: LocationData;
  lastSeen?: string;
}

const FamilyDashboard: React.FC = () => {
  const [familyMembers, setFamilyMembers] = useState<FamilyMember[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isMapOpen, setIsMapOpen] = useState(false);
  const [selectedMember, setSelectedMember] = useState<FamilyMember | null>(
    null,
  );
  const [refreshInterval, setRefreshInterval] = useState<number>(30); // seconds
  const [lastRefresh, setLastRefresh] = useState<Date>(new Date());
  const [useMockLocation, setUseMockLocation] = useState<boolean>(false);

  const locationService = LocationService.getInstance();
  const navigate = useNavigate();
  const { userInfo } = useAuth();
  const refreshTimerRef = useRef<number | null>(null);
  const mapRef = useRef<HTMLDivElement>(null);
  const mapInstanceRef = useRef<google.maps.Map | null>(null);
  const markersRef = useRef<Map<string, google.maps.Marker>>(new Map());

  // Check if user is admin, parent, or has appropriate role
  const hasViewAccess =
    userInfo?.role === "Admin" ||
    userInfo?.role === "parent" ||
    userInfo?.role === "FleetUser";

  useEffect(() => {
    if (!hasViewAccess) {
      toast.error("You need appropriate permissions to view this page");
      navigate("/");
      return;
    }

    // Get mock mode status from location service
    setUseMockLocation(locationService.isMockModeEnabled());

    // Fetch family members and their locations
    fetchFamilyMembers();

    // Setup periodic refresh
    startRefreshTimer();

    // Setup location listener
    locationService.addListener(handleLocationUpdates);

    // Initialize Google Maps API
    loadGoogleMapsScript();

    return () => {
      if (refreshTimerRef.current) {
        window.clearInterval(refreshTimerRef.current);
      }
      locationService.removeListener(handleLocationUpdates);
    };
  }, [refreshInterval]);

  useEffect(() => {
    if (
      isMapOpen &&
      mapRef.current &&
      window.google?.maps &&
      !mapInstanceRef.current
    ) {
      initializeMap();
    }
  }, [isMapOpen, selectedMember, window.google?.maps]);

  const loadGoogleMapsScript = () => {
    if (!window.google || !window.google.maps) {
      // In a real app, use your Google Maps API key
      const apiKey = import.meta.env.VITE_GOOGLE_MAPS_API_KEY;
      const script = document.createElement("script");
      script.src = `https://maps.googleapis.com/maps/api/js?key=${apiKey}&libraries=places`;
      script.async = true;
      script.defer = true;
      script.onload = () => {
        if (isMapOpen && mapRef.current) {
          initializeMap();
        }
      };
      document.head.appendChild(script);
    }
  };

  const fetchFamilyMembers = async () => {
    setIsLoading(true);
    try {
      // Fetch all vehicles first to assign to family members
      const vehiclesResponse = await apiClient.get("/vehicles");
      let vehicles: Vehicle[] = [];

      if (Array.isArray(vehiclesResponse.data)) {
        vehicles = vehiclesResponse.data;
      } else if (vehiclesResponse.data?.resources) {
        vehicles = vehiclesResponse.data.resources.map(
          (item: any) => item.resource || item,
        );
      } else if (typeof vehiclesResponse.data === "object") {
        Object.keys(vehiclesResponse.data).forEach((key) => {
          if (Array.isArray(vehiclesResponse.data[key])) {
            vehicles = vehiclesResponse.data[key];
          }
        });
      }

      console.log("Fetched vehicles:", vehicles);

      // In a real application, you would fetch real family member data from the API
      // For now, we'll create family members based on available vehicles
      const members: FamilyMember[] = [
        {
          id: userInfo?.username || "parent",
          name: "You (Parent)",
          role: "Parent",
          phone: "555-123-4567",
          vehicle: vehicles.find((v) => v.id === 1),
        },
        {
          id: "teenager",
          name: "Alex (Son)",
          role: "Teenager",
          phone: "555-987-6543",
          vehicle: vehicles.find((v) => v.id === 2),
        },
        {
          id: "spouse",
          name: "Jordan (Spouse)",
          role: "Spouse",
          phone: "555-456-7890",
          vehicle: vehicles.find((v) => v.id === 3),
        },
      ];

      // Add current trips for each member if available
      await addTripsToFamilyMembers(members);

      // Get the latest locations from the location service
      const locations = await locationService.getFamilyLocations();

      // Update members with location data
      members.forEach((member) => {
        const location = locations.get(member.id);
        if (location) {
          member.location = location;
          member.lastSeen = new Date(location.timestamp).toLocaleString();
        }
      });

      setFamilyMembers(members);
      setLastRefresh(new Date());
    } catch (error) {
      console.error("Error fetching family members:", error);
      toast.error("Failed to load family data");
    } finally {
      setIsLoading(false);
    }
  };

  const addTripsToFamilyMembers = async (members: FamilyMember[]) => {
    try {
      // For each member with a vehicle, try to get their current trip
      for (const member of members) {
        if (member.vehicle) {
          try {
            const tripsResponse = await apiClient.get(
              `/vehicles/${member.vehicle.id}/trips`,
            );

            let trips: Trip[] = [];
            if (Array.isArray(tripsResponse.data)) {
              trips = tripsResponse.data;
            } else if (tripsResponse.data?.resources) {
              trips = tripsResponse.data.resources.map(
                (item: any) => item.resource || item,
              );
            } else if (typeof tripsResponse.data === "object") {
              Object.keys(tripsResponse.data).forEach((key) => {
                if (Array.isArray(tripsResponse.data[key])) {
                  trips = tripsResponse.data[key];
                }
              });
            }

            // If we have trips, use the most recent one as current
            if (trips.length > 0) {
              // Sort by start time, most recent first
              const sortedTrips = trips.sort(
                (a, b) =>
                  new Date(b.startTime).getTime() -
                  new Date(a.startTime).getTime(),
              );

              // Check if the most recent trip is still ongoing (within the last hour)
              const mostRecent = sortedTrips[0];
              const tripStartTime = new Date(mostRecent.startTime);
              const oneHourAgo = new Date();
              oneHourAgo.setHours(oneHourAgo.getHours() - 1);

              if (tripStartTime > oneHourAgo) {
                member.currentTrip = mostRecent;
              }
            }
          } catch (error) {
            console.warn(
              `Error fetching trips for family member ${member.name}:`,
              error,
            );
          }
        }
      }
    } catch (error) {
      console.error("Error adding trips to family members:", error);
    }
  };

  const startRefreshTimer = () => {
    if (refreshTimerRef.current) {
      window.clearInterval(refreshTimerRef.current);
    }

    // Convert seconds to milliseconds
    const intervalMs = refreshInterval * 1000;

    refreshTimerRef.current = window.setInterval(() => {
      fetchFamilyMembers();
    }, intervalMs);
  };

  const handleLocationUpdates = (locations: Map<string, LocationData>) => {
    setFamilyMembers((prevMembers) => {
      return prevMembers.map((member) => {
        const location = locations.get(member.id);
        if (location) {
          return {
            ...member,
            location,
            lastSeen: new Date(location.timestamp).toLocaleString(),
          };
        }
        return member;
      });
    });

    // Update map markers if map is open
    if (isMapOpen && mapInstanceRef.current) {
      updateMapMarkers(locations);
    }
  };

  const initializeMap = () => {
    if (!mapRef.current || !window.google?.maps) return;

    // Initialize the map centered on the first family member with location
    const memberWithLocation =
      selectedMember?.location ||
      familyMembers.find((m) => m.location)?.location;

    const center = memberWithLocation
      ? { lat: memberWithLocation.latitude, lng: memberWithLocation.longitude }
      : { lat: 40.7128, lng: -74.006 }; // Default to NYC

    const mapOptions: google.maps.MapOptions = {
      center,
      zoom: 12,
      mapTypeId: google.maps.MapTypeId.ROADMAP,
      mapTypeControl: true,
      streetViewControl: true,
      fullscreenControl: true,
    };

    // Create map instance
    mapInstanceRef.current = new google.maps.Map(mapRef.current, mapOptions);

    // Add markers for all family members
    const locations = new Map<string, LocationData>();
    familyMembers.forEach((member) => {
      if (member.location) {
        locations.set(member.id, member.location);
      }
    });

    updateMapMarkers(locations);
  };

  const updateMapMarkers = (locations: Map<string, LocationData>) => {
    if (!mapInstanceRef.current || !window.google?.maps) return;

    // Update or create markers for each location
    locations.forEach((location, userId) => {
      const member = familyMembers.find((m) => m.id === userId);
      if (!member) return;

      const position = {
        lat: location.latitude,
        lng: location.longitude,
      };

      // If marker exists, update position
      if (markersRef.current.has(userId)) {
        const marker = markersRef.current.get(userId);
        marker?.setPosition(position);
      } else {
        // Create new marker
        const marker = new google.maps.Marker({
          position,
          map: mapInstanceRef.current,
          title: member.name,
          icon: {
            path: google.maps.SymbolPath.CIRCLE,
            scale: 8,
            fillColor: getMarkerColor(member.role),
            fillOpacity: 0.8,
            strokeWeight: 2,
            strokeColor: "white",
          },
        });

        // Add info window with member details
        const infoContent = `
          <div style="padding: 10px;">
            <h3 style="margin: 0 0 5px;">${member.name}</h3>
            <p style="margin: 0 0 5px;">${member.vehicle?.make} ${member.vehicle?.model}</p>
            <p style="margin: 0 0 5px;">Speed: ${Math.round((location.speed || 0) * 2.237)} mph</p>
            <p style="margin: 0;">Last updated: ${new Date(location.timestamp).toLocaleTimeString()}</p>
          </div>
        `;

        const infoWindow = new google.maps.InfoWindow({
          content: infoContent,
        });

        marker.addListener("click", () => {
          infoWindow.open({
            anchor: marker,
            map: mapInstanceRef.current,
          });
        });

        markersRef.current.set(userId, marker);
      }
    });

    // Remove markers for family members no longer tracked
    markersRef.current.forEach((marker, userId) => {
      if (!locations.has(userId)) {
        marker.setMap(null);
        markersRef.current.delete(userId);
      }
    });

    // If we're showing a specific family member, center on them
    if (selectedMember && selectedMember.location) {
      mapInstanceRef.current.panTo({
        lat: selectedMember.location.latitude,
        lng: selectedMember.location.longitude,
      });
      mapInstanceRef.current.setZoom(15);
    }
  };

  const getMarkerColor = (role: string): string => {
    switch (role.toLowerCase()) {
      case "parent":
        return "#4285F4"; // Blue
      case "teenager":
        return "#EA4335"; // Red
      case "spouse":
        return "#34A853"; // Green
      default:
        return "#FBBC05"; // Yellow
    }
  };

  const showOnMap = (member: FamilyMember) => {
    setSelectedMember(member);
    setIsMapOpen(true);
  };

  const formatTripDuration = (trip: Trip): string => {
    if (!trip.startTime || !trip.endTime) return "";

    const start = new Date(trip.startTime);
    const end = new Date(trip.endTime);
    const diffMs = end.getTime() - start.getTime();
    const diffMins = Math.floor(diffMs / 1000 / 60);

    const hours = Math.floor(diffMins / 60);
    const mins = diffMins % 60;

    return hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
  };

  const getSpeed = (member: FamilyMember): string => {
    if (!member.location?.speed) return "Not available";

    // Convert m/s to mph
    const speedMph = Math.round(member.location.speed * 2.237 || 0);
    return `${speedMph} mph`;
  };

  const isMoving = (member: FamilyMember): boolean => {
    return (member.location?.speed || 0) > 5; // Consider moving if > 5 m/s (~11 mph)
  };

  const getFamilyStatusSummary = (): string => {
    const moving = familyMembers.filter(isMoving).length;
    const total = familyMembers.length;

    return `${moving} of ${total} family members currently on the move`;
  };

  // Calculate time since last location update
  const getTimeSinceUpdate = (timestamp?: string): string => {
    if (!timestamp) return "Never";

    const now = new Date();
    const updateTime = new Date(timestamp);
    const diffMs = now.getTime() - updateTime.getTime();
    const diffMins = Math.floor(diffMs / 1000 / 60);

    if (diffMins < 1) return "Just now";
    if (diffMins < 60) return `${diffMins}m ago`;

    const hours = Math.floor(diffMins / 60);
    if (hours < 24) return `${hours}h ago`;

    return `${Math.floor(hours / 24)}d ago`;
  };

  // Handle toggle of mock location mode
  const handleToggleMockMode = () => {
    const newMode = !useMockLocation;
    setUseMockLocation(newMode);
    locationService.setMockMode(newMode);
    toast.info(
      newMode ? "Mock location mode enabled" : "Using real GPS locations",
    );
    fetchFamilyMembers();
  };

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="text-3xl font-bold">Family Dashboard</h1>
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
          >
            <RefreshCw className="h-5 w-5 mr-2" />
            Refresh
          </button>
        </div>
      </div>

      {/* Admin mode indicator */}
      {userInfo?.role === "Admin" && (
        <div className="mb-6 p-4 bg-indigo-50 border border-indigo-200 rounded-lg">
          <div className="flex items-center text-indigo-800">
            <Shield className="h-5 w-5 mr-2" />
            <span className="font-medium">Admin Mode</span>
          </div>
          <p className="text-sm text-indigo-600 mt-1">
            You have access to view and manage all family members' locations.
          </p>
        </div>
      )}

      {/* Map Modal */}
      {isMapOpen && (
        <div className="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-lg w-full max-w-6xl h-[90vh] flex flex-col overflow-hidden">
            <div className="p-4 border-b flex justify-between items-center">
              <h2 className="text-xl font-bold flex items-center">
                <Map className="h-5 w-5 mr-2 text-blue-600" />
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
              <div className="w-3/4 h-full" ref={mapRef}></div>
              <div className="w-1/4 p-4 overflow-y-auto border-l">
                <h3 className="font-bold mb-4">Family Members</h3>
                <div className="space-y-4">
                  {familyMembers.map((member) => (
                    <div
                      key={member.id}
                      className={`p-3 rounded-lg cursor-pointer ${
                        selectedMember?.id === member.id
                          ? "bg-blue-100 border border-blue-300"
                          : "hover:bg-gray-100"
                      }`}
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
        {isLoading
          ? // Loading skeleton UI
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
          : // Actual member cards
            familyMembers.map((member) => (
              <div
                key={member.id}
                className="bg-white rounded-lg shadow-md p-6"
              >
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

                {/* Location Status */}
                <div
                  className={`mb-4 p-3 rounded-lg ${
                    member.location
                      ? isMoving(member)
                        ? "bg-green-100 text-green-800"
                        : "bg-blue-100 text-blue-800"
                      : "bg-gray-100 text-gray-800"
                  }`}
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
                          <span>Location not available</span>
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
                        {member.location.heading && (
                          <span className="flex items-center">
                            <Compass className="h-4 w-4 mr-1" />
                            {member.location.heading}°
                          </span>
                        )}
                      </div>
                    </div>
                  )}
                </div>

                {/* Vehicle Info */}
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
                      {member.vehicle.currentMileage.toLocaleString()} miles
                    </p>
                  </div>
                )}

                {/* Current/Recent Trip */}
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

                {/* Action Buttons */}
                <div className="flex space-x-2 mt-6">
                  <button
                    onClick={() => showOnMap(member)}
                    disabled={!member.location}
                    className={`flex-1 py-2 px-3 rounded-lg text-sm flex items-center justify-center ${
                      member.location
                        ? "bg-blue-600 hover:bg-blue-700 text-white"
                        : "bg-gray-200 text-gray-500 cursor-not-allowed"
                    }`}
                  >
                    <Map className="h-4 w-4 mr-1" />
                    View on Map
                  </button>

                  <button
                    onClick={() => navigate(`/vehicles/${member.vehicle?.id}`)}
                    disabled={!member.vehicle}
                    className={`flex-1 py-2 px-3 rounded-lg text-sm flex items-center justify-center ${
                      member.vehicle
                        ? "bg-green-600 hover:bg-green-700 text-white"
                        : "bg-gray-200 text-gray-500 cursor-not-allowed"
                    }`}
                  >
                    <Calendar className="h-4 w-4 mr-1" />
                    View History
                  </button>
                </div>
              </div>
            ))}
      </div>

      {/* Settings */}
      <div className="bg-white rounded-lg shadow-md p-6 mb-8">
        <h2 className="text-xl font-bold mb-4 flex items-center">
          <Settings className="h-5 w-5 mr-2" />
          Location Tracking Settings
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
              Location Sharing
            </h3>
            <div className="flex items-center mb-4">
              <label className="inline-flex items-center cursor-pointer">
                <input
                  type="checkbox"
                  className="sr-only peer"
                  checked={locationService.isTrackingEnabled()}
                  onChange={() => {
                    if (locationService.isTrackingEnabled()) {
                      locationService.stopTracking();
                    } else {
                      locationService.startTracking();
                    }
                    // Force re-render
                    setLastRefresh(new Date());
                  }}
                />
                <div className="relative w-11 h-6 bg-gray-200 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-blue-300 rounded-full peer peer-checked:after:translate-x-full rtl:peer-checked:after:-translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:start-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-blue-600"></div>
                <span className="ms-3 text-sm font-medium text-gray-700">
                  Share my location with family
                </span>
              </label>
            </div>

            {/* Mock location toggle */}
            <div className="flex items-center">
              <label className="inline-flex items-center cursor-pointer">
                <input
                  type="checkbox"
                  className="sr-only peer"
                  checked={useMockLocation}
                  onChange={handleToggleMockMode}
                />
                <div className="relative w-11 h-6 bg-gray-200 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-orange-300 rounded-full peer peer-checked:after:translate-x-full rtl:peer-checked:after:-translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:start-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-orange-500"></div>
                <span className="ms-3 text-sm font-medium text-gray-700">
                  Use mock locations (for testing)
                </span>
              </label>
            </div>
            <p className="text-xs text-gray-500 mt-2">
              Enable this if GPS isn't working or for testing purposes
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default FamilyDashboard;
