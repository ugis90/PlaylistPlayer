// src/services/LocationService.ts
// Replace the implementation with this corrected version

import { toast } from "sonner";
import apiClient from "../api/client";

export interface LocationData {
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

class LocationService {
  private static instance: LocationService;
  private locationCache: Map<string, LocationData> = new Map();
  private listeners: Set<(locations: Map<string, LocationData>) => void> =
    new Set();
  private trackingInterval: number | null = null;
  private isTracking: boolean = false;
  private mockMode: boolean = false;
  private locationHistory: LocationData[] = [];
  private isRecordingHistory: boolean = false;

  // Keys for storage
  private readonly TRACKING_KEY = "fleet_tracking_enabled";
  private readonly USER_LOCATION_KEY = "fleet_user_location";

  private constructor() {
    // Initialize by loading from storage
    this.loadFromStorage();

    // Check if tracking was previously enabled
    const trackingEnabled = localStorage.getItem(this.TRACKING_KEY) === "true";
    if (trackingEnabled) {
      this.startTracking();
    }

    // Check if mock mode was set (for testing when real GPS isn't available)
    this.mockMode = localStorage.getItem("fleet_mock_location_mode") === "true";
  }

  public static getInstance(): LocationService {
    if (!LocationService.instance) {
      LocationService.instance = new LocationService();
    }
    return LocationService.instance;
  }

  public startRecordingHistory(): void {
    this.locationHistory = [];
    this.isRecordingHistory = true;
    console.log("Started recording location history");
  }

  public stopRecordingHistory(): void {
    this.isRecordingHistory = false;
    console.log(
      `Stopped recording location history. Recorded ${this.locationHistory.length} points`,
    );
  }

  public getLocationHistory(): LocationData[] {
    return [...this.locationHistory]; // Return a copy
  }

  public isHistoryRecordingActive(): boolean {
    return this.isRecordingHistory;
  }

  public addLocationToHistory(location: LocationData): void {
    if (this.isRecordingHistory) {
      // Deep copy to avoid reference issues
      const locationCopy = JSON.parse(JSON.stringify(location));
      this.locationHistory.push(locationCopy);
      console.log(
        `Added location to history. Total points: ${this.locationHistory.length}`,
      );
    }
  }

  public clearLocationHistory(): void {
    this.locationHistory = [];
    console.log("Location history cleared");
  }

  public setMockMode(enabled: boolean): void {
    this.mockMode = enabled;
    localStorage.setItem(
      "fleet_mock_location_mode",
      enabled ? "true" : "false",
    );

    if (enabled) {
      toast.info("Using mock locations for testing");
    }
  }

  public isMockModeEnabled(): boolean {
    return this.mockMode;
  }

  public startTracking(intervalMs: number = 10000): void {
    if (this.isTracking) return;

    this.isTracking = true;
    localStorage.setItem(this.TRACKING_KEY, "true");

    // Get location immediately
    this.updateCurrentLocation();

    // Set interval for continuous updates
    this.trackingInterval = window.setInterval(() => {
      this.updateCurrentLocation();
    }, intervalMs);

    console.log(`Location tracking started with ${intervalMs}ms interval`);
    toast.success("Location sharing is now active");
  }

  public stopTracking(): void {
    if (!this.isTracking) return;

    this.isTracking = false;
    localStorage.setItem(this.TRACKING_KEY, "false");

    if (this.trackingInterval) {
      window.clearInterval(this.trackingInterval);
      this.trackingInterval = null;
    }

    console.log("Location tracking stopped");
    toast.info("Location sharing is now paused");
  }

