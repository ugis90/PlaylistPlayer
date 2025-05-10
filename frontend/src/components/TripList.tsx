// src/components/TripList.tsx
import React, { useState, useEffect, useMemo } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { toast } from "sonner";
import {
  MapPin,
  Clock,
  Route,
  ArrowRight,
  Plus,
  Edit,
  Trash2,
  ChevronLeft,
  Search,
  Droplet,
  ChevronRight,
  RefreshCw,
  Loader,
  Map, // Wrench removed
} from "lucide-react";
import apiClient from "../api/client";
import { Trip, Vehicle } from "../types";
import {
  useQuery,
  useMutation,
  useQueryClient,
  QueryKey,
} from "@tanstack/react-query";

// Define Pagination type
interface PaginationInfo {
  currentPage: number;
  pageSize: number;
  totalPages: number;
  totalCount: number;
  hasPrevious: boolean;
  hasNext: boolean;
}
// Define API response structure
interface TripApiResponse {
  resource: Array<{ resource: Trip; links: any[] }>;
  links: any[];
}
// Define Query Data structure
interface TripQueryData {
  trips: Trip[];
  pagination: PaginationInfo;
}

interface UpdateTripDto {
  distance?: number | null;
  purpose?: string | null;
  fuelUsed?: number | null;
}

