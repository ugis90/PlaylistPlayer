// src/App.tsx
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import { LoginForm } from "./components/LoginForm";
import { RegisterForm } from "./components/RegisterForm";
import { AuthProvider } from "./auth/AuthContext";
import PrivateRoute from "./components/PrivateRoute";
import { ToastProvider } from "./components/Toast";
import Layout from "./components/Layout";
import Dashboard from "./components/Dashboard";
import VehicleList from "./components/VehicleList";
import TripList from "./components/TripList";
import FuelRecords from "./components/FuelRecords";
import MaintenanceRecords from "./components/MaintenanceRecords";
import GpsTracking from "./components/GpsTracking";
import FamilyTracking from "./components/FamilyTracking";
import AdminDashboard from "./components/AdminDashboard";
import { FamilyManagement } from "./components/FamilyManagement";

const router = createBrowserRouter([
  {
    path: "/",
    element: (
      <Layout>
        {" "}
        <PrivateRoute>
          {" "}
          <Dashboard />{" "}
        </PrivateRoute>{" "}
      </Layout>
    ),
  },
  {
    path: "/login",
    element: (
      <Layout>
        {" "}
        <LoginForm />{" "}
      </Layout>
    ),
  },
  {
    path: "/register",
    element: (
      <Layout>
        {" "}
        <RegisterForm />{" "}
      </Layout>
    ),
  },
  {
    path: "/vehicles",
    element: (
      <Layout>
        {" "}
        <PrivateRoute>
          {" "}
          <VehicleList />{" "}
        </PrivateRoute>{" "}
      </Layout>
    ),
  },
  {
    path: "/vehicles/:vehicleId/maintenance",
    element: (
      <Layout>
        {" "}
        <PrivateRoute>
          {" "}
          <MaintenanceRecords />{" "}
        </PrivateRoute>{" "}
      </Layout>
    ),
  },
  {
    path: "/vehicles/:vehicleId/trips",
    element: (
      <Layout>
        {" "}
        <PrivateRoute>
          {" "}
          <TripList />{" "}
        </PrivateRoute>{" "}
      </Layout>
    ),
  },
  {
    path: "/vehicles/:vehicleId/fuel",
    element: (
      <Layout>
        {" "}
        <PrivateRoute>
          {" "}
          <FuelRecords />{" "}
        </PrivateRoute>{" "}
      </Layout>
    ),
  },
  {
    path: "/tracking",
    element: (
      <Layout>
        {" "}
        <PrivateRoute>
          {" "}
          <GpsTracking />{" "}
        </PrivateRoute>{" "}
      </Layout>
    ),
  },
  {
    path: "/family-tracking",
    element: (
      <Layout>
        {/* *** CONFIRM Uppercase Role Names *** */}
        <PrivateRoute roles={["ADMIN", "PARENT"]}>
          <FamilyTracking />
        </PrivateRoute>
      </Layout>
    ),
  },
  {
    path: "/family-management",
    element: (
      <Layout>
        {/* *** CONFIRM Uppercase Role Names *** */}
        <PrivateRoute roles={["ADMIN", "PARENT"]}>
          <FamilyManagement />
        </PrivateRoute>
      </Layout>
    ),
  },
  {
    path: "/admin",
    element: (
      <Layout>
        {/* *** CONFIRM Uppercase Role Name *** */}
        <PrivateRoute roles={["ADMIN"]}>
          <AdminDashboard />
        </PrivateRoute>
      </Layout>
    ),
  },
]);

export default function App() {
  const queryClient = new QueryClient();
  return (
    <AuthProvider>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        <ToastProvider />
      </QueryClientProvider>
    </AuthProvider>
  );
}
