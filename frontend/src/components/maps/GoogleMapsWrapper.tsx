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

  const isGoogleMapsFullyLoaded = (): boolean => {
    return !!(
      window.google?.maps?.Map &&
      window.google?.maps?.Marker &&
      window.google?.maps?.InfoWindow &&
      window.google?.maps?.MapTypeId?.ROADMAP
    );
  };

  useEffect(() => {
    if (isGoogleMapsFullyLoaded()) {
      setIsLoading(false);
      return;
    }

    const existingScript = document.querySelector(
      'script[src*="maps.googleapis.com/maps/api/js"]',
    );

    if (existingScript) {
      const checkInterval = setInterval(() => {
        if (isGoogleMapsFullyLoaded()) {
          clearInterval(checkInterval);
          setIsLoading(false);
        }
      }, 100);

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
        setTimeout(() => {
          if (isGoogleMapsFullyLoaded()) {
            setIsLoading(false);
          } else {
            console.warn(
              "Google Maps API loaded but objects not available yet",
            );
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

      return () => {};
    }
  }, []);

  useEffect(() => {
    if (isLoading || error || !containerRef.current) return;
    if (mapInitialized.current) return;

    mapInitialized.current = true;
    onMapLoadedCalled.current = false;

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

        const mapContainer = document.createElement("div");
        mapContainer.style.width = "100%";
        mapContainer.style.height = "100%";

        if (containerRef.current) {
          while (containerRef.current.firstChild) {
            containerRef.current.removeChild(containerRef.current.firstChild);
          }
          containerRef.current.appendChild(mapContainer);
        }

        const defaultOptions = {
          zoom: 10,
          center: { lat: 40.7128, lng: -74.006 },
          mapTypeControl: true,
          zoomControl: true,
          streetViewControl: false,
        };

        const googleMaps = window.google!.maps;
        const combinedOptions = { ...defaultOptions, ...mapOptions };

        if (!combinedOptions.mapTypeId && googleMaps.MapTypeId) {
          combinedOptions.mapTypeId = googleMaps.MapTypeId.ROADMAP;
        }

        const map = new googleMaps.Map(
          mapContainer,
          combinedOptions as google.maps.MapOptions,
        );

        mapRef.current = map;

        setTimeout(() => {
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

  useEffect(() => {
    return () => {
      if (mapRef.current && window.google?.maps?.event) {
        try {
          (window.google.maps.event as any).clearInstanceListeners(
            mapRef.current,
          );

          if (containerRef.current) {
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