const TripList = () => {
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearchTerm, setDebouncedSearchTerm] = useState(searchTerm);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [currentTrip, setCurrentTrip] = useState<Trip | null>(null);
  const [vehicle, setVehicle] = useState<Vehicle | null>(null);
  const navigate = useNavigate();
  const { vehicleId } = useParams<{ vehicleId: string }>();
  const queryClient = useQueryClient();

  const [pagination, setPagination] = useState<PaginationInfo>({
    currentPage: 1,
    pageSize: 10,
    totalPages: 1,
    totalCount: 0,
    hasPrevious: false,
    hasNext: false,
  });

  // Debounce search term and reset page
  useEffect(() => {
    const handler = setTimeout(() => {
      if (debouncedSearchTerm !== searchTerm) {
        setPagination((prev) => ({ ...prev, currentPage: 1 }));
      }
      setDebouncedSearchTerm(searchTerm);
    }, 500);
    return () => clearTimeout(handler);
  }, [searchTerm, debouncedSearchTerm]);

  useEffect(() => {
    if (vehicleId) {
      fetchVehicleDetails();
    } else {
      toast.error("Vehicle ID missing!");
      navigate("/vehicles");
    }
  }, [vehicleId, navigate]);

  const fetchVehicleDetails = async () => {
    if (!vehicleId) return;
    try {
      const response = await apiClient.get(`/vehicles/${vehicleId}`);
      if (
        response.data?.resource &&
        typeof response.data.resource === "object"
      ) {
        setVehicle(response.data.resource);
      } else if (typeof response.data === "object") {
        setVehicle(response.data);
      } else {
        toast.error("Could not load vehicle details.");
      }
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
    } catch (error) {
      toast.error("Failed to load vehicle details");
    }
  };

  // --- Data Fetching with React Query ---
  const queryKey = useMemo(
    () =>
      [
        "trips",
        vehicleId,
        pagination.currentPage,
        pagination.pageSize,
        debouncedSearchTerm,
      ] as QueryKey,
    [
      vehicleId,
      pagination.currentPage,
      pagination.pageSize,
      debouncedSearchTerm,
    ],
  );

  const {
    data: queryResult,
    isLoading,
    error,
    isFetching,
    refetch,
  } = useQuery<TripQueryData, Error>({
    queryKey: queryKey,
    queryFn: async ({ queryKey }): Promise<TripQueryData> => {
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      const [_key, vId, page, size, search] = queryKey as [
        string,
        string,
        number,
        number,
        string,
      ];
      if (!vId) throw new Error("Vehicle ID is required");
      console.log(
        `Fetching trips for vehicle ${vId}, page ${page}, search: '${search}'...`,
      );
      const url = `/vehicles/${vId}/trips?pageNumber=${page}&pageSize=${size}${search ? `&searchTerm=${encodeURIComponent(search)}` : ""}`;
      const response = await apiClient.get<TripApiResponse>(url);

      let paginationInfo: PaginationInfo = {
        currentPage: page,
        pageSize: size,
        totalPages: 1,
        totalCount: 0,
        hasPrevious: false,
        hasNext: false,
      };
      const paginationHeader = response.headers["pagination"];
      if (paginationHeader) {
        try {
          const headerData = JSON.parse(paginationHeader);
          paginationInfo = {
            currentPage: headerData?.currentPage ?? page,
            pageSize: headerData?.pageSize ?? size,
            totalPages: headerData?.totalPages ?? 1,
            totalCount: headerData?.totalCount ?? 0,
            hasPrevious: headerData?.currentPage > 1,
            hasNext: headerData?.currentPage < (headerData?.totalPages ?? 1),
          };
        } catch (e) {
          console.error("Error parsing pagination header:", e);
        }
      } else {
        console.warn("Pagination header not found.");
      }

      const tripData: Trip[] =
        response.data?.resource
          ?.map((item: any) => item.resource)
          .filter(Boolean) ?? [];
      const validTrips = tripData.filter(
        (t) =>
          t &&
          t.id &&
          t.startLocation &&
          t.endLocation &&
          t.startTime &&
          t.endTime,
      );

      console.log("Parsed trips:", validTrips);
      console.log("Calculated Pagination:", paginationInfo);
      setPagination(paginationInfo);

      return { trips: validTrips, pagination: paginationInfo };
    },
    enabled: !!vehicleId,
    staleTime: 1 * 60 * 1000,
    gcTime: 5 * 60 * 1000, // Use gcTime
    // keepPreviousData: true, // Removed
  });

  const trips = queryResult?.trips ?? [];
  const currentPagination = pagination;

  // --- Mutations ---
  const createTripMutation = useMutation({
    mutationFn: async (tripData: Omit<Trip, "id" | "vehicleId">) => {
      if (!vehicleId) throw new Error("Cannot create trip without vehicleId");
      return apiClient.post(`/vehicles/${vehicleId}/trips`, tripData);
    },
    onSuccess: () => {
      toast.success("Trip created successfully");
      queryClient.invalidateQueries({ queryKey: ["trips", vehicleId] });
      setIsModalOpen(false);
    },
    onError: (error: any) => {
      console.error("Error creating trip:", error);
      if (error.response?.status === 403) {
        const detail =
          error.response?.data?.detail ||
          error.response?.data ||
          "Permission Denied.";
        toast.error(detail);
      } else {
        toast.error(error.response?.data?.detail || "Failed to create trip.");
      }
    },
  });

  const updateTripMutation = useMutation({
    mutationFn: async ({
      tripId,
      data,
    }: {
      tripId: number;
      data: Partial<Trip>;
    }) => {
      if (!vehicleId) throw new Error("Cannot update trip without vehicleId");
      const updatePayload: Partial<UpdateTripDto> = {
        distance: data.distance,
        purpose: data.purpose,
        fuelUsed: data.fuelUsed,
      };
      if (
        updatePayload.distance !== undefined &&
        updatePayload.distance !== null &&
        isNaN(Number(updatePayload.distance))
      )
        updatePayload.distance = null;
      if (
        updatePayload.fuelUsed !== undefined &&
        updatePayload.fuelUsed !== null &&
        isNaN(Number(updatePayload.fuelUsed))
      )
        updatePayload.fuelUsed = null;
      console.log(
        `API Request: PUT /vehicles/${vehicleId}/trips/${tripId}`,
        updatePayload,
      );
      return apiClient.put(
        `/vehicles/${vehicleId}/trips/${tripId}`,
        updatePayload,
      );
    },
    onSuccess: () => {
      toast.success("Trip updated successfully");
      queryClient.invalidateQueries({ queryKey: queryKey });
      setIsModalOpen(false);
    },
    onError: (error: any) => {
      console.error("Error updating trip:", error);
      if (error.response?.status === 403) {
        toast.error("Permission Denied.");
      } else {
        toast.error(error.response?.data?.detail || "Failed to update trip.");
      }
    },
  });

  const deleteTripMutation = useMutation({
    mutationFn: (tripId: number) => {
      if (!vehicleId) throw new Error("Cannot delete trip without vehicleId");
      return apiClient.delete(`/vehicles/${vehicleId}/trips/${tripId}`);
    },
    onSuccess: () => {
      toast.success("Trip deleted successfully");
      if (trips.length === 1 && pagination.currentPage > 1) {
        handlePageChange(pagination.currentPage - 1);
      } else {
        queryClient.invalidateQueries({ queryKey: queryKey });
      }
    },
    onError: (error: any) => {
      console.error("Error deleting trip:", error);
      if (error.response?.status === 403) {
        toast.error("Permission Denied.");
      } else {
        toast.error(error.response?.data?.detail || "Failed to delete trip.");
      }
    },
  });

  // --- Event Handlers & Helpers ---
  const handlePageChange = (newPage: number) => {
    if (
      newPage >= 1 &&
      newPage <= currentPagination.totalPages &&
      newPage !== pagination.currentPage &&
      !isFetching
    ) {
      setPagination((prev) => ({ ...prev, currentPage: newPage }));
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
  };

  const handleCreateTrip = (formData: FormData) => {
    const startTime = formData.get("startTime") as string;
    const endTime = formData.get("endTime") as string;
    const distanceStr = formData.get("distance") as string;
    const fuelUsedStr = formData.get("fuelUsed") as string;
    if (!startTime || !endTime || !distanceStr) {
      toast.error("Start/End Time and Distance are required.");
      return;
    }
    if (new Date(endTime) <= new Date(startTime)) {
      toast.error("End time must be after start time.");
      return;
    }
    if (parseFloat(distanceStr) < 0) {
      toast.error("Distance cannot be negative.");
      return;
    }
    if (fuelUsedStr && parseFloat(fuelUsedStr) < 0) {
      toast.error("Fuel used cannot be negative.");
      return;
    }
    const tripData = {
      startLocation: formData.get("startLocation") as string,
      endLocation: formData.get("endLocation") as string,
      distance: parseFloat(distanceStr),
      startTime: new Date(startTime).toISOString(),
      endTime: new Date(endTime).toISOString(),
      purpose: (formData.get("purpose") as string) || "",
      fuelUsed: fuelUsedStr ? parseFloat(fuelUsedStr) : null,
    };
    createTripMutation.mutate(tripData);
  };

  const handleUpdateTrip = (tripId: number, formData: FormData) => {
    const distanceStr = formData.get("distance") as string;
    const fuelUsedStr = formData.get("fuelUsed") as string;
    const purpose = formData.get("purpose") as string;
    const startTime = formData.get("startTime") as string;
    const endTime = formData.get("endTime") as string;
    if (!startTime || !endTime || !distanceStr) {
      toast.error("Start/End Time and Distance are required.");
      return;
    }
    if (new Date(endTime) <= new Date(startTime)) {
      toast.error("End time must be after start time.");
      return;
    }
    if (parseFloat(distanceStr) < 0) {
      toast.error("Distance cannot be negative.");
      return;
    }
    if (fuelUsedStr && parseFloat(fuelUsedStr) < 0) {
      toast.error("Fuel used cannot be negative.");
      return;
    }
    const tripData: Partial<Trip> = {
      startLocation: formData.get("startLocation") as string,
      endLocation: formData.get("endLocation") as string,
      startTime: new Date(startTime).toISOString(),
      endTime: new Date(endTime).toISOString(),
      distance: distanceStr ? parseFloat(distanceStr) : undefined,
      purpose: purpose,
      fuelUsed: fuelUsedStr ? parseFloat(fuelUsedStr) : null,
    };
    updateTripMutation.mutate({ tripId, data: tripData });
  };

  const handleDeleteTrip = (tripId: number) => {
    if (window.confirm("Are you sure you want to delete this trip?")) {
      deleteTripMutation.mutate(tripId);
    }
  };

  const handleFormSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    if (currentTrip) {
      handleUpdateTrip(currentTrip.id, formData);
    } else {
      handleCreateTrip(formData);
    }
  };

  const goBack = () => navigate("/vehicles");
  const navigateToGpsTracking = () => navigate("/tracking");

  // Formatting functions
  const formatDate = (dateString: string | undefined) => {
    if (!dateString) return "N/A";
    try {
      return new Date(dateString).toLocaleString("en-US", {
        dateStyle: "medium",
        timeStyle: "short",
      });
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
    } catch (error) {
      return "Invalid date";
    }
  };
  const calculateDuration = (startTime?: string, endTime?: string) => {
    if (!startTime || !endTime) return "N/A";
    try {
      const start = new Date(startTime);
      const end = new Date(endTime);
      const diffMs = end.getTime() - start.getTime();
      if (diffMs < 0) return "Invalid";
      const diffMins = Math.floor(diffMs / 60000);
      const hours = Math.floor(diffMins / 60);
      const mins = diffMins % 60;
      return hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
    } catch (error) {
      return "Error";
    }
  };
  const formatDateTimeForInput = (dateString: string | undefined) => {
    if (!dateString) return "";
    try {
      const date = new Date(dateString);
      return date.toISOString().slice(0, 16);
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
    } catch (error) {
      return "";
    }
  };

  // Client-side filtering (REMOVE if backend handles search)
  const displayTrips = useMemo(() => {
    if (!debouncedSearchTerm) return trips;
    return trips.filter(
      (trip: Trip) =>
        (trip?.startLocation?.toLowerCase() || "").includes(
          debouncedSearchTerm.toLowerCase(),
        ) ||
        (trip?.endLocation?.toLowerCase() || "").includes(
          debouncedSearchTerm.toLowerCase(),
        ) ||
        (trip?.purpose?.toLowerCase() || "").includes(
          debouncedSearchTerm.toLowerCase(),
        ),
    );
  }, [trips, debouncedSearchTerm]);

  return (
    <div className="container mx-auto px-4 py-8">
      {/* Back Button */}
      <button
        onClick={goBack}
        className="flex items-center text-blue-600 hover:text-blue-800 mb-4"
      >
        <ChevronLeft className="h-5 w-5 mr-1" /> Back to Vehicles
      </button>

      {/* Header */}
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="text-3xl font-bold">Trips</h1>
          {vehicle && (
            <p className="text-gray-600">
              {vehicle.make} {vehicle.model} ({vehicle.year})
            </p>
          )}
        </div>
        <div className="flex space-x-2">
          <button
            onClick={navigateToGpsTracking}
            className="bg-purple-600 hover:bg-purple-700 text-white px-4 py-2 rounded-lg flex items-center"
          >
            <Map className="h-5 w-5 mr-2" /> GPS Tracking
          </button>
          <button
            onClick={() => {
              setCurrentTrip(null);
              setIsModalOpen(true);
            }}
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg flex items-center"
          >
            <Plus className="h-5 w-5 mr-2" /> Add Trip
          </button>
        </div>
      </div>

      {/* Search and Filter Bar */}
      <div className="bg-white rounded-lg shadow-md mb-8">
        <div className="p-4 border-b">
          <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
            <div className="relative flex-grow max-w-md">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                <Search className="h-5 w-5 text-gray-400" />
              </div>
              <input
                type="text"
                placeholder="Search trips (client-side)..."
                className="pl-10 pr-4 py-2 border rounded-lg w-full focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
            <div className="flex space-x-2">
              <button
                onClick={() => refetch()}
                className="flex items-center text-gray-600 px-3 py-2 border rounded-lg hover:bg-gray-50"
                disabled={isFetching}
              >
                <RefreshCw
                  className={`h-5 w-5 mr-2 ${isFetching ? "animate-spin" : ""}`}
                />{" "}
                Refresh
              </button>
              {/* <button className="flex items-center text-gray-600 px-3 py-2 border rounded-lg hover:bg-gray-50"><Filter className="h-5 w-5 mr-2" /> Filter</button> */}
            </div>
          </div>
        </div>

        {/* Trip List */}
        {isLoading && !queryResult ? (
          <div className="p-8 text-center">
            <Loader className="h-8 w-8 animate-spin text-blue-600 mx-auto" />
            <p className="mt-2 text-gray-500">Loading trips...</p>
          </div>
        ) : error ? (
          <div className="p-8 text-center text-red-600">
            {" "}
            Error loading trips: {error.message}{" "}
          </div>
        ) : trips.length === 0 && !debouncedSearchTerm ? (
          <div className="p-8 text-center">
            <p className="text-gray-600 mb-4">
              No trips found for this vehicle.
            </p>
            <div className="flex justify-center space-x-4">
              <button
                onClick={() => {
                  setCurrentTrip(null);
                  setIsModalOpen(true);
                }}
                className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg flex items-center"
              >
                <Plus className="h-5 w-5 mr-2" /> Add First Trip
              </button>
              <button
                onClick={navigateToGpsTracking}
                className="bg-purple-600 hover:bg-purple-700 text-white px-4 py-2 rounded-lg flex items-center"
              >
                <Map className="h-5 w-5 mr-2" /> Use GPS Tracking
              </button>
            </div>
          </div>
        ) : displayTrips.length === 0 && debouncedSearchTerm ? (
          <div className="p-8 text-center text-gray-500">
            {" "}
            No trips match your search term "{debouncedSearchTerm}".{" "}
          </div>
        ) : (
          <div
            className={`divide-y divide-gray-200 ${isFetching ? "opacity-75" : ""}`}
          >
            {displayTrips.map((trip: Trip) => (
              <div key={trip.id} className="p-6 hover:bg-gray-50">
                <div className="flex flex-col md:flex-row md:items-center justify-between">
                  {/* Trip Details */}
                  <div className="flex-grow">
                    <div className="flex items-center mb-2">
                      <span className="text-md font-medium mr-2">
                        {trip.purpose || "No purpose specified"}
                      </span>
                      <span className="text-sm text-gray-600">
                        ({formatDate(trip.startTime)})
                      </span>
                    </div>
                    <div className="flex items-center text-gray-600 mb-2">
                      <MapPin className="h-4 w-4 mr-2 flex-shrink-0" />
                      <span
                        className="truncate"
                        title={`${trip.startLocation} to ${trip.endLocation}`}
                      >
                        {trip.startLocation}
                      </span>
                      <ArrowRight className="h-4 w-4 mx-2 flex-shrink-0" />
                      <span
                        className="truncate"
                        title={`${trip.startLocation} to ${trip.endLocation}`}
                      >
                        {trip.endLocation}
                      </span>
                    </div>
                    <div className="flex flex-wrap gap-x-4 gap-y-1 text-sm text-gray-600">
                      <div className="flex items-center">
                        <Route className="h-4 w-4 mr-1" />
                        {trip.distance?.toFixed(1) || "0"} miles
                      </div>
                      <div className="flex items-center">
                        <Clock className="h-4 w-4 mr-1" />
                        {calculateDuration(trip.startTime, trip.endTime)}
                      </div>
                      {trip.fuelUsed != null && (
                        <div className="flex items-center">
                          <Droplet className="h-4 w-4 mr-1" />
                          {trip.fuelUsed.toFixed(1)} gal
                        </div>
                      )}
                    </div>
                  </div>
                  {/* Action Buttons */}
                  <div className="flex space-x-2 mt-4 md:mt-0 flex-shrink-0 self-start md:self-center">
                    {/* Removed Maintenance Button */}
                    <button
                      onClick={() => {
                        setCurrentTrip(trip);
                        setIsModalOpen(true);
                      }}
                      className="text-indigo-600 hover:text-indigo-800 p-1"
                      title="Edit"
                    >
                      <Edit className="h-5 w-5" />
                    </button>
                    <button
                      onClick={() => handleDeleteTrip(trip.id)}
                      disabled={deleteTripMutation.isPending}
                      className="text-red-600 hover:text-red-800 p-1"
                      title="Delete"
                    >
                      <Trash2 className="h-5 w-5" />
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Pagination Controls */}
      {currentPagination.totalCount > 0 && !isLoading && (
        <div className="flex justify-between items-center mt-6">
          <div className="text-sm text-gray-600">
            Showing page {currentPagination.currentPage} of{" "}
            {currentPagination.totalPages} ({currentPagination.totalCount} total
            trips | {displayTrips.length} on this page)
          </div>
          <div className="flex space-x-2">
            <button
              onClick={() =>
                handlePageChange(currentPagination.currentPage - 1)
              }
              disabled={!currentPagination.hasPrevious || isFetching}
              className={`flex items-center p-2 rounded-md border ${currentPagination.hasPrevious && !isFetching ? "text-blue-600 border-blue-200 hover:bg-blue-50" : "text-gray-400 border-gray-200 cursor-not-allowed"}`}
            >
              <ChevronLeft className="h-5 w-5" /> Previous
            </button>
            <button
              onClick={() =>
                handlePageChange(currentPagination.currentPage + 1)
              }
              disabled={!currentPagination.hasNext || isFetching}
              className={`flex items-center p-2 rounded-md border ${currentPagination.hasNext && !isFetching ? "text-blue-600 border-blue-200 hover:bg-blue-50" : "text-gray-400 border-gray-200 cursor-not-allowed"}`}
            >
              Next <ChevronRight className="h-5 w-5" />
            </button>
          </div>
        </div>
      )}

      {/* Trip Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-lg shadow-lg w-full max-w-lg p-6 max-h-[90vh] overflow-y-auto">
            <h2 className="text-xl font-bold mb-4">
              {currentTrip ? "Edit Trip" : "Add New Trip"}
            </h2>
            <form onSubmit={handleFormSubmit} className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Start Location <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    name="startLocation"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentTrip?.startLocation || ""}
                    placeholder="e.g. Home Address"
                    required
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    End Location <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    name="endLocation"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentTrip?.endLocation || ""}
                    placeholder="e.g. Work Address"
                    required
                  />
                </div>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Start Time <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="datetime-local"
                    name="startTime"
                    className="w-full border rounded-lg p-2"
                    defaultValue={
                      currentTrip?.startTime
                        ? formatDateTimeForInput(currentTrip.startTime)
                        : ""
                    }
                    required
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    End Time <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="datetime-local"
                    name="endTime"
                    className="w-full border rounded-lg p-2"
                    defaultValue={
                      currentTrip?.endTime
                        ? formatDateTimeForInput(currentTrip.endTime)
                        : ""
                    }
                    required
                  />
                </div>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Distance (miles) <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="number"
                    name="distance"
                    step="0.1"
                    min="0"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentTrip?.distance ?? ""}
                    placeholder="e.g. 12.5"
                    required
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Fuel Used (gallons)
                  </label>
                  <input
                    type="number"
                    name="fuelUsed"
                    step="0.01"
                    min="0"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentTrip?.fuelUsed ?? ""}
                    placeholder="Optional, e.g. 0.8"
                  />
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Purpose
                </label>
                <input
                  type="text"
                  name="purpose"
                  className="w-full border rounded-lg p-2"
                  defaultValue={currentTrip?.purpose || ""}
                  placeholder="e.g. Commute, Shopping, Road Trip"
                />
              </div>
              <div className="flex justify-end space-x-3 pt-4">
                <button
                  type="button"
                  className="px-4 py-2 border rounded-lg text-gray-700 hover:bg-gray-50"
                  onClick={() => setIsModalOpen(false)}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 flex items-center"
                  disabled={
                    createTripMutation.isPending || updateTripMutation.isPending
                  }
                >
                  {(createTripMutation.isPending ||
                    updateTripMutation.isPending) && (
                    <Loader className="animate-spin h-4 w-4 mr-2" />
                  )}
                  {currentTrip ? "Save Changes" : "Add Trip"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default TripList;
