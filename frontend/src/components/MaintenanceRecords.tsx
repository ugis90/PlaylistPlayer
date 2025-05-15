import React, { useState, useEffect, useMemo } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { toast } from "sonner";
import {
  Wrench,
  ChevronLeft,
  Search,
  Calendar,
  Plus,
  Tag,
  Edit,
  Trash2,
  AlertTriangle,
  RefreshCw,
  Loader,
  ChevronRight,
} from "lucide-react";
import { apiClient } from "../api/client";
import { Vehicle, MaintenanceRecord } from "../types";
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
interface MaintenanceApiResponse {
  resource: Array<{ resource: MaintenanceRecord; links: any[] }>;
  links: any[];
}
interface MaintenanceQueryData {
  records: MaintenanceRecord[];
  pagination: PaginationInfo;
}

interface UpdateMaintenanceRecordDto {
  serviceType?: string | null;
  description?: string | null;
  cost?: number | null;
  mileage?: number | null;
  date?: string | null;
  provider?: string | null;
  nextServiceDue?: string | null;
}

const MaintenanceRecords = () => {
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearchTerm, setDebouncedSearchTerm] = useState(searchTerm);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [currentRecord, setCurrentRecord] = useState<MaintenanceRecord | null>(
    null,
  );
  const [vehicle, setVehicle] = useState<Vehicle | null>(null);
  const [formErrors, setFormErrors] = useState<Record<string, string>>({});

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
      toast.error("Vehicle ID is missing!");
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

  const queryKey = useMemo(
    () =>
      [
        "maintenance",
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
  } = useQuery<MaintenanceQueryData, Error>({
    initialData: undefined,
    queryKey: queryKey,
    queryFn: async (): Promise<MaintenanceQueryData> => {
      if (!vehicleId) throw new Error("Vehicle ID is required");
      console.log(`Fetching maintenance page ${pagination.currentPage}...`);
      const url = `/vehicles/${vehicleId}/maintenanceRecords?pageNumber=${pagination.currentPage}&pageSize=${pagination.pageSize}${debouncedSearchTerm ? `&searchTerm=${encodeURIComponent(debouncedSearchTerm)}` : ""}`;
      const response = await apiClient.get<MaintenanceApiResponse>(url);

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

      const recordsData: MaintenanceRecord[] =
        response.data?.resource
          ?.map((item: any) => item.resource)
          .filter(Boolean) ?? [];
      const validRecords = recordsData.filter(
        (r) => r && r.id && r.serviceType && r.date,
      );

      console.log("Parsed maintenance records:", validRecords);
      console.log("Calculated Pagination:", paginationInfo);
      setPagination(paginationInfo);

      return { records: validRecords, pagination: paginationInfo };
    },
    enabled: !!vehicleId,
    staleTime: 60 * 1000,
    gcTime: 5 * 60 * 1000,
    keepPreviousData: true,
  });

  // eslint-disable-next-line @typescript-eslint/ban-ts-comment
  // @ts-expect-error
  const records = queryResult?.records ?? [];
  const currentPagination = pagination;

  // --- Mutations ---
  const createRecordMutation = useMutation({
    mutationFn: async (
      recordData: Omit<MaintenanceRecord, "id" | "vehicleId" | "createdOn">,
    ) => {
      if (!vehicleId) throw new Error("Missing vehicleId");
      return apiClient.post(
        `/vehicles/${vehicleId}/maintenanceRecords`,
        recordData,
      );
    },
    onSuccess: () => {
      toast.success("Record created");
      queryClient.invalidateQueries({ queryKey: ["maintenance", vehicleId] });
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

  const formatCurrency = (value: number | undefined | null) => {
    if (value === undefined || value === null || isNaN(value)) return "€0.00";
    return new Intl.NumberFormat("lt-LT", {
      style: "currency",
      currency: "EUR",
    }).format(value);
  };

  const updateRecordMutation = useMutation({
    mutationFn: async ({
      recordId,
      data,
    }: {
      recordId: number;
      data: Partial<UpdateMaintenanceRecordDto>;
    }) => {
      if (!vehicleId) throw new Error("Missing vehicleId");
      return apiClient.put(
        `/vehicles/${vehicleId}/maintenanceRecords/${recordId}`,
        data,
      );
    },
    onSuccess: () => {
      toast.success("Record updated");
      queryClient.invalidateQueries({ queryKey: queryKey });
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
      return apiClient.delete(
        `/vehicles/${vehicleId}/maintenanceRecords/${recordId}`,
      );
    },
    onSuccess: () => {
      toast.success("Record deleted");
      if (records.length === 1 && pagination.currentPage > 1) {
        handlePageChange(pagination.currentPage - 1);
      } else {
        queryClient.invalidateQueries({ queryKey: queryKey });
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

  const validateMaintenanceForm = (
    formData: FormData,
  ): Record<string, string> => {
    const errors: Record<string, string> = {};
    const serviceType = formData.get("serviceType") as string;
    const description = formData.get("description") as string;
    const costStr = formData.get("cost") as string;
    const mileageStr = formData.get("mileage") as string;
    const date = formData.get("date") as string;
    const nextServiceDue = formData.get("nextServiceDue") as string;

    if (!serviceType || serviceType.trim() === "")
      errors.serviceType = "Service type is required";
    if (!description || description.trim() === "")
      errors.description = "Description is required";
    const cost = parseFloat(costStr);
    if (isNaN(cost) || cost < 0)
      errors.cost = "Cost must be a valid positive number";
    const mileage = parseInt(mileageStr);
    if (isNaN(mileage) || mileage < 0)
      errors.mileage = "Mileage must be a valid positive number";
    if (!date) errors.date = "Date is required";
    else {
      try {
        if (isNaN(new Date(date).getTime()))
          errors.date = "Invalid date format";
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
      } catch (e) {
        errors.date = "Invalid date format";
      }
    }
    if (vehicle && mileage > vehicle.currentMileage)
      errors.mileage = `Mileage cannot be greater than vehicle's current mileage (${vehicle.currentMileage})`;
    if (nextServiceDue && nextServiceDue.trim() !== "") {
      try {
        const nextServiceDueDate = new Date(nextServiceDue);
        const serviceDate = new Date(date);
        if (isNaN(nextServiceDueDate.getTime()))
          errors.nextServiceDue = "Invalid date format";
        else if (nextServiceDueDate <= serviceDate)
          errors.nextServiceDue =
            "Next service due date must be after the service date";
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
      } catch (e) {
        errors.nextServiceDue = "Invalid date format";
      }
    }
    return errors;
  };

  const handleCreateRecord = (formData: FormData) => {
    const errors = validateMaintenanceForm(formData);
    if (Object.keys(errors).length > 0) {
      setFormErrors(errors);
      toast.error("Please fix form errors");
      return;
    }
    const recordData = {
      serviceType: formData.get("serviceType") as string,
      description: formData.get("description") as string,
      cost: parseFloat(formData.get("cost") as string),
      mileage: parseInt(formData.get("mileage") as string),
      date: new Date(formData.get("date") as string).toISOString(),
      provider: formData.get("provider") as string,
      nextServiceDue: formData.get("nextServiceDue")
        ? new Date(formData.get("nextServiceDue") as string).toISOString()
        : null,
    };
    createRecordMutation.mutate(recordData);
  };

  const handleUpdateRecord = (recordId: number, formData: FormData) => {
    const errors = validateMaintenanceForm(formData);
    if (Object.keys(errors).length > 0) {
      setFormErrors(errors);
      toast.error("Please fix form errors");
      return;
    }
    const recordData: UpdateMaintenanceRecordDto = {
      serviceType: formData.get("serviceType") as string,
      description: formData.get("description") as string,
      cost: parseFloat(formData.get("cost") as string),
      mileage: parseInt(formData.get("mileage") as string),
      date: new Date(formData.get("date") as string).toISOString(),
      provider: formData.get("provider") as string,
      nextServiceDue: formData.get("nextServiceDue")
        ? new Date(formData.get("nextServiceDue") as string).toISOString()
        : null,
    };
    updateRecordMutation.mutate({ recordId, data: recordData });
  };

  const handleDeleteRecord = (recordId: number) => {
    if (window.confirm("Delete this record?")) {
      deleteRecordMutation.mutate(recordId);
    }
  };

  const handleFormSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    if (currentRecord) {
      handleUpdateRecord(currentRecord.id, formData);
    } else {
      handleCreateRecord(formData);
    }
  };

  const goBack = () => navigate(`/vehicles`);

  const formatDate = (dateString: string | null | undefined) => {
    if (!dateString) return "N/A";
    try {
      return new Date(dateString).toLocaleDateString("en-US", {
        year: "numeric",
        month: "short",
        day: "numeric",
      });
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
    } catch (error) {
      return "Invalid date";
    }
  };
  const formatDateForInput = (dateString: string | null | undefined) => {
    if (!dateString) return "";
    try {
      return new Date(dateString).toISOString().slice(0, 10);
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
    } catch (error) {
      return "";
    }
  };
  const isServiceDueSoon = (dateString: string | null | undefined) => {
    if (!dateString) return false;
    try {
      const dueDate = new Date(dateString);
      const today = new Date();
      const thirtyDaysFromNow = new Date();
      thirtyDaysFromNow.setDate(today.getDate() + 30);
      return dueDate <= thirtyDaysFromNow && dueDate >= today;
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
    } catch (error) {
      return false;
    }
  };
  const isServiceOverdue = (dateString: string | null | undefined) => {
    if (!dateString) return false;
    try {
      const dueDate = new Date(dateString);
      const today = new Date();
      return dueDate < today;
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
    } catch (error) {
      return false;
    }
  };
  const displayRecords = useMemo(() => {
    if (!debouncedSearchTerm) return records;
    return records.filter(
      (record: MaintenanceRecord) =>
        (record.serviceType?.toLowerCase() || "").includes(
          debouncedSearchTerm.toLowerCase(),
        ) ||
        (record.description?.toLowerCase() || "").includes(
          debouncedSearchTerm.toLowerCase(),
        ) ||
        (record.provider?.toLowerCase() || "").includes(
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
          <h1 className="text-3xl font-bold">Maintenance Records</h1>
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
              setFormErrors({});
              setIsModalOpen(true);
            }}
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg flex items-center"
          >
            <Plus className="h-5 w-5 mr-2" /> Add Record
          </button>
        </div>
      </div>

      {/* Search and Filter Bar */}
      <div className="bg-white rounded-lg shadow-md mb-8">
        <div className="p-4 border-b">
          <div className="relative flex-grow max-w-md">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <Search className="h-5 w-5 text-gray-400" />
            </div>
            <input
              type="text"
              placeholder="Search maintenance..."
              className="pl-10 pr-4 py-2 border rounded-lg w-full focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>
        </div>

        {/* Records List */}
        {isLoading && !queryResult ? (
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
            <p className="text-gray-600 mb-4">
              No maintenance records found for this vehicle.
            </p>
            <button
              onClick={() => {
                setCurrentRecord(null);
                setFormErrors({});
                setIsModalOpen(true);
              }}
              className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg inline-flex items-center"
            >
              <Plus className="h-5 w-5 mr-2" /> Add First Record
            </button>
          </div>
        ) : displayRecords.length === 0 && debouncedSearchTerm ? (
          <div className="p-8 text-center text-gray-500">
            {" "}
            No records match your search term "{debouncedSearchTerm}".{" "}
          </div>
        ) : (
          <div
            className={`divide-y divide-gray-200 ${isFetching ? "opacity-75" : ""}`}
          >
            {displayRecords.map((record: MaintenanceRecord) => (
              <div key={record.id} className="p-6 hover:bg-gray-50">
                <div className="flex flex-col md:flex-row justify-between">
                  {/* Record Details */}
                  <div className="flex-grow">
                    <div className="flex items-start mb-2">
                      <Wrench className="h-5 w-5 mr-2 text-blue-600 flex-shrink-0 mt-1" />
                      <div>
                        <h3 className="font-medium text-lg">
                          {record.serviceType || "N/A"}
                        </h3>
                        <p className="text-gray-600">
                          {record.description || "N/A"}
                        </p>
                      </div>
                    </div>
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4 text-sm">
                      <div className="flex items-center">
                        <Calendar className="h-4 w-4 mr-2 text-gray-500" />
                        <span className="text-gray-700">
                          {formatDate(record.date)}
                        </span>
                      </div>
                      <div className="flex items-center">
                        <span className="text-gray-700">
                          {formatCurrency(record.cost)}
                        </span>
                      </div>
                      <div className="flex items-center">
                        <Tag className="h-4 w-4 mr-2 text-gray-500" />
                        <span className="text-gray-700">
                          {record.provider || "N/A"}
                        </span>
                      </div>
                    </div>
                    {record.nextServiceDue && (
                      <div
                        className={`mt-4 flex items-center p-2 rounded-md text-xs ${isServiceOverdue(record.nextServiceDue) ? "bg-red-100 text-red-800" : isServiceDueSoon(record.nextServiceDue) ? "bg-yellow-100 text-yellow-800" : "bg-green-100 text-green-800"}`}
                      >
                        <AlertTriangle
                          className={`h-4 w-4 mr-2 ${isServiceOverdue(record.nextServiceDue) ? "text-red-600" : isServiceDueSoon(record.nextServiceDue) ? "text-yellow-600" : "text-green-600"}`}
                        />
                        <span>
                          {isServiceOverdue(record.nextServiceDue)
                            ? `Overdue! Was due ${formatDate(record.nextServiceDue)}`
                            : isServiceDueSoon(record.nextServiceDue)
                              ? `Due soon: ${formatDate(record.nextServiceDue)}`
                              : `Next due: ${formatDate(record.nextServiceDue)}`}
                        </span>
                      </div>
                    )}
                  </div>
                  {/* Action Buttons */}
                  <div className="flex space-x-2 mt-4 md:mt-0 md:ml-6 flex-shrink-0 self-start md:self-center">
                    <button
                      onClick={() => {
                        setCurrentRecord(record);
                        setFormErrors({});
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

      {/* Maintenance Record Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-lg shadow-lg w-full max-w-md p-6 max-h-[90vh] overflow-y-auto">
            <h2 className="text-xl font-bold mb-4">
              {currentRecord
                ? "Edit Maintenance Record"
                : "Add New Maintenance Record"}
            </h2>
            <form onSubmit={handleFormSubmit} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Service Type <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  name="serviceType"
                  className={`w-full border ${formErrors.serviceType ? "border-red-500" : "border-gray-300"} rounded-lg p-2`}
                  defaultValue={currentRecord?.serviceType || ""}
                  required
                />
                {formErrors.serviceType && (
                  <p className="mt-1 text-sm text-red-500">
                    {formErrors.serviceType}
                  </p>
                )}
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Description <span className="text-red-500">*</span>
                </label>
                <textarea
                  name="description"
                  className={`w-full border ${formErrors.description ? "border-red-500" : "border-gray-300"} rounded-lg p-2`}
                  defaultValue={currentRecord?.description || ""}
                  rows={3}
                  required
                />
                {formErrors.description && (
                  <p className="mt-1 text-sm text-red-500">
                    {formErrors.description}
                  </p>
                )}
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Date <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="date"
                    name="date"
                    className={`w-full border ${formErrors.date ? "border-red-500" : "border-gray-300"} rounded-lg p-2`}
                    defaultValue={formatDateForInput(currentRecord?.date)}
                    required
                  />
                  {formErrors.date && (
                    <p className="mt-1 text-sm text-red-500">
                      {formErrors.date}
                    </p>
                  )}
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Cost ($) <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="number"
                    name="cost"
                    step="0.01"
                    min="0"
                    className={`w-full border ${formErrors.cost ? "border-red-500" : "border-gray-300"} rounded-lg p-2`}
                    defaultValue={currentRecord?.cost ?? ""}
                    required
                  />
                  {formErrors.cost && (
                    <p className="mt-1 text-sm text-red-500">
                      {formErrors.cost}
                    </p>
                  )}
                </div>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Mileage <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="number"
                    name="mileage"
                    min="0"
                    className={`w-full border ${formErrors.mileage ? "border-red-500" : "border-gray-300"} rounded-lg p-2`}
                    defaultValue={currentRecord?.mileage ?? ""}
                    required
                  />
                  {formErrors.mileage && (
                    <p className="mt-1 text-sm text-red-500">
                      {formErrors.mileage}
                    </p>
                  )}
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Provider
                  </label>
                  <input
                    type="text"
                    name="provider"
                    className={`w-full border ${formErrors.provider ? "border-red-500" : "border-gray-300"} rounded-lg p-2`}
                    defaultValue={currentRecord?.provider || ""}
                  />
                  {formErrors.provider && (
                    <p className="mt-1 text-sm text-red-500">
                      {formErrors.provider}
                    </p>
                  )}
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Next Service Due
                </label>
                <input
                  type="date"
                  name="nextServiceDue"
                  className={`w-full border ${formErrors.nextServiceDue ? "border-red-500" : "border-gray-300"} rounded-lg p-2`}
                  defaultValue={formatDateForInput(
                    currentRecord?.nextServiceDue,
                  )}
                />
                {formErrors.nextServiceDue && (
                  <p className="mt-1 text-sm text-red-500">
                    {formErrors.nextServiceDue}
                  </p>
                )}
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

export default MaintenanceRecords;
