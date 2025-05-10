// src/components/GpsTracking.tsx
import React, { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import {
  MapPin,
  Clock,
  CheckCircle,
  Save,
  Loader,
  Route,
  Play,
  Pause,
  Map as MapIcon,
} from "lucide-react";
import apiClient from "../api/client";
import { Vehicle } from "../types";
import { useAuth } from "../auth/AuthContext";
import LocationService, { LocationData } from "../services/LocationService";
import { useGoogleMapsApi } from "../hooks/useGoogleMapsApi";
import GoogleMapsWrapper from "./maps/GoogleMapsWrapper";
import MapErrorBoundary from "./maps/MapErrorBoundary";

interface TripData {
  startLocation: string;
  endLocation: string;
  distance: number;
  startTime: Date;
  endTime: Date | null;
  purpose: string;
  locations: LocationData[];
  vehicleId: number | null;
}

type PolylineType = google.maps.Polyline | null;
type MapInstanceType = google.maps.Map | null;
type MarkerType = google.maps.Marker;

const GpsTracking = () => {
  const [isTracking, setIsTracking] = useState(false);
  const [tripStarted, setTripStarted] = useState(false);
  const [currentLocation, setCurrentLocation] = useState<LocationData | null>(
    null,
  );
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const [startLocation, setStartLocation] = useState<LocationData | null>(null);
  const [tripData, setTripData] = useState<TripData>({
    startLocation: "",
    endLocation: "",
    distance: 0,
    startTime: new Date(),
    endTime: null,
    purpose: "",
    locations: [],
    vehicleId: null,
  });
  const [locationHistoryState, setLocationHistoryState] = useState<
    LocationData[]
  >([]);
  const locationHistoryRef = useRef<LocationData[]>([]);
  const [isLoadingVehicles, setIsLoadingVehicles] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [useMockLocation, setUseMockLocation] = useState(false);
  const [debugLog, setDebugLog] = useState<string[]>([]);
  const [availableVehicles, setAvailableVehicles] = useState<Vehicle[]>([]);
  const [showMap, setShowMap] = useState(false);

  const { userInfo } = useAuth();
  const navigate = useNavigate();
  const locationService = LocationService.getInstance();
  const {
    isLoaded: isMapsApiLoaded,
    loadScript: loadMapsScript,
    error: mapApiError,
  } = useGoogleMapsApi();

  const mapRef = useRef<HTMLDivElement>(null);
  const mapInstanceRef = useRef<MapInstanceType>(null);
  const routePathRef = useRef<PolylineType>(null);
  const markersRef = useRef<MarkerType[]>([]);
  const isMounted = useRef(true);
  const [mapInitialized, setMapInitialized] = useState(false);
  const saveTripDataToLocalStorage = (data: TripData) => {
    try {
      localStorage.setItem("gps_trip_data", JSON.stringify(data));
    } catch (e) {
      console.error("Failed to save trip data to local storage:", e);
    }
  };

  // --- Calculations & Formatting ---
  const calculateDistance = (
    lat1: number,
    lon1: number,
    lat2: number,
    lon2: number,
  ): number => {
    if (lat1 === lat2 && lon1 === lon2) return 0;
    const R = 6371e3;
    const φ1 = (lat1 * Math.PI) / 180;
    const φ2 = (lat2 * Math.PI) / 180;
    const Δφ = ((lat2 - lat1) * Math.PI) / 180;
    const Δλ = ((lon2 - lon1) * Math.PI) / 180;
    const a =
      Math.sin(Δφ / 2) * Math.sin(Δφ / 2) +
      Math.cos(φ1) * Math.cos(φ2) * Math.sin(Δλ / 2) * Math.sin(Δλ / 2);
    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return R * c; // metres
  };

  const calculateTripDistance = (locations: LocationData[]): number => {
    if (locations.length < 2) return 0;
    let totalDistanceMeters = 0;
    for (let i = 1; i < locations.length; i++) {
      totalDistanceMeters += calculateDistance(
        locations[i - 1].latitude,
        locations[i - 1].longitude,
        locations[i].latitude,
        locations[i].longitude,
      );
    }
    return totalDistanceMeters / 1609.34; // miles
  };

  const formatCoords = (location: LocationData | null) => {
    if (!location) return "Waiting for location...";
    return `${location.latitude.toFixed(4)}, ${location.longitude.toFixed(4)}`;
  };

  const formatTime = (date: Date | null) => {
    if (!date) return "---";
    return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  };

  const formatDuration = () => {
    if (!tripStarted && !tripData.endTime) return "0m 0s";
    const startTime = tripData.startTime.getTime();
    const endTime = (tripStarted ? new Date() : tripData.endTime)?.getTime();
    if (!endTime) return "Calculating...";
    const durationMs = Math.max(0, endTime - startTime);
    const totalSeconds = Math.floor(durationMs / 1000);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    if (hours > 0) return `${hours}h ${minutes}m ${seconds}s`;
    if (minutes > 0) return `${minutes}m ${seconds}s`;
    return `${seconds}s`;
  };
  const getVehicleLabel = (vehicle: Vehicle) =>
    `${vehicle.make} ${vehicle.model} (${vehicle.licensePlate || vehicle.year})`;

  const isGoogleMapsFullyLoaded = (): boolean => {
    return !!(
      window.google?.maps?.Map &&
      window.google?.maps?.Marker &&
      window.google?.maps?.InfoWindow &&
      window.google?.maps?.MapTypeId?.ROADMAP
    );
  };

  // *** FIX: Define useCallback functions BEFORE useEffect hooks that use them ***
  const addToDebugLog = useCallback((message: string) => {
    if (!isMounted.current) return;
    const timestamp = new Date().toLocaleTimeString();
    const logEntry = `${timestamp} - ${message}`;
    setDebugLog((prevLog) => [logEntry, ...prevLog.slice(0, 49)]);
  }, []); // Empty dependency array

  const updateMapRoute = useCallback(() => {
    if (
      !mapInstanceRef.current ||
      !window.google?.maps ||
      !routePathRef.current
    ) {
      addToDebugLog("Map not ready for update");
      return;
    }

    const googleMaps = window.google.maps;

    // Use a direct copy of the location history
    const currentHistory = [...locationHistoryRef.current];

    addToDebugLog(`Drawing map route with ${currentHistory.length} points`);
    addToDebugLog(
      `Drawing coordinates: ${JSON.stringify(
        currentHistory.map((loc) => [
          loc.latitude.toFixed(6),
          loc.longitude.toFixed(6),
        ]),
      )}`,
    );

    // Clear existing markers
    markersRef.current.forEach((marker) => marker.setMap(null));
    markersRef.current = [];

    // Create path from all history points
    const path = currentHistory.map((loc) => ({
      lat: Number(loc.latitude),
      lng: Number(loc.longitude),
    }));

    // Add current location to path if it exists
    if (currentLocation) {
      const currentPos = {
        lat: Number(currentLocation.latitude),
        lng: Number(currentLocation.longitude),
      };

      // Only add if it's not the last point already
      const lastPoint = path[path.length - 1];
      if (
        !lastPoint ||
        Math.abs(currentPos.lat - lastPoint.lat) > 0.0000001 ||
        Math.abs(currentPos.lng - lastPoint.lng) > 0.0000001
      ) {
        path.push(currentPos);
      }
    }

    // Always set the polyline path with all points
    routePathRef.current.setPath(path);

    // Add start marker
    if (currentHistory.length > 0) {
      const startMarker = new googleMaps.Marker({
        position: {
          lat: Number(currentHistory[0].latitude),
          lng: Number(currentHistory[0].longitude),
        },
        map: mapInstanceRef.current,
        icon: {
          path: googleMaps.SymbolPath.CIRCLE,
          scale: 8,
          fillColor: "#00C853",
          fillOpacity: 1,
          strokeWeight: 0,
        },
        title: "Start",
      });
      markersRef.current.push(startMarker);
    }

    // Add a marker for each history point (limited to avoid too many markers)
    if (currentHistory.length > 1 && currentHistory.length < 20) {
      for (let i = 1; i < currentHistory.length; i++) {
        const point = currentHistory[i];
        const pointMarker = new googleMaps.Marker({
          position: {
            lat: Number(point.latitude),
            lng: Number(point.longitude),
          },
          map: mapInstanceRef.current,
          icon: {
            path: googleMaps.SymbolPath.CIRCLE,
            scale: 4,
            fillColor: "#2196F3",
            fillOpacity: 0.7,
            strokeWeight: 0,
          },
          title: `Point ${i}`,
        });
        markersRef.current.push(pointMarker);
      }
    }

    // Add current position marker
    if (currentLocation) {
      const currentMarker = new googleMaps.Marker({
        position: {
          lat: Number(currentLocation.latitude),
          lng: Number(currentLocation.longitude),
        },
        map: mapInstanceRef.current,
        icon: {
          path: googleMaps.SymbolPath.FORWARD_CLOSED_ARROW,
          scale: 6,
          fillColor: isTracking ? "#FF3D00" : "#FFA000",
          fillOpacity: 1,
          strokeWeight: 1,
          strokeColor: "white",
          rotation: currentLocation.heading ?? 0,
        },
        title: `Current Position`,
        zIndex: 100,
      });
      markersRef.current.push(currentMarker);
    }

    // Fit map to include all points
    if (path.length > 0) {
      const bounds = new googleMaps.LatLngBounds();
      path.forEach((point) => bounds.extend(point));
      mapInstanceRef.current.fitBounds(bounds);

      // If bounds are too small, set a minimum zoom
      if (path.length === 1) {
        mapInstanceRef.current.setZoom(15);
      }
    }

    addToDebugLog(`Map updated with ${path.length} path points`);
  }, [currentLocation, isTracking, addToDebugLog]);

  useEffect(() => {
    // This is a force update to ensure the map reflects the correct history
    if (
      mapInitialized &&
      showMap &&
      mapInstanceRef.current &&
      routePathRef.current &&
      locationHistoryState.length > 0
    ) {
      addToDebugLog(
        `Force updating map with ${locationHistoryState.length} history points`,
      );
      updateMapRoute();
    }
  }, [
    locationHistoryState,
    mapInitialized,
    showMap,
    updateMapRoute,
    addToDebugLog,
  ]);

  const initializeMap = useCallback(() => {
    // First check if component is mounted and container exists
    if (!mapRef.current || !isMounted.current) {
      addToDebugLog(
        "Map init skipped: Map container not ready or component unmounted",
      );
      return;
    }

    // Enhanced check for Google Maps API being fully loaded
    if (!isGoogleMapsFullyLoaded()) {
      addToDebugLog("Google Maps API not fully loaded yet. Will retry later.");
      // Schedule a retry after a short delay
      if (isMounted.current && showMap) {
        setTimeout(() => {
          if (isMounted.current && showMap && !mapInstanceRef.current) {
            initializeMap();
          }
        }, 300);
      }
      return;
    }

    if (mapInstanceRef.current) {
      addToDebugLog("Map already initialized. Skipping.");
      return;
    }

    addToDebugLog("Initializing map...");

    try {
      const currentHistory = locationHistoryRef.current;
      const center = currentLocation
        ? { lat: currentLocation.latitude, lng: currentLocation.longitude }
        : currentHistory.length > 0
          ? {
              lat: currentHistory[0].latitude,
              lng: currentHistory[0].longitude,
            }
          : { lat: 40.7128, lng: -74.006 };

      // Create map with safe access to Google Maps API
      const mapOptions = {
        zoom: 15,
        center,
        mapTypeId: window.google.maps.MapTypeId.ROADMAP,
        mapTypeControl: true,
        scaleControl: true,
        zoomControl: true,
        streetViewControl: false,
      };

      mapInstanceRef.current = new window.google.maps.Map(
        mapRef.current,
        mapOptions,
      );

      // Only create polyline if map instance was created successfully
      if (mapInstanceRef.current) {
        routePathRef.current = new window.google.maps.Polyline({
          map: mapInstanceRef.current,
          path: [],
          strokeColor: "#0088FF",
          strokeOpacity: 0.8,
          strokeWeight: 5,
        });

        // Only call updateMapRoute if everything was initialized successfully
        if (routePathRef.current) {
          updateMapRoute();
          addToDebugLog("Map initialized successfully.");
        }
      }
    } catch (error: any) {
      console.error("Map Initialization Error:", error);
      addToDebugLog(`Map Initialization Error: ${error.message}`);
      toast.error("Failed to initialize map.");

      // Clean up any partially initialized resources
      if (routePathRef.current) {
        routePathRef.current.setMap(null);
        routePathRef.current = null;
      }
      mapInstanceRef.current = null;
    }
  }, [currentLocation, addToDebugLog, updateMapRoute, showMap]);

  const destroyMapInstance = useCallback(() => {
    addToDebugLog("Attempting to destroy map instance...");

    // First clear all markers
    try {
      if (markersRef.current.length > 0) {
        markersRef.current.forEach((m) => {
          if (m) {
            // Remove listeners first
            if (window.google?.maps?.event) {
              (google.maps.event as any).clearInstanceListeners(m);
            }
            m.setMap(null);
          }
        });
        markersRef.current = [];
      }

      // Clear polyline
      if (routePathRef.current) {
        if (window.google?.maps?.event) {
          (google.maps.event as any).clearInstanceListeners(
            routePathRef.current,
          );
        }
        routePathRef.current.setMap(null);
        routePathRef.current = null;
      }

      // Finally clear map instance
      if (mapInstanceRef.current && window.google?.maps?.event) {
        // Detach all event listeners
        (google.maps.event as any).clearInstanceListeners(
          mapInstanceRef.current,
        );
        mapInstanceRef.current = null;
      }
    } catch (err) {
      console.error("Error during map cleanup:", err);
      addToDebugLog(`Error during map cleanup: ${err}`);
    }

    addToDebugLog("Cleared map instance references.");
  }, [addToDebugLog]);

  // Sync ref whenever state changes
  useEffect(() => {
    // Debug log to see when history state changes
    if (locationHistoryState.length > 0) {
      addToDebugLog(
        `History state updated with ${locationHistoryState.length} locations`,
      );
    }

    // Sync ref whenever state changes
    locationHistoryRef.current = locationHistoryState;
  }, [locationHistoryState, addToDebugLog]);

  useEffect(() => {
    if (locationHistoryState.length > 0) {
      addToDebugLog(
        `Location history state updated. Total points: ${locationHistoryState.length}`,
      );
      addToDebugLog(
        `Current history coordinates: ${JSON.stringify(
          locationHistoryState.map((loc) => [
            loc.latitude.toFixed(6),
            loc.longitude.toFixed(6),
          ]),
        )}`,
      );
    }
  }, [locationHistoryState, addToDebugLog]);

  useEffect(() => {
    return () => {
      if (!isMounted.current) return;

      // Add this safety check
      if (!locationHistoryRef.current) {
        locationHistoryRef.current = [];
      }
    };
  }, []);

  useEffect(() => {
    if (!tripStarted) return;

    // Set up an interval to check for changes in the service's history
    const historyCheckInterval = setInterval(() => {
      if (!isMounted.current) return;

      const serviceHistory = locationService.getLocationHistory();

      // Only update if the history length has changed
      if (serviceHistory.length !== locationHistoryRef.current.length) {
        addToDebugLog(
          `Sync: Service history has ${serviceHistory.length} points, local has ${locationHistoryRef.current.length}`,
        );

        // Update our local state
        locationHistoryRef.current = serviceHistory;
        setLocationHistoryState(serviceHistory);

        // Update trip data
        setTripData((prevTripData) => {
          const newDistance = calculateTripDistance(serviceHistory);
          const updatedTripData = {
            ...prevTripData,
            distance: newDistance,
            locations: serviceHistory,
          };

          saveTripDataToLocalStorage(updatedTripData);

          return updatedTripData;
        });

        // If the map is showing, update it
        if (showMap && mapInstanceRef.current && routePathRef.current) {
          updateMapRoute();
        }
      }
    }, 2000); // Check every 2 seconds

    return () => {
      clearInterval(historyCheckInterval);
    };
  }, [tripStarted, showMap, updateMapRoute]);

  // --- Initialization ---
  useEffect(() => {
    isMounted.current = true;
    addToDebugLog("Component Mounted");
    setUseMockLocation(locationService.isMockModeEnabled());
    fetchVehicles();
    locationService.addListener(handleLocationUpdates);

    const wasTracking = localStorage.getItem("gps_is_tracking") === "true";
    if (wasTracking) {
      startTracking();
      const savedTripData = localStorage.getItem("gps_trip_data");
      if (savedTripData) {
        try {
          const parsedTrip = JSON.parse(savedTripData);
          parsedTrip.startTime = new Date(parsedTrip.startTime);
          parsedTrip.endTime = parsedTrip.endTime
            ? new Date(parsedTrip.endTime)
            : null;
          parsedTrip.locations = Array.isArray(parsedTrip.locations)
            ? parsedTrip.locations
            : [];
          if (isMounted.current) {
            setTripData(parsedTrip);
            setLocationHistoryState(parsedTrip.locations);
            setTripStarted(true);
            setStartLocation(parsedTrip.locations?.[0] || null);
            addToDebugLog("Restored previous trip data.");
            if (parsedTrip.locations.length > 0) setShowMap(true);
          }
        } catch (e) {
          console.error("Failed to restore trip data:", e);
          localStorage.removeItem("gps_trip_data");
        }
      }
    }

    return () => {
      isMounted.current = false;
      addToDebugLog("Component Unmounting");
      locationService.removeListener(handleLocationUpdates);

      // Clean up map resources, but only if Google Maps is available
      if (window.google?.maps) {
        // Clean up markers
        if (markersRef.current.length) {
          markersRef.current.forEach((marker) => {
            if (marker) marker.setMap(null);
          });
          markersRef.current = [];
        }

        // Clean up polyline
        if (routePathRef.current) {
          routePathRef.current.setMap(null);
          routePathRef.current = null;
        }
      }

      // The map instance itself will be cleaned up by the GoogleMapsWrapper
      mapInstanceRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // Run only once on mount

  // --- Map Initialization and Cleanup ---
  const prevShowMapRef = useRef(showMap);

  useEffect(() => {
    // Handle map show/hide transitions safely
    const wasShown = prevShowMapRef.current;
    prevShowMapRef.current = showMap;

    // Map is being closed
    if (wasShown && !showMap) {
      const timeoutId = setTimeout(() => {
        if (isMounted.current) {
          destroyMapInstance();
        }
      }, 0);
      return () => clearTimeout(timeoutId);
    }

    // Map is being opened
    if (showMap) {
      const timeoutId = setTimeout(() => {
        if (!isMounted.current) return;

        if (!isMapsApiLoaded && !mapApiError) {
          loadMapsScript()
            .then((loaded) => {
              if (
                loaded &&
                isMounted.current &&
                mapRef.current &&
                !mapInstanceRef.current &&
                showMap // Double-check map is still supposed to be shown
              ) {
                // Add a delay to ensure API is fully loaded
                setTimeout(() => {
                  if (isMounted.current && showMap) {
                    initializeMap();
                  }
                }, 200);
              }
            })
            .catch((err) => {
              console.error("Map script load promise rejected:", err);
              addToDebugLog(`Map script load error: ${err.message}`);
            });
        } else if (
          isMapsApiLoaded &&
          mapRef.current &&
          !mapInstanceRef.current &&
          showMap
        ) {
          // Add a delay here as well
          setTimeout(() => {
            if (isMounted.current && showMap) {
              initializeMap();
            }
          }, 200);
        }
      }, 0);

      return () => clearTimeout(timeoutId);
    }
  }, [
    showMap,
    isMapsApiLoaded,
    loadMapsScript,
    mapApiError,
    initializeMap,
    destroyMapInstance,
  ]);

  useEffect(() => {
    if (
      mapInitialized &&
      showMap &&
      mapInstanceRef.current &&
      routePathRef.current
    ) {
      updateMapRoute();
    }
  }, [currentLocation, mapInitialized, showMap, updateMapRoute]);

  useEffect(() => {
    if (!showMap) {
      // Reset map initialization state when map is hidden
      setMapInitialized(false);

      // Clean up map resources
      if (window.google?.maps) {
        if (markersRef.current.length) {
          markersRef.current.forEach((marker) => {
            if (marker) marker.setMap(null);
          });
          markersRef.current = [];
        }

        if (routePathRef.current) {
          routePathRef.current.setMap(null);
          routePathRef.current = null;
        }
      }

      mapInstanceRef.current = null;
      addToDebugLog("Map hidden and resources cleaned up");
    }
  }, [showMap, addToDebugLog]);

  useEffect(() => {
    // When trip is started, make sure we capture the first location point
    if (
      tripStarted &&
      locationHistoryRef.current.length === 0 &&
      currentLocation
    ) {
      addToDebugLog("Adding initial location to history for new trip");

      const updatedHistory = [currentLocation];
      locationHistoryRef.current = updatedHistory;
      setLocationHistoryState(updatedHistory);

      setTripData((prevTripData) => {
        const updatedTripData = {
          ...prevTripData,
          locations: updatedHistory,
        };
        saveTripDataToLocalStorage(updatedTripData);
        return updatedTripData;
      });
    }
  }, [tripStarted, currentLocation, addToDebugLog, saveTripDataToLocalStorage]);

  // --- Data Fetching ---
  const fetchVehicles = async () => {
    if (!isMounted.current) return;
    setIsLoadingVehicles(true);
    try {
      addToDebugLog("Fetching vehicles...");
      const response = await apiClient.get("/vehicles?pageSize=100");
      addToDebugLog(`Vehicle response status: ${response.status}`);
      let vehicleData: Vehicle[] = [];
      if (response.data?.resource && Array.isArray(response.data.resource)) {
        vehicleData = response.data.resource
          .map((item: any) => item.resource || item)
          .filter(Boolean);
      } else {
        addToDebugLog(
          `Unexpected vehicle data format in GPS: ${JSON.stringify(response.data)}`,
        );
      }
      const validVehicles = vehicleData.filter((v) => v && v.id);
      addToDebugLog(`Found ${validVehicles.length} valid vehicles.`);
      if (isMounted.current) {
        setAvailableVehicles(validVehicles);
        const savedVehicleId = localStorage.getItem("gps_vehicleId");
        if (validVehicles.length === 1)
          handleVehicleSelect(validVehicles[0].id);
        else if (
          savedVehicleId &&
          validVehicles.some((v) => v.id === parseInt(savedVehicleId))
        )
          handleVehicleSelect(parseInt(savedVehicleId));
        else if (validVehicles.length > 0 && !tripData.vehicleId)
          handleVehicleSelect(validVehicles[0].id);
      }
    } catch (error: any) {
      console.error("Error fetching vehicles:", error);
      addToDebugLog(`Error fetching vehicles: ${error.message}`);
      if (isMounted.current) toast.error("Failed to load vehicles");
    } finally {
      if (isMounted.current) setIsLoadingVehicles(false);
    }
  };

  // --- Location Handling ---
  const handleLocationUpdates = useCallback(
    (locations: Map<string, LocationData>) => {
      if (!isMounted.current) return;

      const userId = userInfo?.username || "user";
      const newLocation = locations.get(userId);

      if (!newLocation) return;

      // Always update current location
      setCurrentLocation(newLocation);

      const speedMph = newLocation.speed
        ? (newLocation.speed * 2.237).toFixed(1) + "mph"
        : "N/A";
      addToDebugLog(
        `Location Update Received: ${newLocation.latitude.toFixed(4)}, ${newLocation.longitude.toFixed(4)} Speed: ${speedMph}`,
      );

      // If recording a trip, refresh our local history from the service
      if (tripStarted) {
        // The service will have already added the location to history in its internal update
        // We just need to get the updated history
        const updatedHistory = locationService.getLocationHistory();

        // Only update our local state if the history has changed
        if (updatedHistory.length !== locationHistoryRef.current.length) {
          addToDebugLog(
            `Updated history from service: ${updatedHistory.length} points (was ${locationHistoryRef.current.length})`,
          );

          // Update our local refs and state
          locationHistoryRef.current = updatedHistory;
          setLocationHistoryState(updatedHistory);

          // Update trip data with new history
          setTripData((prevTripData) => {
            const newDistance = calculateTripDistance(updatedHistory);
            const updatedTripData = {
              ...prevTripData,
              distance: newDistance,
              locations: updatedHistory,
            };

            // Save to local storage
            saveTripDataToLocalStorage(updatedTripData);

            return updatedTripData;
          });
        }
      }
    },
    [tripStarted, userInfo?.username, addToDebugLog],
  );

  const startTracking = () => {
    if (isTracking) return;
    addToDebugLog("Attempting to start location tracking...");
    locationService.setMockMode(useMockLocation);
    locationService.startTracking();
    setIsTracking(true);
    localStorage.setItem("gps_is_tracking", "true");
    addToDebugLog(`Tracking started. Mock mode: ${useMockLocation}`);
  };

  const stopTracking = (persistState = true) => {
    if (!isTracking) return;
    addToDebugLog("Stopping location tracking...");
    locationService.stopTracking();
    setIsTracking(false);
    if (persistState) {
      localStorage.setItem("gps_is_tracking", "false");
    } else {
      localStorage.removeItem("gps_is_tracking");
    }
    addToDebugLog("Tracking stopped.");
    if (tripStarted) {
      toast.info("Location tracking paused. Trip recording remains active.");
    }
  };

  // --- Trip Management ---
  const startTrip = () => {
    if (!isTracking) {
      toast.warning("Start location tracking first.");
      addToDebugLog("Cannot start trip: Tracking not active.");
      startTracking();
      setTimeout(() => {
        if (!isMounted.current) return;
        const loc = locationService.getUserLocation(
          userInfo?.username || "user",
        );
        if (loc && tripData.vehicleId) {
          initiateTripStart(loc);
        } else if (!tripData.vehicleId) {
          toast.error("Please select a vehicle first.");
          addToDebugLog("Cannot start trip: No vehicle selected.");
        } else {
          toast.error("Waiting for location...");
          addToDebugLog("Cannot start trip: No current location yet.");
        }
      }, 2000);
      return;
    }

    const loc =
      currentLocation ??
      locationService.getUserLocation(userInfo?.username || "user");

    if (!loc) {
      toast.error("Waiting for current location to start trip...");
      addToDebugLog("Cannot start trip: No current location.");
      return;
    }

    if (!tripData.vehicleId) {
      toast.error("Please select a vehicle before starting the trip.");
      addToDebugLog("Cannot start trip: No vehicle selected.");
      return;
    }

    initiateTripStart(loc);
  };

  const initiateTripStart = (startLoc: LocationData) => {
    if (!isMounted.current) return;

    // Start recording history in the service
    locationService.clearLocationHistory();
    locationService.startRecordingHistory();

    // Add the first location point
    locationService.addLocationToHistory(startLoc);

    // Get the initial history array from the service
    const initialHistory = locationService.getLocationHistory();

    setStartLocation(startLoc);

    // Update our local state with this history
    locationHistoryRef.current = initialHistory;
    setLocationHistoryState(initialHistory);

    addToDebugLog(
      `Trip recording started with initial location: ${startLoc.latitude.toFixed(4)}, ${startLoc.longitude.toFixed(4)}`,
    );
    addToDebugLog(
      `Initial history from service has ${initialHistory.length} points`,
    );

    const startTime = new Date();
    const initialTripData: TripData = {
      startLocation: `GPS: ${startLoc.latitude.toFixed(4)}, ${startLoc.longitude.toFixed(4)}`,
      endLocation: "",
      distance: 0,
      startTime: startTime,
      endTime: null,
      purpose: "",
      locations: initialHistory,
      vehicleId: tripData.vehicleId,
    };

    setTripData(initialTripData);
    setTripStarted(true);
    saveTripDataToLocalStorage(initialTripData);

    addToDebugLog(
      `Trip recording started for vehicle ID: ${tripData.vehicleId} at ${startTime.toLocaleTimeString()}`,
    );

    toast.success("Trip recording started!");
    setShowMap(true);
  };

  const endTrip = () => {
    if (!tripStarted || !isMounted.current) return;

    // Stop recording history in the service
    locationService.stopRecordingHistory();

    // Get the final history from the service
    const finalHistory = locationService.getLocationHistory();

    addToDebugLog(`Ending trip with ${finalHistory.length} points.`);

    const endTime = new Date();
    let finalEndLocation = tripData.endLocation;

    if (finalHistory.length > 0) {
      const lastLoc = finalHistory[finalHistory.length - 1];
      finalEndLocation = `GPS: ${lastLoc.latitude.toFixed(4)}, ${lastLoc.longitude.toFixed(4)}`;
    }

    const finalDistance = calculateTripDistance(finalHistory);

    const finalTripData = {
      ...tripData,
      endTime,
      endLocation: finalEndLocation,
      distance: finalDistance,
      locations: finalHistory,
    };

    setTripData(finalTripData);
    saveTripDataToLocalStorage(finalTripData);
    setTripStarted(false);

    addToDebugLog(
      `Trip recording ended at ${endTime.toLocaleTimeString()}. Final Distance: ${finalDistance.toFixed(2)}. Ready to save.`,
    );

    toast.info("Trip ended. Review details and save.");
  };

  const saveTrip = async () => {
    if (!tripData.vehicleId) {
      toast.error("Please select a vehicle.");
      return;
    }
    if (!tripData.purpose.trim()) {
      toast.error("Please enter a purpose.");
      return;
    }
    if (tripData.locations.length < 1) {
      toast.warning("No location data recorded. Saving with 0 distance.");
    }

    const processedTripData = processLocationDataForSaving();
    if (!processedTripData) return;
    if (processedTripData.distance <= 0 && tripData.locations.length > 1) {
      toast.warning("Calculated distance is 0, saving anyway.");
    } else if (processedTripData.distance <= 0) {
      addToDebugLog("Saving trip with 0 distance (1 location point).");
    }

    setIsSaving(true);
    addToDebugLog("Saving trip to database...");
    try {
      console.log(
        `API Request: POST /vehicles/${tripData.vehicleId}/trips`,
        processedTripData,
      );
      const response = await apiClient.post(
        `/vehicles/${tripData.vehicleId}/trips`,
        processedTripData,
      );
      addToDebugLog(
        `Trip saved successfully: ${JSON.stringify(response.data)}`,
      );
      toast.success("GPS trip saved successfully!");
      resetTripState();
      localStorage.removeItem("gps_trip_data");
    } catch (error: any) {
      console.error("Error saving trip:", error);
      const errorDetail =
        error.response?.data?.detail ||
        error.response?.data?.errors?.Distance?.[0] ||
        error.message ||
        "Unknown error";
      addToDebugLog(`Error saving trip: ${errorDetail}`);
      toast.error(`Failed to save trip: ${errorDetail}`);
    } finally {
      if (isMounted.current) setIsSaving(false);
    }
  };

  const resetTripState = useCallback(() => {
    if (!isMounted.current) return;

    // Clear history in the service
    locationService.clearLocationHistory();
    locationService.stopRecordingHistory();

    // Reset local state
    setTripData({
      startLocation: "",
      endLocation: "",
      distance: 0,
      startTime: new Date(),
      endTime: null,
      purpose: "",
      locations: [],
      vehicleId: tripData.vehicleId,
    });

    locationHistoryRef.current = [];
    setLocationHistoryState([]);
    setStartLocation(null);
    setTripStarted(false);
    localStorage.removeItem("gps_trip_data");
    addToDebugLog("Trip state reset.");
  }, [addToDebugLog, tripData.vehicleId]);

  const processLocationDataForSaving = () => {
    const finalHistory = tripData.locations;
    if (finalHistory.length < 1) {
      addToDebugLog("Cannot save: No location data in final trip state.");
      return null;
    }
    const finalDistanceMiles = tripData.distance;
    const startPoint = finalHistory[0];
    const endPoint = finalHistory[finalHistory.length - 1];
    const startLocationName =
      tripData.startLocation.startsWith("GPS:") ||
      !tripData.startLocation.trim()
        ? `GPS: ${startPoint.latitude.toFixed(4)}, ${startPoint.longitude.toFixed(4)}`
        : tripData.startLocation;
    const endLocationName =
      tripData.endLocation ||
      `GPS: ${endPoint.latitude.toFixed(4)}, ${endPoint.longitude.toFixed(4)}`;
    const startTimeStr =
      tripData.startTime instanceof Date
        ? tripData.startTime.toISOString()
        : new Date().toISOString();
    const endTimeStr =
      tripData.endTime instanceof Date
        ? tripData.endTime.toISOString()
        : new Date().toISOString();
    addToDebugLog(
      `Prepared trip for saving. Final Distance: ${finalDistanceMiles.toFixed(2)} miles. Points: ${finalHistory.length}`,
    );
    return {
      startLocation: startLocationName,
      endLocation: endLocationName,
      distance: finalDistanceMiles,
      startTime: startTimeStr,
      endTime: endTimeStr,
      purpose: tripData.purpose || "GPS Tracked Trip",
    };
  };

  const handleVehicleSelect = (vehicleId: number | null) => {
    if (!isMounted.current) return;
    setTripData((prev) => ({ ...prev, vehicleId }));
    if (vehicleId) {
      localStorage.setItem("gps_vehicleId", vehicleId.toString());
      locationService.setCurrentVehicle(vehicleId);
      addToDebugLog(`Vehicle ${vehicleId} selected.`);
    } else {
      localStorage.removeItem("gps_vehicleId");
      addToDebugLog("No vehicle selected.");
    }
  };

  // --- Render ---
  return (
    <div className="container mx-auto px-4 py-8">
      {/* Header */}
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl font-bold">GPS Trip Tracker</h1>
        <button
          onClick={() => navigate(-1)}
          className="text-blue-600 hover:underline"
        >
          Go Back
        </button>
      </div>

      {/* Main Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
        {/* Column 1: Controls & Status */}
        <div className="bg-white rounded-lg shadow-md p-6 space-y-4">
          <h2 className="text-xl font-bold">Tracking Controls</h2>
          {/* Location Status */}
          <div className="flex items-center p-3 rounded-lg bg-gray-100">
            <MapPin className="h-5 w-5 mr-2 text-blue-600" />
            <div>
              <p className="text-sm font-medium">Current Location:</p>
              <p className="text-xs text-gray-600">
                {formatCoords(currentLocation)}
              </p>
              {currentLocation?.speed !== null &&
                currentLocation?.speed !== undefined && (
                  <p className="text-xs text-gray-600">
                    Speed: {(currentLocation.speed * 2.237).toFixed(1)} mph
                  </p>
                )}
            </div>
          </div>
          {/* Mock Location Toggle */}
          <div className="flex items-center justify-between">
            <label
              htmlFor="mockToggle"
              className="text-sm font-medium text-gray-700"
            >
              Use Mock Location
            </label>
            <label className="inline-flex items-center cursor-pointer">
              <input
                id="mockToggle"
                type="checkbox"
                className="sr-only peer"
                checked={useMockLocation}
                onChange={() => {
                  const newMode = !useMockLocation;
                  setUseMockLocation(newMode);
                  locationService.setMockMode(newMode);
                  addToDebugLog(
                    `Mock mode ${newMode ? "enabled" : "disabled"}.`,
                  );
                }}
                disabled={isTracking}
              />
              <div className="relative w-11 h-6 bg-gray-200 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:start-[2px] after:bg-white after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-orange-500"></div>
            </label>
          </div>
          {/* Vehicle Selector */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Select Vehicle <span className="text-red-500">*</span>
            </label>
            <select
              className="w-full border rounded-lg p-2 bg-white disabled:bg-gray-100"
              value={tripData.vehicleId || ""}
              onChange={(e) =>
                handleVehicleSelect(
                  e.target.value ? parseInt(e.target.value) : null,
                )
              }
              disabled={tripStarted || isLoadingVehicles}
            >
              <option value="">
                {isLoadingVehicles ? "Loading..." : "Select a vehicle..."}
              </option>
              {availableVehicles.map((vehicle) => (
                <option key={vehicle.id} value={vehicle.id}>
                  {getVehicleLabel(vehicle)}
                </option>
              ))}
            </select>
            {availableVehicles.length === 0 && !isLoadingVehicles && (
              <p className="text-xs text-red-600 mt-1">
                No vehicles available. Add one first.
              </p>
            )}
          </div>
          {/* Tracking & Trip Buttons */}
          <div className="space-y-3 pt-2">
            {!isTracking ? (
              <button
                onClick={startTracking}
                className="w-full bg-blue-600 hover:bg-blue-700 text-white rounded-lg py-2.5 px-4 flex items-center justify-center"
              >
                <Play className="h-5 w-5 mr-2" /> Start Location Tracking
              </button>
            ) : (
              <button
                onClick={() => stopTracking()}
                className="w-full bg-red-600 hover:bg-red-700 text-white rounded-lg py-2.5 px-4 flex items-center justify-center"
              >
                <Pause className="h-5 w-5 mr-2" /> Stop Location Tracking
              </button>
            )}
            {isTracking && !tripStarted && (
              <button
                onClick={startTrip}
                disabled={!tripData.vehicleId}
                className={`w-full ${tripData.vehicleId ? "bg-green-600 hover:bg-green-700" : "bg-gray-400 cursor-not-allowed"} text-white rounded-lg py-2.5 px-4 flex items-center justify-center`}
              >
                <Clock className="h-5 w-5 mr-2" /> Start Trip Recording
              </button>
            )}
            {tripStarted && (
              <button
                onClick={endTrip}
                className="w-full bg-yellow-500 hover:bg-yellow-600 text-black rounded-lg py-2.5 px-4 flex items-center justify-center"
              >
                <CheckCircle className="h-5 w-5 mr-2" /> End Trip Recording
              </button>
            )}
          </div>
        </div>

        {/* Column 2: Trip Details */}
        <div className="bg-white rounded-lg shadow-md p-6 space-y-4">
          <h2 className="text-xl font-bold">Trip Details</h2>
          {tripStarted || locationHistoryRef.current.length > 0 ? ( // Use ref for check
            <>
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <span className="font-medium text-gray-600">Start Time:</span>{" "}
                  {formatTime(tripData.startTime)}
                </div>
                <div>
                  <span className="font-medium text-gray-600">Duration:</span>{" "}
                  {formatDuration()}
                </div>
                <div>
                  <span className="font-medium text-gray-600">Distance:</span>{" "}
                  {tripData.distance.toFixed(2)} mi
                </div>
                <div>
                  <span className="font-medium text-gray-600">Points:</span>{" "}
                  {locationHistoryRef.current.length}
                </div>{" "}
                {/* Use ref */}
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Purpose <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={tripData.purpose}
                  onChange={(e) =>
                    setTripData({ ...tripData, purpose: e.target.value })
                  }
                  placeholder="e.g., Commute, Shopping"
                  className="w-full border rounded-lg p-2"
                  disabled={tripStarted}
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Start Location
                </label>
                <input
                  type="text"
                  value={tripData.startLocation}
                  onChange={(e) =>
                    setTripData({ ...tripData, startLocation: e.target.value })
                  }
                  className="w-full border rounded-lg p-2"
                  disabled={tripStarted}
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  End Location
                </label>
                <input
                  type="text"
                  value={tripData.endLocation}
                  onChange={(e) =>
                    setTripData({ ...tripData, endLocation: e.target.value })
                  }
                  className="w-full border rounded-lg p-2"
                  disabled={tripStarted}
                />
              </div>
              {!tripStarted &&
                locationHistoryRef.current.length > 0 && ( // Use ref
                  <button
                    onClick={saveTrip}
                    disabled={isSaving || !tripData.purpose}
                    className={`w-full mt-4 ${!tripData.purpose || isSaving ? "bg-gray-400 cursor-not-allowed" : "bg-teal-600 hover:bg-teal-700"} text-white rounded-lg py-2.5 px-4 flex items-center justify-center`}
                  >
                    {isSaving ? (
                      <Loader className="h-5 w-5 mr-2 animate-spin" />
                    ) : (
                      <Save className="h-5 w-5 mr-2" />
                    )}
                    {isSaving ? "Saving..." : "Save Trip"}
                  </button>
                )}
              {!tripStarted &&
                locationHistoryRef.current.length > 0 && ( // Use ref
                  <button
                    onClick={resetTripState}
                    className="w-full mt-2 bg-gray-500 hover:bg-gray-600 text-white rounded-lg py-2 px-4 text-sm"
                  >
                    Discard Trip Data
                  </button>
                )}
            </>
          ) : (
            <div className="text-center py-10 text-gray-500">
              <Route className="h-10 w-10 mx-auto mb-2 text-gray-400" /> Start
              tracking and begin a trip to see details here.
            </div>
          )}
        </div>

        {/* Column 3: Log & Map Toggle */}
        <div className="bg-white rounded-lg shadow-md p-6 space-y-4">
          <div className="flex justify-between items-center">
            <h2 className="text-xl font-bold">Activity Log</h2>
            <button
              onClick={() => setDebugLog([])}
              className="text-xs text-gray-500 hover:text-red-600"
            >
              Clear Log
            </button>
          </div>
          <div className="text-xs font-mono bg-gray-800 text-gray-200 p-3 rounded-lg h-64 overflow-y-auto">
            {debugLog.length > 0 ? (
              debugLog.map((log, i) => (
                <div key={i} className="whitespace-pre-wrap break-words pb-1">
                  {log}
                </div>
              ))
            ) : (
              <div className="text-gray-400">Log is empty...</div>
            )}
          </div>
          <button
            onClick={() => setShowMap(!showMap)}
            className="w-full bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg py-2.5 px-4 flex items-center justify-center"
          >
            <MapIcon className="h-5 w-5 mr-2" />{" "}
            {showMap ? "Hide Map" : "Show Map"}
          </button>
        </div>
      </div>

      {/* Map Section */}
      {showMap && (
        <div className="bg-white rounded-lg shadow-md p-4 mb-6">
          <h2 className="text-xl font-bold mb-4">Live Map</h2>
          <MapErrorBoundary>
            <div className="w-full h-96 rounded-lg border border-gray-300">
              <GoogleMapsWrapper
                height="384px" // 96 * 4px = 384px
                onMapLoaded={(mapInstance) => {
                  // Only proceed if not already initialized
                  if (mapInitialized) {
                    addToDebugLog(
                      "Map already initialized, skipping redundant initialization",
                    );
                    return;
                  }

                  addToDebugLog("Initial map instance received");

                  // Store the map instance reference
                  mapInstanceRef.current = mapInstance;

                  // Create polyline once we have the map instance
                  if (mapInstance && window.google?.maps) {
                    routePathRef.current = new window.google.maps.Polyline({
                      map: mapInstance,
                      path: [],
                      strokeColor: "#0088FF",
                      strokeOpacity: 0.8,
                      strokeWeight: 5,
                    });

                    // Add route data and update markers
                    updateMapRoute();
                    setMapInitialized(true);
                    addToDebugLog("Map initialized successfully.");
                  }
                }}
                mapOptions={{
                  zoom: 15,
                  center: currentLocation
                    ? {
                        lat: currentLocation.latitude,
                        lng: currentLocation.longitude,
                      }
                    : locationHistoryRef.current.length > 0
                      ? {
                          lat: locationHistoryRef.current[0].latitude,
                          lng: locationHistoryRef.current[0].longitude,
                        }
                      : { lat: 40.7128, lng: -74.006 },
                  mapTypeControl: true,
                  scaleControl: true,
                  zoomControl: true,
                  streetViewControl: false,
                }}
              />
            </div>
          </MapErrorBoundary>
        </div>
      )}
    </div>
  );
};

export default GpsTracking;