  private updateCurrentLocation(): void {
    // If in mock mode, use mock location
    if (this.mockMode) {
      this.updateMockCurrentLocation();
      return;
    }

    // Check if browser supports geolocation
    if (!navigator.geolocation) {
      console.error("Geolocation is not supported by this browser");
      toast.error("Your browser doesn't support location services");
      this.stopTracking();
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        const currentUserId = this.getCurrentUserId();
        const currentVehicleId = this.getCurrentVehicleId();

        if (!currentUserId || !currentVehicleId) {
          console.warn("Cannot update location: missing user ID or vehicle ID");
          return;
        }

        const locationData: LocationData = {
          userId: currentUserId,
          vehicleId: currentVehicleId,
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          speed: position.coords.speed,
          heading: position.coords.heading,
          accuracy: position.coords.accuracy,
          timestamp: new Date().toISOString(),
        };

        // Update cache
        this.locationCache.set(currentUserId, locationData);

        // Save to storage
        this.saveToStorage();

        // Upload to server - using the real API endpoint
        this.uploadLocationToServer(locationData);

        // Notify listeners
        this.notifyListeners();

        if (this.isRecordingHistory) {
          this.addLocationToHistory(locationData);
        }
      },
      (error) => {
        console.error("Error getting location:", error);

        // If the error is permission denied, stop tracking
        if (error.code === error.PERMISSION_DENIED) {
          toast.error(
            "Location permission denied. Please enable location services.",
          );
          this.stopTracking();

          // Switch to mock mode for testing if we can't get real locations
          this.setMockMode(true);
        }
      },
      {
        enableHighAccuracy: true,
        timeout: 15000,
        maximumAge: 0,
      },
    );
  }

  private updateMockCurrentLocation(): void {
    const currentUserId = this.getCurrentUserId();
    const currentVehicleId = this.getCurrentVehicleId();

    if (!currentUserId || !currentVehicleId) {
      console.warn(
        "Cannot update mock location: missing user ID or vehicle ID",
      );
      return;
    }

    // Generate a location near New York City (or use last location as base if exists)
    const lastLocation = this.locationCache.get(currentUserId);

    let latitude = 40.7128; // NYC default
    let longitude = -74.006;

    if (lastLocation) {
      // Simulate movement by adding small random changes to location
      latitude = lastLocation.latitude + (Math.random() - 0.5) * 0.001;
      longitude = lastLocation.longitude + (Math.random() - 0.5) * 0.001;
    }

    const locationData: LocationData = {
      userId: currentUserId,
      vehicleId: currentVehicleId,
      latitude,
      longitude,
      speed: 15 + Math.random() * 30, // Random speed between 15-45 mph
      heading: Math.random() * 360, // Random heading
      accuracy: 10,
      timestamp: new Date().toISOString(),
    };

    // Update cache
    this.locationCache.set(currentUserId, locationData);

    // Save to storage
    this.saveToStorage();

    // In mock mode, still try to upload to server if available
    this.uploadLocationToServer(locationData);

    // Notify listeners
    this.notifyListeners();

    if (this.isRecordingHistory) {
      this.addLocationToHistory(locationData);
    }
  }

  private async uploadLocationToServer(
    locationData: LocationData,
  ): Promise<void> {
    try {
      // Use the real API endpoint for location updates
      await apiClient.post("/locations", locationData);
      console.log("Location uploaded successfully");
    } catch (error) {
      console.error("Failed to upload location:", error);
    }
  }

  private getCurrentUserId(): string | null {
    const userInfo = localStorage.getItem("userInfo");
    if (!userInfo) return null;

    try {
      const parsed = JSON.parse(userInfo);
      return parsed.username || parsed.userId || null;
    } catch (e) {
      return null;
    }
  }

  private getCurrentVehicleId(): number | null {
    const vehicleId = localStorage.getItem("currentVehicleId");

    if (!vehicleId) {
      // If no vehicle is selected, try to get the first vehicle from cache
      return 1; // Default to ID 1 for demo purposes
    }

    return vehicleId ? parseInt(vehicleId) : null;
  }

  public setCurrentVehicle(vehicleId: number): void {
    localStorage.setItem("currentVehicleId", vehicleId.toString());
    toast.success(`Now tracking vehicle #${vehicleId}`);
  }

