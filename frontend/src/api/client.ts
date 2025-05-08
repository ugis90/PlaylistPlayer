import axios, { AxiosError } from "axios";
import { toast } from "sonner";

export const apiClient = axios.create({
  //baseURL: "https://octopus-app-3t93j.ondigitalocean.app/api",
  baseURL: "http://localhost:5006/api",
  headers: {
    "Content-Type": "application/json",
  },
  withCredentials: true, // Important for cookies (like RefreshToken)
});

let isRefreshing = false;
let refreshSubscribers: ((token: string) => void)[] = [];

const subscribeTokenRefresh = (cb: (token: string) => void) => {
  refreshSubscribers.push(cb);
};

const onTokenRefreshed = (token: string) => {
  refreshSubscribers.forEach((cb) => cb(token));
  refreshSubscribers = [];
};

// Add request interceptor for auth token and logging
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem("token");
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    // --- Enhanced Logging ---
    console.log(
      `🚀 API Request: ${config.method?.toUpperCase()} ${config.url}`,
      // `\nHeaders:`, config.headers, // Uncomment for detailed header logging
      // `\nData:`, config.data // Uncomment for detailed data logging
    );
    // --- End Enhanced Logging ---
    return config;
  },
  (error) => {
    console.error("❌ Request Error:", error);
    return Promise.reject(error);
  },
);

// Add response interceptor for handling 401, 422, and logging
apiClient.interceptors.response.use(
  (response) => {
    // --- Enhanced Logging ---
    console.log(
      `✅ API Response: ${response.status} ${response.config.method?.toUpperCase()} ${response.config.url}`,
      // `\nHeaders:`, response.headers, // Uncomment for detailed header logging
      // `\nData:`, response.data // Uncomment for detailed data logging
    );
    // Log pagination header if present
    if (response.headers["pagination"]) {
      console.log("📄 Pagination Header:", response.headers["pagination"]);
    }
    // --- End Enhanced Logging ---
    return response;
  },
  async (error: AxiosError) => {
    // Use AxiosError type
    console.error("❌ Response Error Interceptor:", error);

    const originalRequest = error.config;

    // --- Detailed Error Logging ---
    if (error.response) {
      console.error(`Response Status: ${error.response.status}`);
      console.error("Response Data:", error.response.data);
      console.error("Response Headers:", error.response.headers);
    } else if (error.request) {
      console.error("No response received:", error.request);
    } else {
      console.error("Error setting up request:", error.message);
    }
    // --- End Detailed Error Logging ---

    // Handle 401 Unauthorized (Token Expired / Invalid)
    // Ensure originalRequest exists and _retry flag is not set
    if (
      error.response?.status === 401 &&
      originalRequest &&
      !originalRequest._retry
    ) {
      if (isRefreshing) {
        // Wait for the new token if refresh is already in progress
        return new Promise((resolve) => {
          subscribeTokenRefresh((token) => {
            if (originalRequest.headers) {
              originalRequest.headers["Authorization"] = `Bearer ${token}`;
            }
            resolve(apiClient(originalRequest));
          });
        });
      }

      originalRequest._retry = true; // Mark request as retried
      isRefreshing = true;

      try {
        console.log("Attempting token refresh...");
        // Call refresh token endpoint directly
        const response = await apiClient.post("/accessToken"); // No need for full URL if baseURL is set
        const { accessToken, userInfo } = response.data; // Expect userInfo here too

        if (!accessToken || !userInfo) {
          throw new Error("Invalid refresh token response from server.");
        }

        console.log("Token refreshed successfully.");
        localStorage.setItem("token", accessToken);
        localStorage.setItem("userInfo", JSON.stringify(userInfo)); // Update user info

        // Update the default header for subsequent requests
        apiClient.defaults.headers.common["Authorization"] =
          `Bearer ${accessToken}`;

        // Update the header of the original failed request
        if (originalRequest.headers) {
          originalRequest.headers["Authorization"] = `Bearer ${accessToken}`;
        }

        onTokenRefreshed(accessToken); // Notify subscribers
        isRefreshing = false;
        return apiClient(originalRequest); // Retry the original request
      } catch (refreshError: any) {
        console.error("Token refresh failed:", refreshError);
        isRefreshing = false;
        // If refresh fails, clear token, logout, and reject
        localStorage.removeItem("token");
        localStorage.removeItem("userInfo");
        delete apiClient.defaults.headers.common["Authorization"];
        // Consider calling a logout function from AuthContext here if available
        // Example: authContext.logout();
        toast.error("Session expired. Please log in again.");
        return Promise.reject(refreshError);
      }
    }

    // Handle 403 Forbidden (Permission Denied)
    if (error.response?.status === 403) {
      const errorDetail =
        (error.response.data as any)?.detail ||
        "You don't have permission for this action.";
      console.error("Permission Denied (403):", errorDetail);
      toast.error(`Permission Denied: ${errorDetail}`);
      // Don't automatically reject, let the component handle it if needed
      // return Promise.reject(error); // Or let it fall through
    }

    // Handle 422 Unprocessable Entity (Validation Errors)
    if (error.response?.status === 422) {
      const errorData = error.response.data as any; // Type assertion
      console.error("Validation Error (422):", errorData);
      if (errorData?.errors) {
        // Extract and show the first validation error message
        const firstErrorKey = Object.keys(errorData.errors)[0];
        const firstErrorMessage =
          errorData.errors[firstErrorKey]?.[0] || "Validation failed.";
        toast.error(`Validation Error: ${firstErrorMessage}`);
        // Reject with the structured validation errors for the component
        return Promise.reject({ status: 422, errors: errorData.errors });
      } else {
        // Show generic 422 error if structure is unexpected
        toast.error(errorData?.detail || "Validation error occurred.");
      }
      // Reject so the calling code knows it failed
      return Promise.reject(error);
    }

    // Handle other errors (404, 500, Network Error, etc.)
    let errorMessage = "An unexpected error occurred.";
    if (error.response) {
      // Use detail or title if available, otherwise status text
      errorMessage =
        (error.response.data as any)?.detail ||
        (error.response.data as any)?.title ||
        error.response.statusText ||
        `Server error: ${error.response.status}`;
    } else if (error.request) {
      errorMessage = "Network error: Could not reach the server.";
    } else {
      errorMessage = error.message;
    }
    // Avoid showing redundant 401/403/422 toasts if already handled above
    if (![401, 403, 422].includes(error.response?.status ?? 0)) {
      toast.error(errorMessage);
    }

    // Reject the promise for all other errors
    return Promise.reject(error);
  },
);

export default apiClient;
