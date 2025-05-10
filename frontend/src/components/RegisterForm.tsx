// src/components/RegisterForm.tsx
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { apiClient } from "../api/client";
import { toast } from "sonner";
import { Select } from "./ui/select";

interface ApiError {
  errors?: Record<string, string[]>;
  detail?: string;
  title?: string;
}

export function RegisterForm() {
  const [formData, setFormData] = useState({
    userName: "",
    email: "",
    password: "",
  });
  const [role, setRole] = useState("FleetUser"); // Default role
  const [isLoading, setIsLoading] = useState(false);
  const [apiErrors, setApiErrors] = useState<Record<string, string[]>>({}); // Store API validation errors

  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setApiErrors({}); // Clear previous errors

    try {
      const registrationData = {
        ...formData,
        role: role, // Send the selected role
      };

      console.log("Sending registration data:", registrationData);

      await apiClient.post("/accounts", registrationData);

      toast.success("Registration successful! Please login.");
      navigate("/login");
    } catch (err: any) {
      console.error("Registration error:", err);

      const errorData: ApiError = err.response?.data || {};

      if (err.response?.status === 422 && errorData.errors) {
        // Handle validation errors from the API
        setApiErrors(errorData.errors);
        // Show a general validation error toast
        toast.error(errorData.title || "Please fix the errors below.");
      } else if (errorData.detail) {
        toast.error(errorData.detail);
      } else {
        toast.error("Failed to register. Please try again.");
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="max-w-md mx-auto mt-8 p-6 bg-white rounded-lg shadow space-y-6 transition-shadow hover:shadow-lg">
      <h2 className="text-2xl font-bold">Register</h2>

      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Username"
          type="text"
          value={formData.userName}
          onChange={(e) => {
            setFormData({ ...formData, userName: e.target.value });
            setApiErrors((prev) => {
              const next = { ...prev };
              delete next.UserName; // Delete the key
              return next;
            });
          }}
          placeholder="Choose a username"
          error={apiErrors.UserName?.[0]} // Display API error for username
          required
        />

        <Input
          label="Email"
          type="email"
          value={formData.email}
          onChange={(e) => {
            setFormData({ ...formData, email: e.target.value });
            setApiErrors((prev) => {
              const next = { ...prev };
              delete next.UserName; // Delete the key
              return next;
            });
          }}
          placeholder="Your email address"
          error={apiErrors.Email?.[0]} // Display API error for email
          required
        />

        <Input
          label="Password"
          type="password"
          value={formData.password}
          onChange={(e) => {
            setFormData({ ...formData, password: e.target.value });
            setApiErrors((prev) => {
              const next = { ...prev };
              delete next.UserName; // Delete the key
              return next;
            });
          }}
          placeholder="Choose a strong password"
          error={
            apiErrors.Password?.[0] ||
            apiErrors.PasswordRequiresDigit?.[0] ||
            apiErrors.PasswordRequiresLower?.[0] ||
            apiErrors.PasswordRequiresNonAlphanumeric?.[0] ||
            apiErrors.PasswordRequiresUpper?.[0] ||
            apiErrors.PasswordTooShort?.[0]
          } // Display API password errors
          required
          minLength={8} // Basic client-side check
        />

        {/* Role Selection */}
        <Select
          label="Account Type"
          value={role}
          onChange={(e) => {
            setRole(e.target.value);
            setApiErrors((prev) => {
              const next = { ...prev };
              delete next.UserName; // Delete the key
              return next;
            });
          }}
          error={apiErrors.Role?.[0]} // Display API error for role
        >
          <option value="FleetUser">Regular User</option>
          <option value="Parent">Parent</option>
          <option value="Teenager">Teenager</option>
          {/* Optionally hide Admin role unless needed */}
          {/* <option value="Admin">Administrator</option> */}
        </Select>
        <div className="mt-2 text-xs text-gray-500 space-y-1">
          <p>
            <strong>Parent:</strong> Can track family members & manage vehicles.
          </p>
          <p>
            <strong>Teenager:</strong> Limited access, location trackable by
            parents.
          </p>
          <p>
            <strong>Regular User:</strong> Standard access to own
            vehicles/trips.
          </p>
        </div>

        <Button type="submit" className="w-full" disabled={isLoading}>
          {isLoading ? "Registering..." : "Register"}
        </Button>
      </form>

      <p className="text-sm text-center mt-4">
        Already have an account?{" "}
        <a href="/login" className="text-blue-600 hover:underline">
          Login
        </a>
      </p>
    </div>
  );
}
