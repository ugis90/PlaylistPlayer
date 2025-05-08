// src/components/VehicleList.tsx
import React, { useState, useEffect, useCallback, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import {
  Car,
  Edit,
  Trash2,
  Plus,
  FileText,
  Search,
  Droplet,
  Wrench,
  ChevronLeft,
  ChevronRight,
  RefreshCw,
  Loader,
} from "lucide-react";
import apiClient from "../api/client";
import { Vehicle } from "../types"; // Assuming Vehicle type is defined correctly
import { useAuth } from "../auth/AuthContext";
import {
  useQuery,
  useMutation,
  useQueryClient,
  QueryKey,
} from "@tanstack/react-query";

interface PaginationInfo {
  currentPage: number;
  pageSize: number;
  totalPages: number;
  totalCount: number;
  hasPrevious: boolean;
  hasNext: boolean;
}
interface VehicleApiResponse {
  resource: Array<{ resource: Vehicle; links: any[] }>;
  links: any[];
}
// Define the structure of the data returned by useQuery's `data` property
interface VehicleQueryResponseData {
  vehicles: Vehicle[];
  pagination: PaginationInfo;
}

const VehicleList = () => {
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearchTerm, setDebouncedSearchTerm] = useState(searchTerm);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [currentVehicle, setCurrentVehicle] = useState<Vehicle | null>(null);
  const navigate = useNavigate();
  const { hasRole } = useAuth();
  const queryClient = useQueryClient();

  const [pagination, setPagination] = useState<PaginationInfo>({
    currentPage: 1,
    pageSize: 8,
    totalPages: 1,
    totalCount: 0,
    hasPrevious: false,
    hasNext: false,
  });

  // Debounce search term and reset page
  useEffect(() => {
    const handler = setTimeout(() => {
      if (debouncedSearchTerm !== searchTerm) {
        console.log("Search term changed, resetting page to 1");
        setPagination((prev) => ({ ...prev, currentPage: 1 }));
      }
      setDebouncedSearchTerm(searchTerm);
    }, 500);
    return () => clearTimeout(handler);
  }, [searchTerm, debouncedSearchTerm]);

  // --- Data Fetching with React Query ---
  const queryKey = useMemo(
    () =>
      [
        "vehicles",
        pagination.currentPage,
        pagination.pageSize,
        debouncedSearchTerm,
      ] as QueryKey,
    [pagination.currentPage, pagination.pageSize, debouncedSearchTerm],
  );

  const {
    data: queryResult, // This object will contain { vehicles: [], pagination: {} } upon success
    isLoading, // Use isLoading for the initial fetch state
    error,
    isFetching, // Use isFetching for background refetches/pagination fetches
    refetch,
    // isPreviousData, // Deprecated
  } = useQuery<VehicleQueryResponseData, Error>({
    // Use the defined interface here
    queryKey: queryKey,
    queryFn: async ({ queryKey }): Promise<VehicleQueryResponseData> => {
      // *** FIX: Destructure queryKey correctly, mark unused with _ ***
      const [_key, page, size, search] = queryKey as [
        string,
        number,
        number,
        string,
      ];
      console.log(
        `Fetching vehicles page ${page}, size ${size}, search: '${search}'...`,
      );
      const url = `/vehicles?pageNumber=${page}&pageSize=${size}${search ? `&searchTerm=${encodeURIComponent(search)}` : ""}`;
      const response = await apiClient.get<VehicleApiResponse>(url);

      let fetchedPaginationInfo: PaginationInfo = {
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
          fetchedPaginationInfo = {
            currentPage: headerData?.currentPage ?? page,
            pageSize: headerData?.pageSize ?? size,
            totalPages: headerData?.totalPages ?? 1,
            totalCount: headerData?.totalCount ?? 0,
            hasPrevious: (headerData?.currentPage ?? 1) > 1,
            hasNext:
              (headerData?.currentPage ?? 1) < (headerData?.totalPages ?? 1),
          };
        } catch (e) {
          console.error("Error parsing pagination header:", e);
        }
      } else {
        console.warn("Pagination header not found in response.");
      }

      const vehicleData: Vehicle[] =
        response.data?.resource
          ?.map((item: any) => item.resource)
          .filter(Boolean) ?? [];
      const validVehicles = vehicleData.filter(
        (v) => v && v.id && v.make !== undefined && v.model !== undefined,
      );

      console.log("Parsed vehicles:", validVehicles);
      console.log("Fetched Pagination Info:", fetchedPaginationInfo);

      // Update local pagination state *after* fetch is successful
      setPagination(fetchedPaginationInfo);

      return { vehicles: validVehicles, pagination: fetchedPaginationInfo };
    },
    staleTime: 60 * 1000,
    // *** FIX: Use gcTime instead of cacheTime in v5 ***
    gcTime: 5 * 60 * 1000,
    // *** FIX: Remove keepPreviousData or use placeholderData if needed ***
    // keepPreviousData: true, // Deprecated
  });

  // *** FIX: Access data correctly from queryResult.data ***
  const vehicles = queryResult?.vehicles ?? [];
  // Use the local pagination state for UI controls and display text
  const currentPagination = pagination;

  // --- Mutations ---
  const createVehicleMutation = useMutation({
    mutationFn: async (vehicleData: Omit<Vehicle, "id">) =>
      apiClient.post("/vehicles", vehicleData),
    onSuccess: () => {
      toast.success("Vehicle created successfully");
      queryClient.invalidateQueries({ queryKey: ["vehicles"] });
      setIsModalOpen(false);
    },
    onError: (error: any) => {
      console.error("Error creating vehicle:", error);
      if (error.response?.status === 403) {
        toast.error("Permission Denied: You cannot create vehicles.");
      } else if (error.response?.data?.errors) {
        const errors = error.response.data.errors;
        const firstErrorKey = Object.keys(errors)[0];
        const firstErrorMessage =
          errors[firstErrorKey]?.[0] || "Validation failed.";
        toast.error(`Validation Error: ${firstErrorMessage}`);
      } else {
        toast.error(
          error.response?.data?.detail || "Failed to create vehicle.",
        );
      }
    },
  });

  const updateVehicleMutation = useMutation({
    mutationFn: async ({
      id,
      data,
    }: {
      id: number;
      data: Partial<Vehicle>;
    }) => {
      const updatePayload = {
        make: data.make,
        model: data.model,
        year: data.year,
        licensePlate: data.licensePlate,
        description: data.description,
        currentMileage: data.currentMileage,
      };
      return apiClient.put(`/vehicles/${id}`, updatePayload);
    },
    onSuccess: () => {
      toast.success("Vehicle updated successfully");
      queryClient.invalidateQueries({ queryKey: queryKey });
      setIsModalOpen(false);
    },
    onError: (error: any) => {
      console.error("Error updating vehicle:", error);
      if (error.response?.status === 403) {
        toast.error("Permission Denied: You cannot update this vehicle.");
      } else if (error.response?.data?.errors) {
        const errors = error.response.data.errors;
        const firstErrorKey = Object.keys(errors)[0];
        const firstErrorMessage =
          errors[firstErrorKey]?.[0] || "Validation failed.";
        toast.error(`Validation Error: ${firstErrorMessage}`);
      } else {
        toast.error(
          error.response?.data?.detail || "Failed to update vehicle.",
        );
      }
    },
  });

  const deleteVehicleMutation = useMutation({
    mutationFn: (id: number) => apiClient.delete(`/vehicles/${id}`),
    onSuccess: () => {
      toast.success("Vehicle deleted successfully");
      if (vehicles.length === 1 && pagination.currentPage > 1) {
        handlePageChange(pagination.currentPage - 1);
      } else {
        queryClient.invalidateQueries({ queryKey: queryKey });
      }
    },
    onError: (error: any) => {
      console.error("Error deleting vehicle:", error);
      if (error.response?.status === 403) {
        toast.error("Permission Denied: You cannot delete this vehicle.");
      } else {
        toast.error(
          error.response?.data?.detail || "Failed to delete vehicle.",
        );
      }
    },
  });

  // --- Event Handlers ---
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

  const handleCreateVehicle = (formData: FormData) => {
    const vehicleData = {
      make: formData.get("make") as string,
      model: formData.get("model") as string,
      year: parseInt(formData.get("year") as string),
      licensePlate: formData.get("licensePlate") as string,
      description: formData.get("description") as string,
      currentMileage: parseInt(
        (formData.get("currentMileage") as string) || "0",
      ),
    };
    if (
      !vehicleData.make ||
      !vehicleData.model ||
      !vehicleData.year ||
      !vehicleData.licensePlate ||
      !vehicleData.description
    ) {
      toast.error("Please fill in all required fields.");
      return;
    }
    if (
      isNaN(vehicleData.year) ||
      vehicleData.year < 1900 ||
      vehicleData.year > 2100
    ) {
      toast.error("Please enter a valid year.");
      return;
    }
    if (isNaN(vehicleData.currentMileage) || vehicleData.currentMileage < 0) {
      toast.error("Please enter a valid mileage (0 or greater).");
      return;
    }
    createVehicleMutation.mutate(vehicleData);
  };

  const handleUpdateVehicle = (id: number, formData: FormData) => {
    const vehicleData = {
      make: formData.get("make") as string,
      model: formData.get("model") as string,
      year: parseInt(formData.get("year") as string),
      licensePlate: formData.get("licensePlate") as string,
      description: formData.get("description") as string,
      currentMileage: parseInt(
        (formData.get("currentMileage") as string) || "0",
      ),
    };
    if (
      !vehicleData.make ||
      !vehicleData.model ||
      !vehicleData.year ||
      !vehicleData.licensePlate ||
      !vehicleData.description
    ) {
      toast.error("Please fill in all required fields.");
      return;
    }
    if (
      isNaN(vehicleData.year) ||
      vehicleData.year < 1900 ||
      vehicleData.year > 2100
    ) {
      toast.error("Please enter a valid year.");
      return;
    }
    if (isNaN(vehicleData.currentMileage) || vehicleData.currentMileage < 0) {
      toast.error("Please enter a valid mileage (0 or greater).");
      return;
    }
    updateVehicleMutation.mutate({ id, data: vehicleData });
  };

  const handleDeleteVehicle = (id: number) => {
    if (window.confirm("Are you sure you want to delete this vehicle?")) {
      deleteVehicleMutation.mutate(id);
    }
  };

  const handleFormSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    if (currentVehicle) {
      handleUpdateVehicle(currentVehicle.id, formData);
    } else {
      handleCreateVehicle(formData);
    }
  };

  // Navigation functions
  const navigateToTrips = (vehicleId: number) =>
    navigate(`/vehicles/${vehicleId}/trips`);
  const navigateToFuelRecords = (vehicleId: number) =>
    navigate(`/vehicles/${vehicleId}/fuel`);
  const navigateToMaintenance = (vehicleId: number) =>
    navigate(`/vehicles/${vehicleId}/maintenance`);

  // Client-side filtering (REMOVE if backend handles search)
  const displayVehicles = useMemo(() => {
    if (!debouncedSearchTerm) return vehicles;
    return vehicles.filter(
      (
        vehicle: Vehicle, // Add explicit type
      ) =>
        vehicle.make
          ?.toLowerCase()
          .includes(debouncedSearchTerm.toLowerCase()) ||
        vehicle.model
          ?.toLowerCase()
          .includes(debouncedSearchTerm.toLowerCase()) ||
        vehicle.licensePlate
          ?.toLowerCase()
          .includes(debouncedSearchTerm.toLowerCase()) ||
        vehicle.description
          ?.toLowerCase()
          .includes(debouncedSearchTerm.toLowerCase()),
    );
  }, [vehicles, debouncedSearchTerm]);

  // Permissions
  const canManageVehicles = hasRole([
    "Admin",
    "Parent",
    "FleetUser",
    "Teenager",
  ]);
  const canEditDelete = hasRole(["Admin", "Parent"]);

  // *** FIX: Use isLoading for initial load check ***
  const showLoading = isLoading && !queryResult; // Show loader only on initial load without data

  return (
    <div className="container mx-auto px-4 py-8">
      {/* Header */}
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl font-bold">Vehicles</h1>
        <div className="flex space-x-2">
          <button
            onClick={() => refetch()}
            className="flex items-center bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-lg"
            disabled={isFetching}
          >
            <RefreshCw
              className={`h-5 w-5 mr-2 ${isFetching ? "animate-spin" : ""}`}
            />{" "}
            Refresh
          </button>
          {canManageVehicles && (
            <button
              onClick={() => {
                setCurrentVehicle(null);
                setIsModalOpen(true);
              }}
              className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg flex items-center"
            >
              {" "}
              <Plus className="h-5 w-5 mr-2" /> Add Vehicle{" "}
            </button>
          )}
        </div>
      </div>

      {/* Search */}
      <div className="bg-white rounded-lg shadow-md mb-8">
        <div className="p-4 border-b">
          <div className="relative flex-grow max-w-md">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <Search className="h-5 w-5 text-gray-400" />
            </div>
            <input
              type="text"
              placeholder="Search vehicles..."
              className="pl-10 pr-4 py-2 border rounded-lg w-full focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>
        </div>

        {/* Vehicle List Table */}
        {/* *** FIX: Use isLoading for initial load check *** */}
        {showLoading ? (
          <div className="p-8 text-center">
            <Loader className="h-8 w-8 animate-spin text-blue-600 mx-auto" />
            <p className="mt-2 text-gray-500">Loading vehicles...</p>
          </div>
        ) : error ? (
          <div className="p-8 text-center text-red-600">
            {" "}
            Error loading vehicles: {error.message}{" "}
          </div>
        ) : // *** FIX: Check vehicles length from queryResult for initial empty state ***
        queryResult?.vehicles.length === 0 && !debouncedSearchTerm ? (
          <div className="p-8 text-center">
            <p className="text-gray-500 mb-4">
              No vehicles found. Add a vehicle to get started.
            </p>
            {canManageVehicles && (
              <button
                onClick={() => {
                  setCurrentVehicle(null);
                  setIsModalOpen(true);
                }}
                className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg inline-flex items-center"
              >
                <Plus className="h-5 w-5 mr-2" /> Add First Vehicle
              </button>
            )}
          </div>
        ) : // *** FIX: Use displayVehicles for search empty state ***
        displayVehicles.length === 0 && debouncedSearchTerm ? (
          <div className="p-8 text-center text-gray-500">
            {" "}
            No vehicles match your search term "{debouncedSearchTerm}".{" "}
          </div>
        ) : (
          <div
            className={`overflow-x-auto ${isFetching ? "opacity-75 transition-opacity duration-300" : "opacity-100"}`}
          >
            <table className="w-full table-auto">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Vehicle
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    License Plate
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Mileage
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Description
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {/* *** FIX: Map over displayVehicles *** */}
                {displayVehicles.map(
                  (
                    vehicle: Vehicle, // Add explicit type
                  ) => (
                    <tr key={vehicle.id} className="hover:bg-gray-50">
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="flex items-center">
                          <div className="flex-shrink-0 h-10 w-10 bg-blue-100 rounded-full flex items-center justify-center">
                            <Car className="h-6 w-6 text-blue-600" />
                          </div>
                          <div className="ml-4">
                            <div className="text-sm font-medium text-gray-900">
                              {vehicle.make} {vehicle.model}
                            </div>
                            <div className="text-sm text-gray-500">
                              {vehicle.year}
                            </div>
                          </div>
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="text-sm text-gray-900">
                          {vehicle.licensePlate}
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="text-sm text-gray-900">
                          {vehicle.currentMileage?.toLocaleString() || 0} miles
                        </div>
                      </td>
                      <td className="px-6 py-4">
                        <div
                          className="text-sm text-gray-900 max-w-xs truncate"
                          title={vehicle.description}
                        >
                          {vehicle.description || "No description"}
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-medium">
                        <div className="flex space-x-1">
                          <button
                            onClick={() => navigateToTrips(vehicle.id)}
                            className="text-blue-600 hover:text-blue-900 p-1"
                            title="View Trips"
                          >
                            <FileText className="h-5 w-5" />
                          </button>
                          <button
                            onClick={() => navigateToFuelRecords(vehicle.id)}
                            className="text-green-600 hover:text-green-900 p-1"
                            title="View Fuel Records"
                          >
                            <Droplet className="h-5 w-5" />
                          </button>
                          <button
                            onClick={() => navigateToMaintenance(vehicle.id)}
                            className="text-orange-600 hover:text-orange-900 p-1"
                            title="View Maintenance"
                          >
                            <Wrench className="h-5 w-5" />
                          </button>
                          {canEditDelete && (
                            <>
                              <button
                                onClick={() => {
                                  setCurrentVehicle(vehicle);
                                  setIsModalOpen(true);
                                }}
                                className="text-indigo-600 hover:text-indigo-900 p-1"
                                title="Edit"
                              >
                                <Edit className="h-5 w-5" />
                              </button>
                              <button
                                onClick={() => handleDeleteVehicle(vehicle.id)}
                                className="text-red-600 hover:text-red-900 p-1"
                                title="Delete"
                                disabled={deleteVehicleMutation.isPending}
                              >
                                <Trash2 className="h-5 w-5" />
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ),
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Pagination Controls - Use currentPagination */}
      {/* *** FIX: Use isLoading for initial loading check *** */}
      {currentPagination.totalCount > 0 && !isLoading && (
        <div className="flex justify-between items-center mt-6">
          <div className="text-sm text-gray-600">
            Showing page {currentPagination.currentPage} of{" "}
            {currentPagination.totalPages} ({currentPagination.totalCount} total
            vehicles | {displayVehicles.length} on this page)
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

      {/* Vehicle Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-lg shadow-lg w-full max-w-md p-6 max-h-[90vh] overflow-y-auto">
            <h2 className="text-xl font-bold mb-4">
              {currentVehicle ? "Edit Vehicle" : "Add New Vehicle"}
            </h2>
            <form onSubmit={handleFormSubmit} className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Make <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    name="make"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentVehicle?.make || ""}
                    placeholder="e.g. Toyota"
                    required
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Model <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    name="model"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentVehicle?.model || ""}
                    placeholder="e.g. Camry"
                    required
                  />
                </div>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Year <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="number"
                    name="year"
                    className="w-full border rounded-lg p-2"
                    defaultValue={
                      currentVehicle?.year || new Date().getFullYear()
                    }
                    placeholder="e.g. 2023"
                    required
                    min="1900"
                    max="2100"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    License Plate <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    name="licensePlate"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentVehicle?.licensePlate || ""}
                    placeholder="e.g. ABC123"
                    required
                  />
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Current Mileage
                </label>
                <input
                  type="number"
                  name="currentMileage"
                  className="w-full border rounded-lg p-2"
                  defaultValue={currentVehicle?.currentMileage || 0}
                  placeholder="e.g. 15000"
                  min="0"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Description <span className="text-red-500">*</span>
                </label>
                <textarea
                  name="description"
                  className="w-full border rounded-lg p-2"
                  defaultValue={currentVehicle?.description || ""}
                  placeholder="Brief description of the vehicle"
                  rows={3}
                  required
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
                    createVehicleMutation.isPending ||
                    updateVehicleMutation.isPending
                  }
                >
                  {(createVehicleMutation.isPending ||
                    updateVehicleMutation.isPending) && (
                    <Loader className="animate-spin h-4 w-4 mr-2" />
                  )}
                  {currentVehicle ? "Save Changes" : "Add Vehicle"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default VehicleList;
