// src/hooks/useGoogleMapsApi.ts
import { useState, useEffect, useRef, useCallback } from "react";
import { toast } from "sonner";

let mapsApiLoaded = typeof window !== "undefined" && !!window.google?.maps;
let mapsApiLoadPromise: Promise<boolean> | null = null;

export function useGoogleMapsApi() {
  const [isLoaded, setIsLoaded] = useState<boolean>(mapsApiLoaded);
  const [error, setError] = useState<string | null>(null);
  const isMounted = useRef(true);

  useEffect(() => {
    isMounted.current = true;
    // If already loaded when hook mounts, ensure state is true
    if (mapsApiLoaded && !isLoaded) {
      setIsLoaded(true);
    }
    return () => {
      isMounted.current = false;
    };
  }, [isLoaded]); // Re-sync if isLoaded changes externally somehow

  const loadScript = useCallback((): Promise<boolean> => {
    // Return existing promise if loading or already loaded
    if (mapsApiLoadPromise) return mapsApiLoadPromise;
    if (mapsApiLoaded) {
      if (!isLoaded && isMounted.current) setIsLoaded(true);
      return Promise.resolve(true);
    }

    mapsApiLoadPromise = new Promise((resolve, reject) => {
      if (!isMounted.current) {
        reject(new Error("Component unmounted before script could load"));
        mapsApiLoadPromise = null;
        return;
      }
      setError(null);

      const existingScript = document.querySelector(
        'script[src*="maps.googleapis.com/maps/api/js"]',
      );
      if (existingScript) {
        console.log("useGoogleMapsApi: Script tag exists, waiting for load...");
        const checkInterval = setInterval(() => {
          if (window.google?.maps) {
            clearInterval(checkInterval);
            console.log("useGoogleMapsApi: Existing script loaded.");
            mapsApiLoaded = true;
            if (isMounted.current) setIsLoaded(true);
            resolve(true);
            mapsApiLoadPromise = null;
          }
          // Add a timeout mechanism here if needed to prevent infinite checks
        }, 100); // Check every 100ms
        return; // Don't add another script tag
      }

      const apiKey = import.meta.env.VITE_GOOGLE_MAPS_API_KEY;
      if (!apiKey) {
        console.error("Google Maps API Key is missing!");
        toast.error("Map functionality requires an API key.");
        if (isMounted.current) setError("API Key Missing");
        reject(new Error("API Key Missing"));
        mapsApiLoadPromise = null;
        return;
      }

      console.log("useGoogleMapsApi: Loading Google Maps script...");
      const script = document.createElement("script");
      script.src = `https://maps.googleapis.com/maps/api/js?key=${apiKey}&libraries=geometry,places&loading=async`;
      script.async = true;
      script.defer = true;

      script.onload = () => {
        console.log("useGoogleMapsApi: Script loaded successfully via onload.");
        mapsApiLoaded = true;
        if (isMounted.current) setIsLoaded(true);
        resolve(true);
        mapsApiLoadPromise = null;
      };
      script.onerror = () => {
        console.error("useGoogleMapsApi: Failed to load script.");
        if (isMounted.current) {
          toast.error("Failed to load Google Maps.");
          setError("Script Load Failed");
        }
        reject(new Error("Script Load Failed"));
        mapsApiLoadPromise = null;
      };
      document.head.appendChild(script);
    });

    return mapsApiLoadPromise;
  }, [isLoaded]); // Recreate loadScript if isLoaded changes

  return { isLoaded, loadScript, error };
}