  public async getFamilyLocations(): Promise<Map<string, LocationData>> {
    try {
      // Use the real API to get family locations
      const response = await apiClient.get("/users/locations");
      const locations = response.data;

      const familyLocations = new Map<string, LocationData>();

      // Process the API response
      if (Array.isArray(locations)) {
        locations.forEach((location) => {
          familyLocations.set(location.userId, location);
        });
      }

      // If we're in mock mode or no locations were found, add mock data
      if (this.mockMode || familyLocations.size === 0) {
        const mockedLocations = this.mockFamilyLocations();
        mockedLocations.forEach((location) => {
          familyLocations.set(location.userId, location);
        });
      }

      // Update cache
      for (const [userId, locationData] of familyLocations.entries()) {
        this.locationCache.set(userId, locationData);
      }

      // Save to storage
      this.saveToStorage();

      // Notify listeners
      this.notifyListeners();

      return familyLocations;
    } catch (error) {
      console.error("Failed to fetch family locations:", error);

      // If API fails, use mock data
      if (this.mockMode || this.locationCache.size === 0) {
        const mockedLocations = this.mockFamilyLocations();
        mockedLocations.forEach((location) => {
          this.locationCache.set(location.userId, location);
        });
        this.notifyListeners();
      }

      return this.locationCache;
    }
  }

  public async getVehicleLocation(
    vehicleId: number,
  ): Promise<LocationData | undefined> {
    try {
      const response = await apiClient.get(`/locations/vehicle/${vehicleId}`);
      return response.data;
    } catch (error) {
      console.error(`Failed to get location for vehicle ${vehicleId}:`, error);
      return undefined;
    }
  }

  public addListener(
    callback: (locations: Map<string, LocationData>) => void,
  ): void {
    this.listeners.add(callback);
  }

  public removeListener(
    callback: (locations: Map<string, LocationData>) => void,
  ): void {
    this.listeners.delete(callback);
  }

  private notifyListeners(): void {
    this.listeners.forEach((listener) => {
      listener(this.locationCache);
    });
  }

  private saveToStorage(): void {
    const locationData = Array.from(this.locationCache.entries());
    localStorage.setItem(this.USER_LOCATION_KEY, JSON.stringify(locationData));
  }

  private loadFromStorage(): void {
    const data = localStorage.getItem(this.USER_LOCATION_KEY);
    if (!data) return;

    try {
      const locationData = JSON.parse(data) as [string, LocationData][];
      this.locationCache = new Map(locationData);
    } catch (e) {
      console.error("Failed to load location data from storage:", e);
    }
  }

  public mockFamilyLocations(): LocationData[] {
    // Base location (NYC by default)
    const baseLat = 40.7128;
    const baseLng = -74.006;

    // Create mock locations for family members
    const mockLocations: LocationData[] = [
      {
        userId: "parent",
        vehicleId: 1,
        latitude: baseLat + (Math.random() - 0.5) * 0.03,
        longitude: baseLng + (Math.random() - 0.5) * 0.03,
        speed: Math.random() * 10, // Stationary or slow
        heading: Math.random() * 360,
        timestamp: new Date().toISOString(),
      },
      {
        userId: "teenager",
        vehicleId: 2,
        latitude: baseLat + (Math.random() - 0.5) * 0.05,
        longitude: baseLng + (Math.random() - 0.5) * 0.05,
        speed: 35 + Math.random() * 15, // Moving at higher speed
        heading: 90,
        timestamp: new Date().toISOString(),
      },
      {
        userId: "spouse",
        vehicleId: 3,
        latitude: baseLat + (Math.random() - 0.5) * 0.02,
        longitude: baseLng + (Math.random() - 0.5) * 0.02,
        speed: 15 + Math.random() * 25,
        heading: 180,
        timestamp: new Date().toISOString(),
      },
    ];

    // Get any existing locations from cache to maintain continuity
    mockLocations.forEach((location) => {
      const existing = this.locationCache.get(location.userId);
      if (existing) {
        // Make small changes to existing location for realism
        location.latitude = existing.latitude + (Math.random() - 0.5) * 0.002;
        location.longitude = existing.longitude + (Math.random() - 0.5) * 0.002;
      }
    });

    return mockLocations;
  }

  public isTrackingEnabled(): boolean {
    return this.isTracking;
  }

  public getUserLocation(userId: string): LocationData | undefined {
    return this.locationCache.get(userId);
  }
}

export default LocationService;
