// src/components/VehicleAnalytics.tsx

import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { toast } from "sonner";
import {
  BarChart,
  Bar,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Area,
  AreaChart,
} from "recharts";
import {
  Calendar,
  ChevronLeft,
  DollarSign,
  Droplet,
  RefreshCw,
  ArrowUpDown,
  AlertTriangle,
} from "lucide-react";
import apiClient from "../api/client";
import { Vehicle } from "../types";

// Define types for analytics data
interface VehicleAnalytics {
  totalCost: number;
  mileage: number;
  costPerMile: number;
  totalTrips: number;
  fuelEfficiency: number;
  maintenanceCosts: number;
  fuelCosts: number;
  upcomingMaintenance: UpcomingMaintenance[];
  fuelEfficiencyTrend: FuelEfficiencyTrend[];
  costByCategory: CostByCategory;
  costByMonth: CostByMonth[];
}

interface UpcomingMaintenance {
  type: string;
  dueDate: string;
  estimatedCost: number;
}

interface FuelEfficiencyTrend {
  date: string;
  mpg: number;
}

interface CostByCategory {
  fuel: number;
  maintenance: number;
  repairs: number;
}

interface CostByMonth {
  month: string;
  cost: number;
}

const VehicleAnalytics = () => {
  const [isLoading, setIsLoading] = useState(true);
  const [vehicle, setVehicle] = useState<Vehicle | null>(null);
  const [analytics, setAnalytics] = useState<VehicleAnalytics | null>(null);
  const [dateRange, setDateRange] = useState({
    startDate: new Date(new Date().setFullYear(new Date().getFullYear() - 1))
      .toISOString()
      .split("T")[0],
    endDate: new Date().toISOString().split("T")[0],
  });
  const [error, setError] = useState<string | null>(null);

  const { vehicleId } = useParams<{ vehicleId: string }>();
  const navigate = useNavigate();

  // Chart colors
  const COLORS = ["#0088FE", "#00C49F", "#FFBB28", "#FF8042", "#8884d8"];

  useEffect(() => {
    if (vehicleId) {
      fetchVehicleDetails();
      fetchAnalytics();
    }
  }, [vehicleId, dateRange]);

  const fetchVehicleDetails = async () => {
    try {
      const response = await apiClient.get(`/vehicles/${vehicleId}`);
      setVehicle(response.data);
    } catch (error) {
      console.error("Error fetching vehicle details:", error);
      toast.error("Failed to load vehicle details");
    }
  };

  const fetchAnalytics = async () => {
    setIsLoading(true);
    setError(null);

    try {
      // Build URL with date range params
      const url = `/vehicles/${vehicleId}/analytics?startDate=${dateRange.startDate}T00:00:00Z&endDate=${dateRange.endDate}T23:59:59Z`;

      const response = await apiClient.get(url);
      console.log("Analytics response:", response);

      // Process the response with better error handling
      if (response.data) {
        // Make sure we have all the expected properties, fill in any missing ones
        const analytics = {
          totalCost: response.data.totalCost || 0,
          mileage: response.data.mileage || 0,
          costPerMile: response.data.costPerMile || 0,
          totalTrips: response.data.totalTrips || 0,
          fuelEfficiency: response.data.fuelEfficiency || 0,
          maintenanceCosts: response.data.maintenanceCosts || 0,
          fuelCosts: response.data.fuelCosts || 0,
          upcomingMaintenance: response.data.upcomingMaintenance || [],
          fuelEfficiencyTrend: response.data.fuelEfficiencyTrend || [],
          costByCategory: response.data.costByCategory || {
            fuel: 0,
            maintenance: 0,
            repairs: 0,
          },
          costByMonth: response.data.costByMonth || [],
        };

        setAnalytics(analytics);
      } else {
        // If the API doesn't return the expected data, let's compute it ourselves
        await computeAnalyticsFromScratch();
      }
    } catch (error) {
      console.error("Error fetching analytics:", error);
      setError("Failed to load analytics data. Computing from raw data...");

      // Try to compute analytics directly
      await computeAnalyticsFromScratch();
    } finally {
      setIsLoading(false);
    }
  };

  // Function to compute analytics from raw data when the API fails
  const computeAnalyticsFromScratch = async () => {
    try {
      // We'll need to fetch trips, fuel records, and maintenance records
      const [tripsResponse, fuelResponse] = await Promise.all([
        apiClient.get(`/vehicles/${vehicleId}/trips`),
        apiClient.get(`/vehicles/${vehicleId}/fuelRecords`),
      ]);

      // Extract trips data with the same approach we've been using
      let trips = [];
      if (Array.isArray(tripsResponse.data)) {
        trips = tripsResponse.data;
      } else if (tripsResponse.data?.resources) {
        trips = tripsResponse.data.resources.map(
          (item) => item.resource || item,
        );
      } else if (typeof tripsResponse.data === "object") {
        Object.keys(tripsResponse.data).forEach((key) => {
          if (Array.isArray(tripsResponse.data[key])) {
            trips = tripsResponse.data[key];
          }
        });
      }

      // Extract fuel records
      let fuelRecords = [];
      if (Array.isArray(fuelResponse.data)) {
        fuelRecords = fuelResponse.data;
      } else if (fuelResponse.data?.resources) {
        fuelRecords = fuelResponse.data.resources.map(
          (item) => item.resource || item,
        );
      } else if (typeof fuelResponse.data === "object") {
        Object.keys(fuelResponse.data).forEach((key) => {
          if (Array.isArray(fuelResponse.data[key])) {
            fuelRecords = fuelResponse.data[key];
          }
        });
      }

      // Now fetch maintenance records for each trip
      let maintenanceRecords = [];
      for (const trip of trips) {
        try {
          const maintenanceResponse = await apiClient.get(
            `/vehicles/${vehicleId}/trips/${trip.id}/maintenanceRecords`,
          );

          let tripMaintenance = [];
          if (Array.isArray(maintenanceResponse.data)) {
            tripMaintenance = maintenanceResponse.data;
          } else if (maintenanceResponse.data?.resources) {
            tripMaintenance = maintenanceResponse.data.resources.map(
              (item) => item.resource || item,
            );
          } else if (typeof maintenanceResponse.data === "object") {
            Object.keys(maintenanceResponse.data).forEach((key) => {
              if (Array.isArray(maintenanceResponse.data[key])) {
                tripMaintenance = maintenanceResponse.data[key];
              }
            });
          }

          maintenanceRecords = [...maintenanceRecords, ...tripMaintenance];
        } catch (err) {
          console.warn(`Could not fetch maintenance for trip ${trip.id}:`, err);
        }
      }

      // Calculate analytics from raw data
      const analytics = calculateAnalyticsFromRawData(
        trips,
        fuelRecords,
        maintenanceRecords,
      );
      setAnalytics(analytics);
    } catch (error) {
      console.error("Error computing analytics from scratch:", error);
      setError("Could not compute analytics from raw data.");

      // Create some default data so the UI doesn't break
      const defaultAnalytics = {
        totalCost: 0,
        mileage: 0,
        costPerMile: 0,
        totalTrips: 0,
        fuelEfficiency: 0,
        maintenanceCosts: 0,
        fuelCosts: 0,
        upcomingMaintenance: [],
        fuelEfficiencyTrend: [],
        costByCategory: {
          fuel: 0,
          maintenance: 0,
          repairs: 0,
        },
        costByMonth: [],
      };

      setAnalytics(defaultAnalytics);
    }
  };

  // Function to calculate analytics from raw data
  const calculateAnalyticsFromRawData = (
    trips,
    fuelRecords,
    maintenanceRecords,
  ) => {
    // Filter by date range
    const startDate = new Date(dateRange.startDate);
    const endDate = new Date(dateRange.endDate);

    trips = trips.filter((trip) => {
      const tripDate = new Date(trip.startTime);
      return tripDate >= startDate && tripDate <= endDate;
    });

    fuelRecords = fuelRecords.filter((record) => {
      const recordDate = new Date(record.date);
      return recordDate >= startDate && recordDate <= endDate;
    });

    maintenanceRecords = maintenanceRecords.filter((record) => {
      const recordDate = new Date(record.date);
      return recordDate >= startDate && recordDate <= endDate;
    });

    // Calculate totals
    const totalTrips = trips.length;
    const totalDistance = trips.reduce(
      (sum, trip) => sum + (trip.distance || 0),
      0,
    );
    const fuelCosts = fuelRecords.reduce(
      (sum, record) => sum + (record.totalCost || 0),
      0,
    );
    const maintenanceCosts = maintenanceRecords.reduce(
      (sum, record) => sum + (record.cost || 0),
      0,
    );
    const totalCost = fuelCosts + maintenanceCosts;
    const costPerMile = totalDistance > 0 ? totalCost / totalDistance : 0;

    // Calculate fuel efficiency (MPG)
    let fuelEfficiency = 0;
    if (fuelRecords.length >= 2) {
      // Sort by mileage
      const sortedRecords = [...fuelRecords].sort(
        (a, b) => a.mileage - b.mileage,
      );

      let totalDistanceForMPG = 0;
      let totalGallons = 0;

      for (let i = 1; i < sortedRecords.length; i++) {
        const curr = sortedRecords[i];
        const prev = sortedRecords[i - 1];

        const distance = curr.mileage - prev.mileage;
        if (distance > 0 && distance < 1000) {
          // Sanity check
          totalDistanceForMPG += distance;
          totalGallons += curr.gallons || 0;
        }
      }

      if (totalGallons > 0) {
        fuelEfficiency = totalDistanceForMPG / totalGallons;
      }
    }

    // Generate cost by month
    const costByMonth = [];
    const monthMap = new Map();

    // Add fuel costs by month
    fuelRecords.forEach((record) => {
      const date = new Date(record.date);
      const monthKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;
      const cost = record.totalCost || 0;

      if (monthMap.has(monthKey)) {
        monthMap.set(monthKey, monthMap.get(monthKey) + cost);
      } else {
        monthMap.set(monthKey, cost);
      }
    });

    // Add maintenance costs by month
    maintenanceRecords.forEach((record) => {
      const date = new Date(record.date);
      const monthKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;
      const cost = record.cost || 0;

      if (monthMap.has(monthKey)) {
        monthMap.set(monthKey, monthMap.get(monthKey) + cost);
      } else {
        monthMap.set(monthKey, cost);
      }
    });

    // Convert map to array and sort by month
    Array.from(monthMap.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .forEach(([month, cost]) => {
        costByMonth.push({
          month,
          cost,
        });
      });

    // Generate upcoming maintenance based on next service due dates
    const upcomingMaintenance = maintenanceRecords
      .filter((record) => record.nextServiceDue)
      .map((record) => ({
        type: record.serviceType,
        dueDate: record.nextServiceDue,
        estimatedCost: record.cost, // Use previous cost as estimate
      }))
      .sort(
        (a, b) => new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime(),
      )
      .slice(0, 3); // Get top 3 upcoming

    // Calculate fuel efficiency trend
    const fuelEfficiencyTrend = [];
    if (fuelRecords.length >= 2) {
      // Group by month
      const efficiencyByMonth = new Map();
      fuelRecords.forEach((record) => {
        const date = new Date(record.date);
        const monthKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;

        if (!efficiencyByMonth.has(monthKey)) {
          efficiencyByMonth.set(monthKey, {
            records: [],
          });
        }

        efficiencyByMonth.get(monthKey).records.push(record);
      });

      // Calculate MPG for each month
      efficiencyByMonth.forEach((data, month) => {
        if (data.records.length >= 2) {
          const sortedRecords = [...data.records].sort(
            (a, b) => a.mileage - b.mileage,
          );

          let totalDistance = 0;
          let totalGallons = 0;

          for (let i = 1; i < sortedRecords.length; i++) {
            const distance =
              sortedRecords[i].mileage - sortedRecords[i - 1].mileage;
            if (distance > 0 && distance < 1000) {
              totalDistance += distance;
              totalGallons += sortedRecords[i].gallons || 0;
            }
          }

          if (totalGallons > 0) {
            fuelEfficiencyTrend.push({
              date: `${month}-01`,
              mpg: parseFloat((totalDistance / totalGallons).toFixed(1)),
            });
          }
        }
      });

      // Sort by date
      fuelEfficiencyTrend.sort((a, b) => a.date.localeCompare(b.date));
    }

    return {
      totalCost,
      mileage: totalDistance,
      costPerMile,
      totalTrips,
      fuelEfficiency,
      maintenanceCosts,
      fuelCosts,
      upcomingMaintenance,
      fuelEfficiencyTrend,
      costByCategory: {
        fuel: fuelCosts,
        maintenance: maintenanceCosts,
        repairs: 0, // We don't have a separate category for repairs
      },
      costByMonth,
    };
  };

  const formatDate = (dateString: string) => {
    try {
      const date = new Date(dateString);
      return date.toLocaleDateString("en-US", {
        year: "numeric",
        month: "short",
        day: "numeric",
      });
    } catch (error) {
      return "Invalid date";
    }
  };

  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat("en-US", {
      style: "currency",
      currency: "USD",
    }).format(value);
  };

  const goBack = () => {
    navigate(`/vehicles`);
  };

  const getMaintenanceStatusColor = (dueDate: string) => {
    const today = new Date();
    const due = new Date(dueDate);
    const daysDifference = Math.floor(
      (due.getTime() - today.getTime()) / (1000 * 60 * 60 * 24),
    );

    if (daysDifference < 0) return "bg-red-100 text-red-800"; // Overdue
    if (daysDifference < 14) return "bg-yellow-100 text-yellow-800"; // Due soon
    return "bg-green-100 text-green-800"; // Not urgent
  };

  const generateFuelEfficiencyTrend = (fuelRecords: FuelRecord[]) => {
    if (fuelRecords.length < 2) return [];

    // Sort records by date
    const sortedRecords = [...fuelRecords].sort(
      (a, b) => new Date(a.date).getTime() - new Date(b.date).getTime(),
    );

    // Group by month
    const monthlyData = new Map<
      string,
      {
        month: string;
        records: FuelRecord[];
        totalDistance: number;
        totalGallons: number;
        mpg: number | null;
      }
    >();

    // Initialize with months
    for (let i = 0; i < 12; i++) {
      const date = new Date();
      date.setMonth(date.getMonth() - i);
      const monthKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;
      const monthName = date.toLocaleDateString("en-US", {
        year: "numeric",
        month: "short",
      });

      monthlyData.set(monthKey, {
        month: monthName,
        records: [],
        totalDistance: 0,
        totalGallons: 0,
        mpg: null,
      });
    }

    // Assign records to months
    sortedRecords.forEach((record) => {
      try {
        const date = new Date(record.date);
        const monthKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;

        if (monthlyData.has(monthKey)) {
          monthlyData.get(monthKey)!.records.push(record);
        }
      } catch (e) {
        console.warn("Invalid date in fuel record:", record);
      }
    });

    // Calculate MPG for each month
    monthlyData.forEach((data, monthKey) => {
      const records = data.records;
      if (records.length < 2) return;

      // Sort by mileage for this month
      const monthlySortedRecords = [...records].sort(
        (a, b) => a.mileage - b.mileage,
      );

      // Calculate total distance and gallons
      let totalDistance = 0;
      let totalGallons = 0;

      for (let i = 1; i < monthlySortedRecords.length; i++) {
        const current = monthlySortedRecords[i];
        const previous = monthlySortedRecords[i - 1];

        const distance = current.mileage - previous.mileage;
        if (distance > 0 && distance < 1000) {
          // Sanity check
          totalDistance += distance;
          totalGallons += current.gallons || 0;
        }
      }

      if (totalGallons > 0) {
        data.totalDistance = totalDistance;
        data.totalGallons = totalGallons;
        data.mpg = parseFloat((totalDistance / totalGallons).toFixed(1));
      }
    });

    // Format for chart
    return Array.from(monthlyData.values())
      .filter((data) => data.mpg !== null)
      .map((data) => ({
        date: data.month,
        mpg: data.mpg || 0,
      }))
      .reverse(); // Most recent months first
  };

  return (
    <div className="container mx-auto px-4 py-8">
      <button
        onClick={goBack}
        className="flex items-center text-blue-600 hover:text-blue-800 mb-4"
      >
        <ChevronLeft className="h-5 w-5 mr-1" />
        Back to Vehicles
      </button>

      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="text-3xl font-bold">Vehicle Analytics</h1>
          {vehicle && (
            <p className="text-gray-600">
              {vehicle.make} {vehicle.model} ({vehicle.year}) -{" "}
              {vehicle.licensePlate}
            </p>
          )}
        </div>
        <div className="flex gap-4">
          <button
            onClick={fetchAnalytics}
            className="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-lg flex items-center"
          >
            <RefreshCw className="h-5 w-5 mr-2" />
            Refresh
          </button>
        </div>
      </div>

      {/* Date range selector */}
      <div className="bg-white rounded-lg shadow-md p-4 mb-6">
        <div className="flex flex-col md:flex-row md:items-center gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Start Date
            </label>
            <input
              type="date"
              value={dateRange.startDate}
              onChange={(e) =>
                setDateRange({ ...dateRange, startDate: e.target.value })
              }
              className="border rounded-lg p-2"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              End Date
            </label>
            <input
              type="date"
              value={dateRange.endDate}
              onChange={(e) =>
                setDateRange({ ...dateRange, endDate: e.target.value })
              }
              className="border rounded-lg p-2"
            />
          </div>
          <button
            onClick={fetchAnalytics}
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg self-end"
          >
            Apply Date Range
          </button>
        </div>
      </div>

      {error && (
        <div className="bg-yellow-100 border border-yellow-400 text-yellow-700 px-4 py-3 rounded mb-6 flex items-center">
          <AlertTriangle className="h-5 w-5 mr-2" />
          <span>{error}</span>
        </div>
      )}

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          {[1, 2, 3, 4].map((i) => (
            <div
              key={i}
              className="bg-white rounded-lg shadow-md p-6 animate-pulse"
            >
              <div className="h-8 w-8 rounded-full bg-gray-200 mb-4"></div>
              <div className="h-5 w-24 bg-gray-200 mb-2"></div>
              <div className="h-7 w-16 bg-gray-200"></div>
            </div>
          ))}
        </div>
      ) : (
        analytics && (
          <>
            {/* Summary Stats Cards */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
              <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
                <div className="flex justify-between items-center mb-4">
                  <h3 className="text-gray-500 font-medium">Total Cost</h3>
                  <DollarSign className="h-8 w-8 text-blue-500" />
                </div>
                <p className="text-2xl font-bold">
                  {formatCurrency(analytics.totalCost)}
                </p>
                <p className="text-sm text-gray-500 mt-2">For this period</p>
              </div>

              <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
                <div className="flex justify-between items-center mb-4">
                  <h3 className="text-gray-500 font-medium">Mileage</h3>
                  <ArrowUpDown className="h-8 w-8 text-green-500" />
                </div>
                <p className="text-2xl font-bold">
                  {analytics.mileage.toLocaleString()} miles
                </p>
                <p className="text-sm text-gray-500 mt-2">Distance traveled</p>
              </div>

              <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
                <div className="flex justify-between items-center mb-4">
                  <h3 className="text-gray-500 font-medium">Cost Per Mile</h3>
                  <DollarSign className="h-8 w-8 text-red-500" />
                </div>
                <p className="text-2xl font-bold">
                  ${analytics.costPerMile.toFixed(2)}
                </p>
                <p className="text-sm text-gray-500 mt-2">Operating cost</p>
              </div>

              <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
                <div className="flex justify-between items-center mb-4">
                  <h3 className="text-gray-500 font-medium">Fuel Economy</h3>
                  <Droplet className="h-8 w-8 text-purple-500" />
                </div>
                <p className="text-2xl font-bold">
                  {analytics.fuelEfficiency.toFixed(1)} MPG
                </p>
                <p className="text-sm text-gray-500 mt-2">Average efficiency</p>
              </div>
            </div>

            {/* Charts */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
              {/* Monthly Costs Chart */}
              <div className="bg-white rounded-lg shadow-md p-6">
                <h3 className="text-lg font-semibold mb-4">Monthly Expenses</h3>
                {analytics.costByMonth && analytics.costByMonth.length > 0 ? (
                  <ResponsiveContainer width="100%" height={300}>
                    <BarChart data={analytics.costByMonth}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis
                        dataKey="month"
                        tickFormatter={(value) => {
                          const date = new Date(value + "-01");
                          return date.toLocaleDateString("en-US", {
                            month: "short",
                            year: "2-digit",
                          });
                        }}
                      />
                      <YAxis tickFormatter={(value) => `$${value}`} />
                      <Tooltip formatter={(value) => `$${value}`} />
                      <Legend />
                      <Bar dataKey="cost" name="Total Cost" fill="#0088FE" />
                    </BarChart>
                  </ResponsiveContainer>
                ) : (
                  <div className="flex items-center justify-center h-60 bg-gray-50 rounded">
                    <p className="text-gray-500">
                      No monthly expense data available
                    </p>
                  </div>
                )}
              </div>

              {/* Fuel Efficiency Chart */}
              <div className="bg-white rounded-lg shadow-md p-6">
                <h3 className="text-lg font-semibold mb-4">
                  Fuel Efficiency Trend
                </h3>
                {analytics.fuelEfficiencyTrend &&
                analytics.fuelEfficiencyTrend.length > 0 ? (
                  <ResponsiveContainer width="100%" height={300}>
                    <LineChart data={analytics.fuelEfficiencyTrend}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis
                        dataKey="date"
                        tickFormatter={(value) => {
                          const date = new Date(value);
                          return date.toLocaleDateString("en-US", {
                            month: "short",
                            year: "2-digit",
                          });
                        }}
                      />
                      <YAxis />
                      <Tooltip formatter={(value) => `${value} MPG`} />
                      <Legend />
                      <Line
                        type="monotone"
                        dataKey="mpg"
                        name="MPG"
                        stroke="#8884d8"
                        activeDot={{ r: 8 }}
                      />
                    </LineChart>
                  </ResponsiveContainer>
                ) : (
                  <div className="flex items-center justify-center h-60 bg-gray-50 rounded">
                    <p className="text-gray-500">
                      No fuel efficiency data available
                    </p>
                  </div>
                )}
              </div>

              {/* Cost Breakdown Pie Chart */}
              <div className="bg-white rounded-lg shadow-md p-6">
                <h3 className="text-lg font-semibold mb-4">Cost Breakdown</h3>
                {analytics.costByCategory &&
                (analytics.costByCategory.fuel > 0 ||
                  analytics.costByCategory.maintenance > 0 ||
                  analytics.costByCategory.repairs > 0) ? (
                  <ResponsiveContainer width="100%" height={300}>
                    <PieChart>
                      <Pie
                        data={[
                          {
                            name: "Fuel",
                            value: analytics.costByCategory.fuel,
                          },
                          {
                            name: "Maintenance",
                            value: analytics.costByCategory.maintenance,
                          },
                          {
                            name: "Repairs",
                            value: analytics.costByCategory.repairs,
                          },
                        ]}
                        cx="50%"
                        cy="50%"
                        labelLine={false}
                        label={({ name, percent }) =>
                          `${name}: ${(percent * 100).toFixed(0)}%`
                        }
                        outerRadius={100}
                        fill="#8884d8"
                        dataKey="value"
                      >
                        {[
                          {
                            name: "Fuel",
                            value: analytics.costByCategory.fuel,
                          },
                          {
                            name: "Maintenance",
                            value: analytics.costByCategory.maintenance,
                          },
                          {
                            name: "Repairs",
                            value: analytics.costByCategory.repairs,
                          },
                        ].map((entry, index) => (
                          <Cell
                            key={`cell-${index}`}
                            fill={COLORS[index % COLORS.length]}
                          />
                        ))}
                      </Pie>
                      <Tooltip formatter={(value) => `$${value.toFixed(2)}`} />
                    </PieChart>
                  </ResponsiveContainer>
                ) : (
                  <div className="flex items-center justify-center h-60 bg-gray-50 rounded">
                    <p className="text-gray-500">
                      No cost breakdown data available
                    </p>
                  </div>
                )}
              </div>

              {/* Cumulative Cost Trend */}
              <div className="bg-white rounded-lg shadow-md p-6">
                <h3 className="text-lg font-semibold mb-4">Cumulative Costs</h3>
                {analytics.costByMonth && analytics.costByMonth.length > 0 ? (
                  <ResponsiveContainer width="100%" height={300}>
                    <AreaChart
                      data={analytics.costByMonth.map((month, index, arr) => ({
                        month: month.month,
                        cumulativeCost: arr
                          .slice(0, index + 1)
                          .reduce((sum, m) => sum + m.cost, 0),
                      }))}
                    >
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis
                        dataKey="month"
                        tickFormatter={(value) => {
                          const date = new Date(value + "-01");
                          return date.toLocaleDateString("en-US", {
                            month: "short",
                            year: "2-digit",
                          });
                        }}
                      />
                      <YAxis tickFormatter={(value) => `$${value}`} />
                      <Tooltip formatter={(value) => `$${value.toFixed(2)}`} />
                      <Area
                        type="monotone"
                        dataKey="cumulativeCost"
                        name="Cumulative Cost"
                        stroke="#4f46e5"
                        fill="#4f46e5"
                        fillOpacity={0.2}
                      />
                    </AreaChart>
                  </ResponsiveContainer>
                ) : (
                  <div className="flex items-center justify-center h-60 bg-gray-50 rounded">
                    <p className="text-gray-500">
                      No cumulative cost data available
                    </p>
                  </div>
                )}
              </div>
            </div>

            {/* Upcoming Maintenance */}
            <div className="bg-white rounded-lg shadow-md p-6 mb-8">
              <h3 className="text-lg font-semibold mb-4">
                Upcoming Maintenance
              </h3>
              {analytics.upcomingMaintenance &&
              analytics.upcomingMaintenance.length > 0 ? (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  {analytics.upcomingMaintenance.map((item, index) => (
                    <div
                      key={index}
                      className={`p-4 rounded-lg flex items-start ${getMaintenanceStatusColor(item.dueDate)}`}
                    >
                      <Calendar className="h-5 w-5 mr-3 mt-1" />
                      <div>
                        <h4 className="font-medium">{item.type}</h4>
                        <p className="text-sm">{formatDate(item.dueDate)}</p>
                        <p className="text-sm font-semibold">
                          {formatCurrency(item.estimatedCost)}
                        </p>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-gray-500 text-center py-8">
                  No upcoming maintenance scheduled
                </p>
              )}
            </div>

            {/* Statistics Summary */}
            <div className="bg-white rounded-lg shadow-md p-6">
              <h3 className="text-lg font-semibold mb-4">
                Detailed Statistics
              </h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                  <dl className="space-y-4">
                    <div className="flex justify-between">
                      <dt className="text-gray-500">Total Trips</dt>
                      <dd className="font-medium">{analytics.totalTrips}</dd>
                    </div>
                    <div className="flex justify-between">
                      <dt className="text-gray-500">Fuel Costs</dt>
                      <dd className="font-medium">
                        {formatCurrency(analytics.fuelCosts)}
                      </dd>
                    </div>
                    <div className="flex justify-between">
                      <dt className="text-gray-500">Maintenance Costs</dt>
                      <dd className="font-medium">
                        {formatCurrency(analytics.maintenanceCosts)}
                      </dd>
                    </div>
                    <div className="flex justify-between">
                      <dt className="text-gray-500">Average Cost per Trip</dt>
                      <dd className="font-medium">
                        {formatCurrency(
                          analytics.totalTrips > 0
                            ? analytics.totalCost / analytics.totalTrips
                            : 0,
                        )}
                      </dd>
                    </div>
                  </dl>
                </div>
                <div>
                  <dl className="space-y-4">
                    <div className="flex justify-between">
                      <dt className="text-gray-500">Period Length</dt>
                      <dd className="font-medium">
                        {Math.ceil(
                          (new Date(dateRange.endDate).getTime() -
                            new Date(dateRange.startDate).getTime()) /
                            (1000 * 60 * 60 * 24),
                        )}{" "}
                        days
                      </dd>
                    </div>
                    <div className="flex justify-between">
                      <dt className="text-gray-500">Daily Cost</dt>
                      <dd className="font-medium">
                        {formatCurrency(
                          analytics.totalCost /
                            Math.max(
                              1,
                              Math.ceil(
                                (new Date(dateRange.endDate).getTime() -
                                  new Date(dateRange.startDate).getTime()) /
                                  (1000 * 60 * 60 * 24),
                              ),
                            ),
                        )}
                      </dd>
                    </div>
                    <div className="flex justify-between">
                      <dt className="text-gray-500">Current Vehicle Mileage</dt>
                      <dd className="font-medium">
                        {vehicle
                          ? vehicle.currentMileage.toLocaleString()
                          : "Unknown"}{" "}
                        miles
                      </dd>
                    </div>
                    <div className="flex justify-between">
                      <dt className="text-gray-500">Yearly Cost (Projected)</dt>
                      <dd className="font-medium">
                        {formatCurrency(
                          (analytics.totalCost /
                            Math.max(
                              1,
                              (new Date(dateRange.endDate).getTime() -
                                new Date(dateRange.startDate).getTime()) /
                                (1000 * 60 * 60 * 24),
                            )) *
                            365,
                        )}
                      </dd>
                    </div>
                  </dl>
                </div>
              </div>
            </div>
          </>
        )
      )}
    </div>
  );
};

export default VehicleAnalytics;
