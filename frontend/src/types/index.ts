import React from "react";

export interface StatsCard {
  title: string;
  value: number | string;
  icon: React.ReactNode;
}

export interface ChartData {
  name: string;
  fuel?: number;
  maintenance?: number;
  L100km?: number;
}

export interface Vehicle {
  createdAt: Date;
  id: number;
  make: string;
  model: string;
  year: number;
  licensePlate: string;
  description: string;
  currentMileage: number;
  createdOn?: string;
}

export interface Trip {
  id: number;
  startLocation: string;
  endLocation: string;
  distance: number;
  startTime: string;
  endTime: string;
  purpose: string;
  fuelUsed: number | null;
  driverId?: string;
  createdOn?: string;
  vehicleId: number;
}

export interface MaintenanceRecord {
  id: number;
  serviceType: string;
  description: string;
  cost: number;
  mileage: number;
  date: string;
  provider: string;
  nextServiceDue: string | null;
  createdOn: string;
  vehicleId: number;
}

export interface FuelRecord {
  id: number;
  date: string;
  liters: number;
  costPerLiter: number;
  totalCost: number;
  mileage: number;
  station: string;
  fullTank: boolean;
  createdOn: string;
  vehicleId: number;
}

export interface Pagination {
  currentPage: number;
  pageSize: number;
  totalPages: number;
  totalCount: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface VehicleAnalytics {
  mileage: number;
  costPerKm: number;
  fuelEfficiencyLitersPer100Km: number;
}
export interface FuelEfficiencyTrendDto {
  date: string;
  litersPer100Km: number;
}
