// src/components/FuelRecords.tsx
import React, { useState, useEffect, useMemo } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { toast } from "sonner";
import {
  Droplet,
  ChevronLeft,
  Search,
  Plus,
  Edit,
  Trash2,
  Gauge,
  MapPin,
  ArrowUpRight,
  RefreshCw,
  Loader,
  DollarSign,
  ChevronRight, // Added Calendar
} from "lucide-react";
import { apiClient } from "../api/client";
import { FuelRecord, Vehicle } from "../types";
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
interface FuelApiResponse {
  resource: Array<{ resource: FuelRecord; links: any[] }>;
  links: any[];
}
// Define Query Data structure
interface FuelQueryData {
  records: FuelRecord[];
  pagination: PaginationInfo;
}
// Define Fuel Stats type
interface FuelStats {
  avgMpg: number;
  totalGallons: number;
  totalCost: number;
  avgCostPerGallon: number;
}

const FuelRecords = () => {
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearchTerm, setDebouncedSearchTerm] = useState(searchTerm);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [currentRecord, setCurrentRecord] = useState<FuelRecord | null>(null);
  const [vehicle, setVehicle] = useState<Vehicle | null>(null);
  const [fuelStats, setFuelStats] = useState<FuelStats>({
    avgMpg: 0,
    totalGallons: 0,
    totalCost: 0,
    avgCostPerGallon: 0,
  });

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
      // Records fetched by useQuery
    }
  }, [vehicleId]);

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
        console.warn("Unexpected vehicle detail format:", response.data);
        toast.error("Could not load vehicle details.");
      }
    } catch (error) {
      console.error("Error fetching vehicle details:", error);
      toast.error("Failed to load vehicle details");
    }
  };

  // --- Data Fetching with React Query ---
  const queryKey = useMemo(
    () =>
      [
        "fuel",
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
  } = useQuery<FuelQueryData, Error>({
    initialData: undefined,
    queryKey: queryKey,
    queryFn: async (): Promise<FuelQueryData> => {
      if (!vehicleId) throw new Error("Vehicle ID is required");
      console.log(`Fetching fuel records page ${pagination.currentPage}...`);
      const url = `/vehicles/${vehicleId}/fuelRecords?pageNumber=${pagination.currentPage}&pageSize=${pagination.pageSize}`;
      const response = await apiClient.get<FuelApiResponse>(url);

      // --- Extract Pagination ---
      let paginationInfo: PaginationInfo = {
        ...pagination,
        totalCount: 0,
        totalPages: 1,
        hasPrevious: false,
        hasNext: false,
      };
      const paginationHeader = response.headers["pagination"];
      if (paginationHeader) {
        try {
          const headerData = JSON.parse(paginationHeader);
          paginationInfo = {
            currentPage: headerData?.currentPage ?? pagination.currentPage,
            pageSize: headerData?.pageSize ?? pagination.pageSize,
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

      // --- Extract Records ---
      const recordsData: FuelRecord[] =
        response.data?.resource
          ?.map((item: any) => item.resource)
          .filter(Boolean) ?? [];
      const validRecords = recordsData.filter(
        (r) => r && r.id && r.date && r.gallons,
      );

      console.log("Parsed fuel records:", validRecords);
      console.log("Calculated Pagination:", paginationInfo);
      setPagination(paginationInfo);

      // Calculate stats based on fetched records for the current page
      if (validRecords.length > 0) {
        calculateFuelStats(validRecords);
      } else {
        setFuelStats({
          avgMpg: 0,
          totalGallons: 0,
          totalCost: 0,
          avgCostPerGallon: 0,
        });
      }

      return { records: validRecords, pagination: paginationInfo };
    },
    enabled: !!vehicleId,
    staleTime: 1 * 60 * 1000,
    cacheTime: 5 * 60 * 1000,
  });

  const records = queryResult?.records ?? [];
  const currentPagination = pagination;

  // --- Mutations ---
  const createRecordMutation = useMutation({
    mutationFn: async (
      recordData: Omit<FuelRecord, "id" | "vehicleId" | "createdOn">,
    ) => {
      if (!vehicleId) throw new Error("Missing vehicleId");
      console.log(
        `API Request: POST /vehicles/${vehicleId}/fuelRecords`,
        recordData,
      );
      return apiClient.post(`/vehicles/${vehicleId}/fuelRecords`, recordData);
    },
    onSuccess: () => {
      toast.success("Record created");
      queryClient.invalidateQueries({ queryKey: ["fuel", vehicleId] });
      setIsModalOpen(false);
    },
    onError: (error: any) => {
      console.error("Error creating record:", error);
      if (error.response?.status === 403) {
        toast.error("Permission Denied.");
      } else {
        toast.error(error.response?.data?.detail || "Failed to create record.");
      }
    },
  });

  const updateRecordMutation = useMutation({
    mutationFn: async ({
      recordId,
      data,
    }: {
      recordId: number;
      data: Partial<FuelRecord>;
    }) => {
      if (!vehicleId) throw new Error("Missing vehicleId");
      const updatePayload = {
        gallons: data.gallons,
        totalCost: data.totalCost,
        station: data.station,
        fullTank: data.fullTank,
      };
      console.log(
        `API Request: PUT /vehicles/${vehicleId}/fuelRecords/${recordId}`,
        updatePayload,
      );
      return apiClient.put(
        `/vehicles/${vehicleId}/fuelRecords/${recordId}`,
        updatePayload,
      );
    },
    onSuccess: () => {
      toast.success("Record updated");
      queryClient.invalidateQueries({
        queryKey: [
          "fuel",
          vehicleId,
          pagination.currentPage,
          pagination.pageSize,
        ],
      });
      setIsModalOpen(false);
    },
    onError: (error: any) => {
      console.error("Error updating record:", error);
      if (error.response?.status === 403) {
        toast.error("Permission Denied.");
      } else {
        toast.error(error.response?.data?.detail || "Failed to update record.");
      }
    },
  });

  const deleteRecordMutation = useMutation({
    mutationFn: (recordId: number) => {
      if (!vehicleId) throw new Error("Missing vehicleId");
      console.log(
        `API Request: DELETE /vehicles/${vehicleId}/fuelRecords/${recordId}`,
      );
      return apiClient.delete(`/vehicles/${vehicleId}/fuelRecords/${recordId}`);
    },
    onSuccess: () => {
      toast.success("Record deleted");
      if (records.length === 1 && pagination.currentPage > 1) {
        handlePageChange(pagination.currentPage - 1);
      } else {
        queryClient.invalidateQueries({
          queryKey: [
            "fuel",
            vehicleId,
            pagination.currentPage,
            pagination.pageSize,
          ],
        });
      }
    },
    onError: (error: any) => {
      console.error("Error deleting record:", error);
      if (error.response?.status === 403) {
        toast.error("Permission Denied.");
      } else {
        toast.error(error.response?.data?.detail || "Failed to delete record.");
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

  const handleCreateFuelRecord = (formData: FormData) => {
    const gallons = parseFloat(formData.get("gallons") as string);
    const costPerGallon = parseFloat(formData.get("costPerGallon") as string);
    const totalCost = parseFloat(formData.get("totalCost") as string);
    const mileage = parseInt(formData.get("mileage") as string);
    if (isNaN(gallons) || gallons <= 0) {
      toast.error("Invalid gallons value.");
      return;
    }
    if (isNaN(costPerGallon) || costPerGallon <= 0) {
      toast.error("Invalid cost per gallon.");
      return;
    }
    if (isNaN(totalCost) || totalCost < 0) {
      toast.error("Invalid total cost.");
      return;
    }
    if (isNaN(mileage) || mileage < 0) {
      toast.error("Invalid mileage.");
      return;
    }

    const recordData = {
      date: new Date(formData.get("date") as string).toISOString(),
      gallons: gallons,
      costPerGallon: costPerGallon,
      totalCost: totalCost,
      mileage: mileage,
      station: formData.get("station") as string,
      fullTank: formData.get("fullTank") === "on",
    };
    createRecordMutation.mutate(recordData);
  };

  const handleUpdateFuelRecord = (recordId: number, formData: FormData) => {
    const gallons = parseFloat(formData.get("gallons") as string);
    const totalCost = parseFloat(formData.get("totalCost") as string);
    if (isNaN(gallons) || gallons <= 0) {
      toast.error("Invalid gallons value.");
      return;
    }
    if (isNaN(totalCost) || totalCost < 0) {
      toast.error("Invalid total cost.");
      return;
    }

    const recordData = {
      date: new Date(formData.get("date") as string).toISOString(),
      gallons: gallons,
      costPerGallon: parseFloat(formData.get("costPerGallon") as string),
      totalCost: totalCost,
      mileage: parseInt(formData.get("mileage") as string),
      station: formData.get("station") as string,
      fullTank: formData.get("fullTank") === "on",
    };
    updateRecordMutation.mutate({ recordId, data: recordData });
  };

  const handleDeleteRecord = (recordId: number) => {
    if (window.confirm("Delete this fuel record?")) {
      deleteRecordMutation.mutate(recordId);
    }
  };

  const handleFormSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    if (currentRecord) {
      handleUpdateFuelRecord(currentRecord.id, formData);
    } else {
      handleCreateFuelRecord(formData);
    }
  };

  const handleAutoCalculateTotal = (
    gallons: number,
    pricePerGallon: number,
  ) => {
    if (
      isNaN(gallons) ||
      isNaN(pricePerGallon) ||
      gallons <= 0 ||
      pricePerGallon <= 0
    )
      return "";
    return (gallons * pricePerGallon).toFixed(2);
  };

  const calculateFuelStats = (fuelData: FuelRecord[]) => {
    if (fuelData.length < 2) {
      setFuelStats({
        avgMpg: 0,
        totalGallons: 0,
        totalCost: 0,
        avgCostPerGallon: 0,
      });
      return;
    }
    const sortedRecords = [...fuelData].sort((a, b) => a.mileage - b.mileage);
    let totalDistance = 0;
    let totalGallonsForMpg = 0;
    let totalCost = 0;
    for (let i = 1; i < sortedRecords.length; i++) {
      const current = sortedRecords[i];
      const previous = sortedRecords[i - 1];
      const distance = current.mileage - previous.mileage;
      // Use full tank logic for MPG calculation
      if (
        distance > 0 &&
        distance < 1000 &&
        current.fullTank &&
        previous.fullTank
      ) {
        totalDistance += distance;
        totalGallonsForMpg += current.gallons;
      }
      totalCost += current.totalCost; // Sum total cost for all records on page
    }
    const avgMpg =
      totalGallonsForMpg > 0 ? totalDistance / totalGallonsForMpg : 0;
    const sumTotalGallons = fuelData.reduce(
      (sum, r) => sum + (r.gallons || 0),
      0,
    ); // Sum all gallons for display
    const avgCostPerGallon =
      sumTotalGallons > 0 ? totalCost / sumTotalGallons : 0;

    setFuelStats({
      avgMpg,
      totalGallons: sumTotalGallons,
      totalCost,
      avgCostPerGallon,
    });
  };

  const goBack = () => navigate("/vehicles");
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
  const calculateMpg = (currentRecord: FuelRecord): string => {
    // Find the correct previous record based on mileage sorting of the *current page data*
    const sortedRecords = [...records].sort((a, b) => a.mileage - b.mileage);
    const recordIndex = sortedRecords.findIndex(
      (r) => r.id === currentRecord.id,
    );

    if (recordIndex <= 0) return "N/A"; // No previous record on this page

    const previousRecord = sortedRecords[recordIndex - 1];
    if (!currentRecord.fullTank || !previousRecord.fullTank) return "N/A"; // Need consecutive full tanks

    const distance = currentRecord.mileage - previousRecord.mileage;
    if (distance <= 0 || currentRecord.gallons <= 0) return "N/A"; // Invalid data

    return (distance / currentRecord.gallons).toFixed(1);
  };

  const displayRecords = useMemo(() => {
    if (!debouncedSearchTerm) return records;
    return records.filter((record) =>
      (record.station?.toLowerCase() || "").includes(
        debouncedSearchTerm.toLowerCase(),
      ),
    );
  }, [records, debouncedSearchTerm]);

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
          <h1 className="text-3xl font-bold">Fuel Records</h1>
          {vehicle && (
            <p className="text-gray-600">
              {vehicle.make} {vehicle.model} ({vehicle.year})
            </p>
          )}
        </div>
        <div className="flex space-x-2">
          <button
            onClick={() => refetch()}
            className="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-lg flex items-center"
            disabled={isFetching}
          >
            <RefreshCw
              className={`h-5 w-5 mr-2 ${isFetching ? "animate-spin" : ""}`}
            />{" "}
            Refresh
          </button>
          <button
            onClick={() => {
              setCurrentRecord(null);
              setIsModalOpen(true);
            }}
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg flex items-center"
          >
            <Plus className="h-5 w-5 mr-2" /> Add Fuel Record
          </button>
        </div>
      </div>

      {/* Fuel stats cards */}
      {!isLoading && records.length >= 2 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
          <div className="bg-white rounded-lg shadow-md p-4">
            <div className="flex items-center justify-between mb-2">
              <h3 className="text-gray-500 text-sm">Avg Fuel Economy (Page)</h3>
              <Gauge className="h-5 w-5 text-blue-500" />
            </div>
            <p className="text-2xl font-bold">
              {fuelStats.avgMpg ? fuelStats.avgMpg.toFixed(1) : "N/A"} MPG
            </p>
          </div>
          <div className="bg-white rounded-lg shadow-md p-4">
            <div className="flex items-center justify-between mb-2">
              <h3 className="text-gray-500 text-sm">Total Fuel (Page)</h3>
              <Droplet className="h-5 w-5 text-green-500" />
            </div>
            <p className="text-2xl font-bold">
              {fuelStats.totalGallons
                ? fuelStats.totalGallons.toFixed(1)
                : "0.0"}{" "}
              gal
            </p>
          </div>
          <div className="bg-white rounded-lg shadow-md p-4">
            <div className="flex items-center justify-between mb-2">
              <h3 className="text-gray-500 text-sm">Total Cost (Page)</h3>
              <DollarSign className="h-5 w-5 text-red-500" />
            </div>
            <p className="text-2xl font-bold">
              ${fuelStats.totalCost ? fuelStats.totalCost.toFixed(2) : "0.00"}
            </p>
          </div>
          <div className="bg-white rounded-lg shadow-md p-4">
            <div className="flex items-center justify-between mb-2">
              <h3 className="text-gray-500 text-sm">Avg Price (Page)</h3>
              <ArrowUpRight className="h-5 w-5 text-purple-500" />
            </div>
            <p className="text-2xl font-bold">
              $
              {fuelStats.avgCostPerGallon
                ? fuelStats.avgCostPerGallon.toFixed(3)
                : "0.000"}
              /gal
            </p>
          </div>
        </div>
      )}

      {/* Search and Table */}
      <div className="bg-white rounded-lg shadow-md mb-8">
        <div className="p-4 border-b">
          <div className="relative flex-grow max-w-md">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <Search className="h-5 w-5 text-gray-400" />
            </div>
            <input
              type="text"
              placeholder="Search by station (client-side)..."
              className="pl-10 pr-4 py-2 border rounded-lg w-full focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>
        </div>

        {isLoading && pagination.currentPage === 1 ? (
          <div className="p-8 text-center">
            <Loader className="h-8 w-8 animate-spin text-blue-600 mx-auto" />
            <p className="mt-2 text-gray-500">Loading records...</p>
          </div>
        ) : error ? (
          <div className="p-8 text-center text-red-600">
            {" "}
            Error loading records: {error.message}{" "}
          </div>
        ) : records.length === 0 && !debouncedSearchTerm ? (
          <div className="p-8 text-center">
            <p className="text-gray-500 mb-4">
              No fuel records found. Add a fuel record to track usage.
            </p>
            <button
              onClick={() => {
                setCurrentRecord(null);
                setIsModalOpen(true);
              }}
              className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg inline-flex items-center"
            >
              <Plus className="h-5 w-5 mr-2" /> Add First Fuel Record
            </button>
          </div>
        ) : displayRecords.length === 0 && debouncedSearchTerm ? (
          <div className="p-8 text-center text-gray-500">
            {" "}
            No records match your search term "{debouncedSearchTerm}".{" "}
          </div>
        ) : (
          <div className={`overflow-x-auto ${isFetching ? "opacity-75" : ""}`}>
            <table className="w-full table-auto">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Date
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Station
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Gallons
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Price/Gal
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Total
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Mileage
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    MPG
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {displayRecords
                  .sort(
                    (
                      a: { date: string | number | Date },
                      b: { date: string | number | Date },
                    ) =>
                      new Date(b.date).getTime() - new Date(a.date).getTime(),
                  )
                  .map((record) => (
                    <tr key={record.id} className="hover:bg-gray-50">
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="text-sm text-gray-900">
                          {formatDate(record.date)}
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="flex items-center">
                          <MapPin className="h-4 w-4 text-gray-500 mr-2" />
                          <div className="text-sm text-gray-900">
                            {record.station || "N/A"}
                          </div>
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="text-sm text-gray-900">
                          {record.gallons ? record.gallons.toFixed(2) : "0.00"}
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="text-sm text-gray-900">
                          $
                          {record.costPerGallon
                            ? record.costPerGallon.toFixed(3)
                            : "0.000"}
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="text-sm font-medium text-gray-900">
                          $
                          {record.totalCost
                            ? record.totalCost.toFixed(2)
                            : "0.00"}
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="text-sm text-gray-900">
                          {record.mileage
                            ? record.mileage.toLocaleString()
                            : "0"}
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="text-sm text-gray-900">
                          {calculateMpg(record)}
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-medium">
                        <div className="flex space-x-2">
                          <button
                            onClick={() => {
                              setCurrentRecord(record);
                              setIsModalOpen(true);
                            }}
                            className="text-indigo-600 hover:text-indigo-800 p-1"
                            title="Edit"
                          >
                            <Edit className="h-5 w-5" />
                          </button>
                          <button
                            onClick={() => handleDeleteRecord(record.id)}
                            className="text-red-600 hover:text-red-800 p-1"
                            title="Delete"
                            disabled={deleteRecordMutation.isPending}
                          >
                            <Trash2 className="h-5 w-5" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Pagination Controls */}
      {currentPagination.totalCount > 0 && !isLoading && (
        <div className="flex justify-between items-center mt-6">
          <div className="text-sm text-gray-600">
            Showing page {currentPagination.currentPage} of{" "}
            {currentPagination.totalPages} ({currentPagination.totalCount} total
            records | {displayRecords.length} on this page)
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

      {/* Fuel Record Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-lg shadow-lg w-full max-w-md p-6 max-h-[90vh] overflow-y-auto">
            <h2 className="text-xl font-bold mb-4">
              {currentRecord ? "Edit Fuel Record" : "Add New Fuel Record"}
            </h2>
            <form onSubmit={handleFormSubmit} className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Date & Time <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="datetime-local"
                    name="date"
                    className="w-full border rounded-lg p-2"
                    defaultValue={
                      currentRecord?.date
                        ? formatDateTimeForInput(currentRecord.date)
                        : ""
                    }
                    required
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Station
                  </label>
                  <input
                    type="text"
                    name="station"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentRecord?.station || ""}
                    placeholder="e.g. Shell, Chevron"
                  />
                </div>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Gallons <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="number"
                    name="gallons"
                    step="0.001"
                    min="0.001"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentRecord?.gallons || ""}
                    placeholder="e.g. 12.5"
                    required
                    onChange={(e) => {
                      const gallons = parseFloat(e.target.value);
                      const costPerGallonInput = document.querySelector(
                        'input[name="costPerGallon"]',
                      ) as HTMLInputElement;
                      const totalCostInput = document.querySelector(
                        'input[name="totalCost"]',
                      ) as HTMLInputElement;
                      if (
                        costPerGallonInput &&
                        totalCostInput &&
                        !isNaN(gallons)
                      ) {
                        const costPerGallon = parseFloat(
                          costPerGallonInput.value,
                        );
                        if (!isNaN(costPerGallon))
                          totalCostInput.value = handleAutoCalculateTotal(
                            gallons,
                            costPerGallon,
                          );
                      }
                    }}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Price per Gallon <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="number"
                    name="costPerGallon"
                    step="0.001"
                    min="0.001"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentRecord?.costPerGallon || ""}
                    placeholder="e.g. 3.459"
                    required
                    onChange={(e) => {
                      const costPerGallon = parseFloat(e.target.value);
                      const gallonsInput = document.querySelector(
                        'input[name="gallons"]',
                      ) as HTMLInputElement;
                      const totalCostInput = document.querySelector(
                        'input[name="totalCost"]',
                      ) as HTMLInputElement;
                      if (
                        gallonsInput &&
                        totalCostInput &&
                        !isNaN(costPerGallon)
                      ) {
                        const gallons = parseFloat(gallonsInput.value);
                        if (!isNaN(gallons))
                          totalCostInput.value = handleAutoCalculateTotal(
                            gallons,
                            costPerGallon,
                          );
                      }
                    }}
                  />
                </div>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Total Cost <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="number"
                    name="totalCost"
                    step="0.01"
                    min="0"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentRecord?.totalCost || ""}
                    placeholder="e.g. 43.13"
                    required
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Odometer Reading <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="number"
                    name="mileage"
                    min="0"
                    className="w-full border rounded-lg p-2"
                    defaultValue={currentRecord?.mileage || ""}
                    placeholder="e.g. 15000"
                    required
                  />
                </div>
              </div>
              <div className="flex items-center">
                <input
                  type="checkbox"
                  id="fullTank"
                  name="fullTank"
                  className="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
                  defaultChecked={currentRecord?.fullTank ?? true}
                />
                <label
                  htmlFor="fullTank"
                  className="ml-2 block text-sm text-gray-900"
                >
                  Full tank fill-up (for MPG calc)
                </label>
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
                    createRecordMutation.isPending ||
                    updateRecordMutation.isPending
                  }
                >
                  {(createRecordMutation.isPending ||
                    updateRecordMutation.isPending) && (
                    <Loader className="animate-spin h-4 w-4 mr-2" />
                  )}
                  {currentRecord ? "Save Changes" : "Add Record"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default FuelRecords;
