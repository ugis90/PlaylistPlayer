import React, { useState, useEffect } from "react";
import {
  BarChart,
  Bar,
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
  Car,
  Droplet,
  Settings,
  Activity,
  AlertTriangle,
  RefreshCw,
  TrendingUp,
  Euro,
  MapPin,
  Clock,
  ChevronRight,
} from "lucide-react";
import { Link } from "react-router-dom";
import apiClient from "../api/client";
import { Vehicle, Trip, FuelRecord, MaintenanceRecord } from "../types";

interface UpcomingMaintenance {
  vehicleId: number;
  vehicleName: string;
  serviceType: string;
  dueDate: string;
  daysDue: number;
  estimatedCost: number;
}

interface MonthlyExpense {
  name: string;
  fuel: number;
  maintenance: number;
}

interface FuelEfficiencyDataPoint {
  name: string;
  litersPer100Km: number;
}

interface VehicleStats {
  totalVehicles: number;
  totalTrips: number;
  totalFuelCost: number;
  totalMaintenance: number;
  totalDistance: number;
  avgL100km: number;
  totalLiters: number;
}

interface VehicleUsageData {
  name: string;
  value: number;
}

const Dashboard: React.FC = () => {
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [stats, setStats] = useState<VehicleStats>({
    totalLiters: 0,
    totalVehicles: 0,
    totalTrips: 0,
    totalFuelCost: 0,
    totalMaintenance: 0,
    totalDistance: 0,
    avgL100km: 0,
  });
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [vehicleCostData, setVehicleCostData] = useState<MonthlyExpense[]>([]);
  const [fuelEfficiencyData, setFuelEfficiencyData] = useState<
    FuelEfficiencyDataPoint[]
  >([]);
  const [vehicleUsageData, setVehicleUsageData] = useState<VehicleUsageData[]>(
    [],
  );
  const [upcomingMaintenance, setUpcomingMaintenance] = useState<
    UpcomingMaintenance[]
  >([]);
  const [recentTrips, setRecentTrips] = useState<Trip[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [failedVehicles, setFailedVehicles] = useState<number>(0);

  const COLORS = [
    "#0088FE",
    "#00C49F",
    "#FFBB28",
    "#FF8042",
    "#8884d8",
    "#4CAF50",
    "#3F51B5",
    "#F44336",
  ];

  useEffect(() => {
    fetchDashboardData();
  }, []);

  const extractResourcesFromResponse = (data: any): any[] => {
    if (Array.isArray(data)) {
      return data;
    } else if (data?.resources) {
      return data.resources.map((item: any) => item.resource || item);
    } else if (data?.resource) {
      if (Array.isArray(data.resource)) {
        return data.resource.map((item: any) => item.resource || item);
      } else {
        return [data.resource];
      }
    }
    if (typeof data === "object" && data !== null) {
      for (const key in data) {
        if (Array.isArray(data[key])) {
          console.warn(`Extracting data from unexpected key: ${key}`);
          return data[key];
        }
      }
    }
    console.warn("Could not extract resources from response:", data);
    return [];
  };

  const fetchDashboardData = async () => {
    setIsLoading(true);
    setError(null);
    let tempFailedVehicles = 0;

    try {
      const vehiclesResponse = await apiClient.get("/vehicles?pageSize=500");
      const vehicleData: Vehicle[] = extractResourcesFromResponse(
        vehiclesResponse.data,
      );

      const validVehicles = vehicleData.filter(
        (v) => v && v.id && v.make !== undefined && v.model !== undefined,
      );
      console.log("Fetched vehicles for Dashboard (Parsed):", validVehicles);
      setVehicles(validVehicles);

      if (!validVehicles.length) {
        setError(
          "No vehicles found. Please add a vehicle to see dashboard data.",
        );
        setIsLoading(false);
        return;
      }

      const allTrips: Trip[] = [];
      const allFuelRecords: FuelRecord[] = [];
      const allMaintenanceRecords: MaintenanceRecord[] = [];

      const vehiclePromises = validVehicles.map(async (vehicle) => {
        try {
          const [tripsResponse, fuelResponse, maintenanceResponse] =
            await Promise.all([
              apiClient.get(`/vehicles/${vehicle.id}/trips?pageSize=500`),
              apiClient.get(`/vehicles/${vehicle.id}/fuelRecords?pageSize=500`),
              apiClient.get(
                `/vehicles/${vehicle.id}/maintenanceRecords?pageSize=500`,
              ),
            ]);

          const vehicleTrips = extractResourcesFromResponse(tripsResponse.data);
          allTrips.push(...vehicleTrips);

          const vehicleFuelRecords = extractResourcesFromResponse(
            fuelResponse.data,
          );
          allFuelRecords.push(...vehicleFuelRecords);

          const vehicleMaintenanceRecords = extractResourcesFromResponse(
            maintenanceResponse.data,
          ).map((rec) => ({ ...rec, vehicleId: vehicle.id }));
          allMaintenanceRecords.push(...vehicleMaintenanceRecords);
        } catch (err: any) {
          console.error(
            `Error fetching data for vehicle ${vehicle.id}:`,
            err.message,
          );
          tempFailedVehicles++;
        }
      });

      await Promise.all(vehiclePromises);
      setFailedVehicles(tempFailedVehicles);

      const sortedTrips = allTrips
        .sort(
          (a, b) =>
            new Date(b.startTime).getTime() - new Date(a.startTime).getTime(),
        )
        .slice(0, 5);
      setRecentTrips(sortedTrips);

      processAllData(
        validVehicles,
        allTrips,
        allFuelRecords,
        allMaintenanceRecords,
      );
    } catch (err: any) {
      console.error("Error fetching dashboard data:", err);
      setError(
        err.message || "Failed to load dashboard data. Please try again later.",
      );
    } finally {
      setIsLoading(false);
    }
  };

  const processAllData = (
    vehiclesData: Vehicle[],
    trips: Trip[],
    fuelRecords: FuelRecord[],
    maintenanceRecords: MaintenanceRecord[],
  ) => {
    const tripsInPeriod = trips;
    const fuelRecordsInPeriod = fuelRecords;
    const maintenanceRecordsInPeriod = maintenanceRecords;

    const totalVehicles = vehiclesData.length;
    const totalTrips = tripsInPeriod.length;
    const totalDistance = tripsInPeriod.reduce(
      (sum, trip) => sum + (trip.distance || 0),
      0,
    );
    const totalFuelCost = fuelRecordsInPeriod.reduce(
      (sum, record) => sum + (record.totalCost || 0),
      0,
    );
    const totalMaintenance = maintenanceRecordsInPeriod.reduce(
      (sum, record) => sum + (record.cost || 0),
      0,
    );

    const totalLitersConsumedInPeriod = fuelRecordsInPeriod.reduce(
      (sum, record) => sum + (record.liters || 0),
      0,
    );

    const fuelRecordsByVehicle = fuelRecordsInPeriod.reduce(
      (acc, record) => {
        if (!acc[record.vehicleId]) acc[record.vehicleId] = [];
        acc[record.vehicleId].push(record);
        return acc;
      },
      {} as Record<number, FuelRecord[]>,
    );

    let totalKmForEfficiency = 0;
    let totalLitersForEfficiency = 0;

    Object.values(fuelRecordsByVehicle).forEach((records) => {
      if (records.length < 2) return;
      const sortedRecords = [...records].sort((a, b) => a.mileage - b.mileage);
      const fullTankSegments = [];
      for (let i = 0; i < sortedRecords.length - 1; i++) {
        if (sortedRecords[i].fullTank && sortedRecords[i + 1].fullTank) {
          fullTankSegments.push({
            previous: sortedRecords[i],
            current: sortedRecords[i + 1],
          });
        }
      }

      const segmentsToUse = fullTankSegments;
      if (segmentsToUse.length === 0 && sortedRecords.length >= 2) {
        for (let i = 0; i < sortedRecords.length - 1; i++) {
          segmentsToUse.push({
            previous: sortedRecords[i],
            current: sortedRecords[i + 1],
          });
        }
      }

      for (const segment of segmentsToUse) {
        const distance = segment.current.mileage - segment.previous.mileage;
        const litersConsumed = segment.current.liters;

        if (distance > 0 && distance < 2000 && litersConsumed > 0) {
          totalKmForEfficiency += distance;
          totalLitersForEfficiency += litersConsumed;
        }
      }
    });

    const overallAvgL100km =
      totalLitersForEfficiency > 0 && totalKmForEfficiency > 0
        ? (totalLitersForEfficiency / totalKmForEfficiency) * 100
        : 0;

    setStats({
      totalVehicles,
      totalTrips,
      totalFuelCost,
      totalMaintenance,
      totalDistance,
      avgL100km: Math.round(overallAvgL100km * 10) / 10,
      totalLiters: totalLitersConsumedInPeriod,
    });
    generateChartData(
      vehiclesData,
      tripsInPeriod,
      fuelRecordsInPeriod,
      maintenanceRecordsInPeriod,
    );
    generateUpcomingMaintenance(vehiclesData, maintenanceRecords);
  };

  const generateChartData = (
    vehiclesData: Vehicle[],
    trips: Trip[],
    fuelRecords: FuelRecord[],
    maintenanceRecords: MaintenanceRecord[],
  ) => {
    setVehicleCostData(
      generateMonthlyCostData(fuelRecords, maintenanceRecords),
    );
    setFuelEfficiencyData(generateFuelEfficiencyData(fuelRecords));
    setVehicleUsageData(generateVehicleUsageData(vehiclesData, trips));
  };

  const generateMonthlyCostData = (
    fuelRecords: FuelRecord[],
    maintenanceRecords: MaintenanceRecord[],
  ): MonthlyExpense[] => {
    const monthsMap = new Map<string, MonthlyExpense>();
    const today = new Date();
    for (let i = 5; i >= 0; i--) {
      const monthDate = new Date(today.getFullYear(), today.getMonth() - i, 1);
      const monthKey = `${monthDate.getFullYear()}-${String(monthDate.getMonth() + 1).padStart(2, "0")}`;
      const monthName = monthDate.toLocaleDateString("en-US", {
        month: "short",
        year: "numeric",
      });
      monthsMap.set(monthKey, { name: monthName, fuel: 0, maintenance: 0 });
    }
    fuelRecords.forEach((record) => {
      if (!record.date) return;
      try {
        const recordDate = new Date(record.date);
        const monthKey = `${recordDate.getFullYear()}-${String(recordDate.getMonth() + 1).padStart(2, "0")}`;
        if (monthsMap.has(monthKey))
          monthsMap.get(monthKey)!.fuel += record.totalCost || 0;
      } catch (e) {
        console.error("Error processing fuel record date:", e);
      }
    });
    maintenanceRecords.forEach((record) => {
      if (!record.date) return;
      try {
        const recordDate = new Date(record.date);
        const monthKey = `${recordDate.getFullYear()}-${String(recordDate.getMonth() + 1).padStart(2, "0")}`;
        if (monthsMap.has(monthKey))
          monthsMap.get(monthKey)!.maintenance += record.cost || 0;
      } catch (e) {
        console.error("Error processing maintenance record date:", e);
      }
    });
    return Array.from(monthsMap.entries())
      .sort(([keyA], [keyB]) => keyA.localeCompare(keyB))
      .map(([, monthData]) => ({
        ...monthData,
        fuel: Math.round(monthData.fuel * 100) / 100,
        maintenance: Math.round(monthData.maintenance * 100) / 100,
      }));
  };

  const generateFuelEfficiencyData = (
    fuelRecords: FuelRecord[],
  ): FuelEfficiencyDataPoint[] => {
    if (fuelRecords.length < 2) return [];
    const sortedRecords = [...fuelRecords].sort(
      (a, b) => new Date(a.date).getTime() - new Date(b.date).getTime(),
    );
    const vehicleMonthlyData = new Map<string, FuelRecord[]>();
    sortedRecords.forEach((record) => {
      if (!record.date || !record.vehicleId) return;
      try {
        const date = new Date(record.date);
        const monthKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;
        const vehicleMonthKey = `${record.vehicleId}-${monthKey}`;
        if (!vehicleMonthlyData.has(vehicleMonthKey))
          vehicleMonthlyData.set(vehicleMonthKey, []);
        vehicleMonthlyData.get(vehicleMonthKey)!.push(record);
      } catch (e) {
        console.error("Error processing fuel record date for efficiency:", e);
      }
    });

    const monthlyEfficiencyMap = new Map<
      string,
      { monthName: string; totalKm: number; totalLiters: number }
    >();

    vehicleMonthlyData.forEach((recordsForVehicleInMonth) => {
      if (recordsForVehicleInMonth.length < 2) return;
      const sortedRecords = [...recordsForVehicleInMonth].sort(
        (a, b) => a.mileage - b.mileage,
      );

      let monthKey = "";
      if (sortedRecords.length > 0) {
        const date = new Date(sortedRecords[0].date);
        monthKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;
      }
      if (!monthKey) return;

      if (!monthlyEfficiencyMap.has(monthKey)) {
        const date = new Date(sortedRecords[0].date);
        monthlyEfficiencyMap.set(monthKey, {
          monthName: date.toLocaleDateString("en-US", {
            month: "short",
            year: "numeric",
          }),
          totalKm: 0,
          totalLiters: 0,
        });
      }
      const monthStat = monthlyEfficiencyMap.get(monthKey)!;

      for (let i = 1; i < sortedRecords.length; i++) {
        const current = sortedRecords[i];
        const previous = sortedRecords[i - 1];
        if (current.fullTank && previous.fullTank) {
          const distKm = current.mileage - previous.mileage;
          const litersUsed = current.liters;
          if (distKm > 0 && distKm < 2000 && litersUsed > 0) {
            monthStat.totalKm += distKm;
            monthStat.totalLiters += litersUsed;
          }
        }
      }
    });

    const trendData: FuelEfficiencyDataPoint[] = [];
    Array.from(monthlyEfficiencyMap.values())
      .sort((a, b) => a.monthName.localeCompare(b.monthName))
      .forEach((data) => {
        if (data.totalLiters > 0 && data.totalKm > 0) {
          const l100km = (data.totalLiters / data.totalKm) * 100;
          trendData.push({
            name: data.monthName,
            litersPer100Km: Math.round(l100km * 10) / 10,
          });
        }
      });
    return trendData;
  };

  const generateVehicleUsageData = (
    vehiclesData: Vehicle[],
    trips: Trip[],
  ): VehicleUsageData[] => {
    return vehiclesData
      .map((vehicle) => {
        const vehicleTrips = trips.filter(
          (trip) => trip.vehicleId === vehicle.id,
        );
        return {
          name: `${vehicle.make} ${vehicle.model}`,
          value: vehicleTrips.length || 0,
        };
      })
      .filter((data) => data.value > 0);
  };

  const generateUpcomingMaintenance = (
    vehiclesData: Vehicle[],
    allMaintenanceRecords: MaintenanceRecord[],
  ) => {
    const upcoming: UpcomingMaintenance[] = [];
    const today = new Date();

    vehiclesData.forEach((vehicle) => {
      const vehicleRecords = allMaintenanceRecords
        .filter((rec) => rec.vehicleId === vehicle.id)
        .sort(
          (a, b) => new Date(b.date).getTime() - new Date(a.date).getTime(),
        );

      const latestServiceByType = new Map<string, MaintenanceRecord>();
      vehicleRecords.forEach((rec) => {
        if (!latestServiceByType.has(rec.serviceType)) {
          latestServiceByType.set(rec.serviceType, rec);
        }
      });

      latestServiceByType.forEach((latestRecord, serviceType) => {
        let dueDate: Date | null = null;
        if (latestRecord.nextServiceDue) {
          dueDate = new Date(latestRecord.nextServiceDue);
        } else {
          let intervalDays = 90;
          if (serviceType.toLowerCase().includes("oil")) intervalDays = 90;
          else if (serviceType.toLowerCase().includes("tire"))
            intervalDays = 180;
          else if (serviceType.toLowerCase().includes("brake"))
            intervalDays = 365;
          else if (serviceType.toLowerCase().includes("inspection"))
            intervalDays = 365;
          else intervalDays = 180;

          const lastServiceDate = new Date(latestRecord.date);
          dueDate = new Date(
            lastServiceDate.setDate(lastServiceDate.getDate() + intervalDays),
          );
        }

        if (dueDate && dueDate > today) {
          const daysDue = Math.ceil(
            (dueDate.getTime() - today.getTime()) / (1000 * 60 * 60 * 24),
          );
          if (daysDue <= 100) {
            upcoming.push({
              vehicleId: vehicle.id,
              vehicleName: `${vehicle.make} ${vehicle.model}`,
              serviceType: serviceType,
              dueDate: dueDate.toISOString(),
              daysDue: daysDue,
              estimatedCost: latestRecord.cost,
            });
          }
        }
      });

      const ageInMonths =
        (today.getFullYear() - vehicle.year) * 12 +
        (today.getMonth() - new Date(vehicle.createdAt ?? today).getMonth());

      if (
        !latestServiceByType.has("Oil Change") &&
        (ageInMonths % 3 === 0 || vehicle.currentMileage % 3000 <= 500)
      ) {
        const estDueDate = new Date(today);
        estDueDate.setDate(today.getDate() + 30);
        upcoming.push({
          vehicleId: vehicle.id,
          vehicleName: `${vehicle.make} ${vehicle.model}`,
          serviceType: "Oil Change (Est.)",
          dueDate: estDueDate.toISOString(),
          daysDue: 30,
          estimatedCost: 50,
        });
      }
      if (
        !latestServiceByType.has("Tire Rotation") &&
        (ageInMonths % 6 === 0 || vehicle.currentMileage % 6000 <= 500)
      ) {
        const estDueDate = new Date(today);
        estDueDate.setDate(today.getDate() + 45);
        upcoming.push({
          vehicleId: vehicle.id,
          vehicleName: `${vehicle.make} ${vehicle.model}`,
          serviceType: "Tire Rotation (Est.)",
          dueDate: estDueDate.toISOString(),
          daysDue: 45,
          estimatedCost: 30,
        });
      }
    });

    upcoming.sort((a, b) => a.daysDue - b.daysDue);
    setUpcomingMaintenance(upcoming);
  };

  const formatDate = (dateString: string) => {
    try {
      return new Date(dateString).toLocaleDateString("en-US", {
        year: "numeric",
        month: "short",
        day: "numeric",
      });
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
    } catch (e) {
      return "Invalid date";
    }
  };

  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat("lt-LT", {
      style: "currency",
      currency: "EUR",
    }).format(value);
  };

  return (
    <div className="container mx-auto px-4 py-8">
      {/* Header */}
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold">Family Fleet Dashboard</h1>
        <button
          onClick={fetchDashboardData}
          className="flex items-center bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg"
          disabled={isLoading}
        >
          <RefreshCw
            className={`h-5 w-5 mr-2 ${isLoading ? "animate-spin" : ""}`}
          />{" "}
          Refresh Dashboard
        </button>
      </div>

      {/* Error Display */}
      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-6">
          {" "}
          <p className="flex items-center">
            <AlertTriangle className="h-5 w-5 mr-2" /> {error}
          </p>{" "}
          {vehicles.length === 0 && (
            <p className="mt-2">
              <Link to="/vehicles" className="text-blue-600 hover:underline">
                Click here to add a vehicle
              </Link>
            </p>
          )}{" "}
        </div>
      )}

      {/* Loading State */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          {" "}
          {[1, 2, 3, 4].map((i) => (
            <div
              key={i}
              className="bg-white rounded-lg shadow-md p-6 animate-pulse"
            >
              {" "}
              <div className="h-8 w-8 rounded-full bg-gray-200 mb-4"></div>{" "}
              <div className="h-5 w-24 bg-gray-200 mb-2"></div>{" "}
              <div className="h-7 w-16 bg-gray-200"></div>{" "}
            </div>
          ))}{" "}
        </div>
      ) : vehicles.length > 0 && !error ? (
        <>
          {/* Stats Cards */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
            <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
              {" "}
              <div className="flex justify-between items-center mb-4">
                <h3 className="text-gray-500 font-medium">Total Vehicles</h3>
                <Car className="h-8 w-8 text-blue-500" />
              </div>{" "}
              <p className="text-2xl font-bold">{stats.totalVehicles}</p>{" "}
            </div>
            <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
              {" "}
              <div className="flex justify-between items-center mb-4">
                <h3 className="text-gray-500 font-medium">Total Trips</h3>
                <Activity className="h-8 w-8 text-green-500" />
              </div>{" "}
              <p className="text-2xl font-bold">{stats.totalTrips}</p>{" "}
            </div>
            <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
              {" "}
              <div className="flex justify-between items-center mb-4">
                <h3 className="text-gray-500 font-medium">Fuel Expenses</h3>
                <Droplet className="h-8 w-8 text-red-500" />
              </div>{" "}
              <p className="text-2xl font-bold">
                {formatCurrency(stats.totalFuelCost)}
              </p>{" "}
            </div>
            <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
              {" "}
              <div className="flex justify-between items-center mb-4">
                <h3 className="text-gray-500 font-medium">Maintenance Costs</h3>
                <Settings className="h-8 w-8 text-purple-500" />
              </div>{" "}
              <p className="text-2xl font-bold">
                {formatCurrency(stats.totalMaintenance)}
              </p>{" "}
            </div>
          </div>

          {/* Performance Metrics */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
            <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
              {" "}
              <div className="flex justify-between items-center mb-4">
                <h3 className="text-gray-500 font-medium">Total Distance</h3>
                <MapPin className="h-8 w-8 text-indigo-500" />
              </div>{" "}
              <p className="text-2xl font-bold">
                {stats.totalDistance.toFixed(1)} km
              </p>{" "}
            </div>
            <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
              {" "}
              <div className="flex justify-between items-center mb-4">
                <h3 className="text-gray-500 font-medium">Average L/100km</h3>
                <TrendingUp className="h-8 w-8 text-teal-500" />
              </div>{" "}
              <p className="text-2xl font-bold">
                {stats.avgL100km.toFixed(1)} L/100km
              </p>{" "}
            </div>
            <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
              {" "}
              <div className="flex justify-between items-center mb-4">
                <h3 className="text-gray-500 font-medium">Total Cost</h3>
                <Euro className="h-8 w-8 text-amber-500" />
              </div>{" "}
              <p className="text-2xl font-bold">
                {formatCurrency(stats.totalFuelCost + stats.totalMaintenance)}
              </p>{" "}
            </div>
            <div className="bg-white rounded-lg shadow-md p-6 transition-transform hover:scale-105">
              {" "}
              <div className="flex justify-between items-center mb-4">
                <h3 className="text-gray-500 font-medium">Cost Per Liter</h3>
                <Clock className="h-8 w-8 text-rose-500" />
              </div>{" "}
              <p className="text-2xl font-bold">
                {stats.totalLiters > 0
                  ? formatCurrency(stats.totalFuelCost / stats.totalLiters)
                  : formatCurrency(0)}
              </p>{" "}
            </div>
          </div>

          {/* Charts */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
            {/* Monthly Costs Chart */}
            <div className="bg-white rounded-lg shadow-md p-6">
              {" "}
              <h3 className="text-lg font-semibold mb-4">
                Monthly Expenses
              </h3>{" "}
              {vehicleCostData.length > 0 ? (
                <ResponsiveContainer width="100%" height={300}>
                  {" "}
                  <BarChart data={vehicleCostData}>
                    {" "}
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" />
                    <YAxis />{" "}
                    <Tooltip
                      formatter={(value) => formatCurrency(value as number)}
                    />{" "}
                    <Legend />
                    <Bar
                      dataKey="fuel"
                      name="Fuel Costs"
                      fill="#0088FE"
                      stackId="a"
                    />{" "}
                    <Bar
                      dataKey="maintenance"
                      name="Maintenance"
                      fill="#00C49F"
                      stackId="a"
                    />{" "}
                  </BarChart>{" "}
                </ResponsiveContainer>
              ) : (
                <div className="flex items-center justify-center h-60 bg-gray-50 rounded">
                  <p className="text-gray-500">No expense data</p>
                </div>
              )}{" "}
            </div>
            {/* Fuel Efficiency Chart */}
            <div className="bg-white rounded-lg shadow-md p-6">
              {" "}
              <h3 className="text-lg font-semibold mb-4">
                Fuel Efficiency Trend
              </h3>{" "}
              {fuelEfficiencyData.length > 0 ? (
                <ResponsiveContainer width="100%" height={300}>
                  {" "}
                  <AreaChart data={fuelEfficiencyData}>
                    {" "}
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" />
                    <YAxis
                      label={{
                        value: "L/100km",
                        angle: -90,
                        position: "insideLeft",
                        offset: -5,
                      }}
                      domain={["dataMin - 2", "dataMax + 2"]}
                      tickFormatter={(value) =>
                        typeof value === "number" ? value.toFixed(1) : value
                      }
                    />{" "}
                    <Tooltip
                      formatter={(value) =>
                        `${typeof value === "number" ? value.toFixed(1) : value} L/100km`
                      }
                    />{" "}
                    <Legend />{" "}
                    <Area
                      type="monotone"
                      dataKey="litersPer100Km"
                      name="L/100km"
                      stroke="#8884d8"
                      fill="#8884d8"
                      fillOpacity={0.3}
                      activeDot={{ r: 8 }}
                    />{" "}
                  </AreaChart>{" "}
                </ResponsiveContainer>
              ) : (
                <div className="flex items-center justify-center h-60 bg-gray-50 rounded">
                  <p className="text-gray-500">No efficiency data</p>
                </div>
              )}{" "}
            </div>
            {/* Vehicle Usage Distribution */}
            <div className="bg-white rounded-lg shadow-md p-6">
              {" "}
              <h3 className="text-lg font-semibold mb-4">
                Vehicle Usage (by Trips)
              </h3>{" "}
              {vehicleUsageData.length > 0 ? (
                <ResponsiveContainer width="100%" height={300}>
                  {" "}
                  <PieChart>
                    {" "}
                    <Pie
                      data={vehicleUsageData}
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
                      {" "}
                      {vehicleUsageData.map((entry, index) => (
                        <Cell
                          key={`cell-${index}`}
                          fill={COLORS[index % COLORS.length]}
                        />
                      ))}{" "}
                    </Pie>{" "}
                    <Tooltip formatter={(value) => `${value} trips`} />{" "}
                    <Legend />{" "}
                  </PieChart>{" "}
                </ResponsiveContainer>
              ) : (
                <div className="flex items-center justify-center h-60 bg-gray-50 rounded">
                  <p className="text-gray-500">No usage data</p>
                </div>
              )}{" "}
            </div>
            {/* Upcoming Maintenance */}
            <div className="bg-white rounded-lg shadow-md p-6">
              {" "}
              <h3 className="text-lg font-semibold mb-4">
                Upcoming Maintenance
              </h3>{" "}
              <div className="space-y-4 max-h-80 overflow-y-auto">
                {" "}
                {upcomingMaintenance.length > 0 ? (
                  upcomingMaintenance.map((item, index) => (
                    <div
                      key={index}
                      className={`flex items-center p-3 rounded-lg ${item.daysDue < 7 ? "bg-red-50" : item.daysDue < 30 ? "bg-yellow-50" : "bg-green-50"}`}
                    >
                      {" "}
                      <Calendar
                        className={`h-5 w-5 mr-3 ${item.daysDue < 7 ? "text-red-500" : item.daysDue < 30 ? "text-yellow-500" : "text-green-500"}`}
                      />{" "}
                      <div>
                        {" "}
                        <h4 className="font-medium">{item.serviceType}</h4>{" "}
                        <p className="text-sm text-gray-500">
                          {item.vehicleName} - Due in {item.daysDue} days
                        </p>{" "}
                        <p className="text-xs text-gray-500">
                          {formatDate(item.dueDate)}
                        </p>{" "}
                      </div>{" "}
                      <Link
                        to={`/vehicles/${item.vehicleId}/maintenance`}
                        className="ml-auto text-xs text-blue-600 hover:underline"
                      >
                        {" "}
                        View{" "}
                      </Link>{" "}
                    </div>
                  ))
                ) : (
                  <p className="text-gray-500 text-center py-4">
                    No upcoming maintenance
                  </p>
                )}{" "}
              </div>{" "}
            </div>
          </div>

          {/* Recent Activity */}
          <div className="bg-white rounded-lg shadow-md p-6 mb-8">
            <h3 className="text-lg font-semibold mb-4">Recent Trips</h3>
            {recentTrips.length > 0 ? (
              <div className="overflow-x-auto">
                <table className="w-full table-auto">
                  <thead className="bg-gray-50">
                    <tr>
                      <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Date
                      </th>
                      <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Route
                      </th>
                      <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Purpose
                      </th>
                      <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Distance
                      </th>
                      <th className="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                        Vehicle
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-200">
                    {recentTrips.map((trip) => (
                      <tr key={trip.id} className="hover:bg-gray-50">
                        <td className="px-4 py-3 whitespace-nowrap text-sm">
                          {formatDate(trip.startTime)}
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap text-sm">
                          {trip.startLocation} → {trip.endLocation}
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap text-sm">
                          {trip.purpose || "N/A"}
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap text-sm">
                          {trip.distance?.toFixed(1) || "0"} km
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap text-sm">
                          {vehicles.find((v) => v.id === trip.vehicleId)
                            ? `${vehicles.find((v) => v.id === trip.vehicleId)?.make} ${vehicles.find((v) => v.id === trip.vehicleId)?.model}`
                            : "Unknown"}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="text-gray-500 text-center py-4">No recent trips</p>
            )}
            <div className="flex justify-center mt-4">
              <Link
                to="/vehicles"
                className="text-blue-600 hover:text-blue-800 flex items-center"
              >
                View Vehicles <ChevronRight className="h-4 w-4 ml-1" />
              </Link>
            </div>
          </div>
        </>
      ) : (
        !isLoading &&
        !error && (
          <div className="text-center py-10 text-gray-500">
            <Car className="h-12 w-12 mx-auto text-gray-400 mb-4" />
            <p>No vehicles found for your account.</p>
            <Link
              to="/vehicles"
              className="text-blue-600 hover:underline mt-2 inline-block"
            >
              Add your first vehicle
            </Link>
          </div>
        )
      )}

      {/* API Status */}
      <div className="mt-8 p-4 border rounded bg-gray-100">
        <h3 className="text-lg font-medium mb-2">API Status</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <p className="text-gray-700">
              API Status:{" "}
              {isLoading ? "Loading..." : error ? "Error" : "Connected"}
            </p>
            <p className="text-gray-700">
              Vehicles: {vehicles.length} | Failed Vehicle Requests:{" "}
              {failedVehicles}
            </p>
            <p className="text-gray-700">
              Trips: {stats.totalTrips} | Recent Trips: {recentTrips.length}
            </p>
          </div>
          <div>
            <p className="text-gray-700">
              Charts: {vehicleCostData.length > 0 ? "Expenses ✓" : "Expenses ✗"}
              {fuelEfficiencyData.length > 0 ? " | L/100km ✓" : " | L/100km ✗"}
              {vehicleUsageData.length > 0 ? " | Usage ✓" : " | Usage ✗"}
            </p>
            <p className="text-gray-700">
              Upcoming Maintenance: {upcomingMaintenance.length} items
            </p>
            <button
              onClick={fetchDashboardData}
              className="mt-2 px-3 py-1 bg-blue-500 text-white rounded"
              disabled={isLoading}
            >
              Reload Dashboard
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
