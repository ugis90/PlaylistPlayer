// src/components/maps/GoogleMapsWrapper.tsx
import React, { useRef, useEffect, useState } from "react";
import { toast } from "sonner";

interface GoogleMapsWrapperProps {
  onMapLoaded?: (map: google.maps.Map) => void;
  width?: string;
  height?: string;
  className?: string;
  apiKey?: string;
  mapOptions?: Partial<google.maps.MapOptions>;
  id?: string;
}

declare global {
  interface Window {
    google?: {
      maps?: any;
    };
  }
}

// Safe wrapper component for Google Maps that isolates it from React's rendering cycle
const GoogleMapsWrapper: React.FC<GoogleMapsWrapperProps> = ({
  onMapLoaded,
  width = "100%",
  height = "400px",
  className = "",
  mapOptions = {},
  id = "google-map-container",
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<google.maps.Map | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const onMapLoadedCalled = useRef(false);
  const mapInitialized = useRef(false);

  // Function to safely check if Google Maps API is fully loaded
  const isGoogleMapsFullyLoaded = (): boolean => {
    return !!(
      window.google?.maps?.Map &&
      window.google?.maps?.Marker &&
      window.google?.maps?.InfoWindow &&
      window.google?.maps?.MapTypeId?.ROADMAP
    );
  };

  // Load Google Maps API if not already loaded
  useEffect(() => {
    // Skip if already loaded
    if (isGoogleMapsFullyLoaded()) {
      setIsLoading(false);
      return;
    }

    // Check for existing script
    const existingScript = document.querySelector(
      'script[src*="maps.googleapis.com/maps/api/js"]',
    );

    if (existingScript) {
      // Script exists, wait for it to load
      const checkInterval = setInterval(() => {
        if (isGoogleMapsFullyLoaded()) {
          clearInterval(checkInterval);
          setIsLoading(false);
        }
      }, 100);

      // Set a timeout to avoid infinite checking
      setTimeout(() => {
        clearInterval(checkInterval);
        if (!isGoogleMapsFullyLoaded()) {
          setError("Google Maps API failed to load within timeout");
        }
      }, 10000);

      return () => {
        clearInterval(checkInterval);
      };
    } else {
      // Need to load the script
      const apiKey = import.meta.env.VITE_GOOGLE_MAPS_API_KEY;
      if (!apiKey) {
        setError("Google Maps API key is missing");
        toast.error("Map functionality requires an API key");
        return;
      }

      const script = document.createElement("script");
      script.src = `https://maps.googleapis.com/maps/api/js?key=${apiKey}&libraries=geometry,places&loading=async`;
      script.async = true;
      script.defer = true;

      script.onload = () => {
        // Introduce a small delay to ensure the API is fully initialized
        setTimeout(() => {
          if (isGoogleMapsFullyLoaded()) {
            setIsLoading(false);
          } else {
            console.warn(
              "Google Maps API loaded but objects not available yet",
            );
            // Try again after a short delay
            setTimeout(() => {
              if (isGoogleMapsFullyLoaded()) {
                setIsLoading(false);
              } else {
                setError("Google Maps API loaded but objects not available");
              }
            }, 300);
          }
        }, 100);
      };

      script.onerror = () => {
        setError("Failed to load Google Maps script");
        toast.error("Failed to load Google Maps");
      };

      document.head.appendChild(script);

      return () => {
        // Don't remove the script on unmount as other components might use it
      };
    }
  }, []);

  // Initialize map after API is loaded
  useEffect(() => {
    if (isLoading || error || !containerRef.current) return;
    if (mapInitialized.current) return;

    // Set initialization flag to prevent multiple initializations
    mapInitialized.current = true;
    onMapLoadedCalled.current = false;

    // Small delay to ensure API is fully loaded
    const initTimeoutId = setTimeout(() => {
      try {
        if (!isGoogleMapsFullyLoaded()) {
          console.warn(
            "Attempted to initialize map but Google Maps API not fully loaded",
          );
          setError("Google Maps API not fully loaded");
          mapInitialized.current = false;
          return;
        }

        // Create a new div element that's completely isolated from React
        const mapContainer = document.createElement("div");
        mapContainer.style.width = "100%";
        mapContainer.style.height = "100%";

        // First clear any existing content to avoid React conflicts
        if (containerRef.current) {
          while (containerRef.current.firstChild) {
            containerRef.current.removeChild(containerRef.current.firstChild);
          }
          containerRef.current.appendChild(mapContainer);
        }

        const defaultOptions = {
          zoom: 10,
          center: { lat: 40.7128, lng: -74.006 }, // Default to NYC
          mapTypeControl: true,
          zoomControl: true,
          streetViewControl: false,
        };

        // Explicitly type window.google to avoid TS errors
        const googleMaps = window.google!.maps;
        const combinedOptions = { ...defaultOptions, ...mapOptions };

        // Make sure mapTypeId is explicitly set to avoid undefined error
        if (!combinedOptions.mapTypeId && googleMaps.MapTypeId) {
          combinedOptions.mapTypeId = googleMaps.MapTypeId.ROADMAP;
        }

        const map = new googleMaps.Map(
          mapContainer,
          combinedOptions as google.maps.MapOptions,
        );

        mapRef.current = map;

        // Small delay before notifying parent to ensure map is fully initialized
        setTimeout(() => {
          // Notify parent component that map is ready
          if (onMapLoaded && map && !onMapLoadedCalled.current) {
            onMapLoadedCalled.current = true;
            onMapLoaded(map);
          }
        }, 100);
      } catch (err: any) {
        console.error("Failed to initialize Google Maps:", err);
        setError(`Map initialization error: ${err.message}`);
        toast.error("Failed to initialize map");
        mapInitialized.current = false;
      }
    }, 200);

    return () => {
      clearTimeout(initTimeoutId);
    };
  }, [isLoading, error, mapOptions, onMapLoaded]);

  // Cleanup when component unmounts
  useEffect(() => {
    return () => {
      // When component unmounts, explicitly clean up all Google Maps objects
      if (mapRef.current && window.google?.maps?.event) {
        try {
          // Try to clean up any listeners
          (window.google.maps.event as any).clearInstanceListeners(
            mapRef.current,
          );

          // Explicitly clear the map's DOM (this is crucial)
          if (containerRef.current) {
            // Detach the map container from our ref container
            while (containerRef.current.firstChild) {
              containerRef.current.removeChild(containerRef.current.firstChild);
            }
          }
        } catch (err) {
          console.error("Error cleaning up map:", err);
        }

        mapRef.current = null;
        mapInitialized.current = false;
        onMapLoadedCalled.current = false;
      }
    };
  }, []);

  return (
    <div
      ref={containerRef}
      id={id}
      style={{ width, height, position: "relative" }}
      className={className}
    >
      {isLoading && (
        <div
          style={{
            position: "absolute",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            backgroundColor: "#f0f0f0",
          }}
        >
          Loading map...
        </div>
      )}

      {error && (
        <div
          style={{
            position: "absolute",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            backgroundColor: "#fff0f0",
            padding: "20px",
            color: "#e53e3e",
          }}
        >
          {error}
        </div>
      )}
    </div>
  );
};

export default GoogleMapsWrapper;
