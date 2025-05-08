// src/types/index.ts
export interface StatsCard {
  title: string;
  value: number | string;
  icon: React.ReactNode;
}

export interface ChartData {
  name: string;
  fuel?: number;
  maintenance?: number;
  mpg?: number;
}

export interface Vehicle {
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
  date: string; // Keep as string (ISO format from API)
  provider: string;
  nextServiceDue: string | null; // Keep as string or null
  createdOn: string;
  vehicleId: number; // ADD this
}

export interface FuelRecord {
  id: number;
  date: string;
  gallons: number;
  costPerGallon: number;
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
