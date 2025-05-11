import axios, { AxiosError } from "axios";
import { toast } from "sonner";

export const apiClient = axios.create({
  baseURL: "https://octopus-app-3t93j.ondigitalocean.app/api",
  //baseURL: "http://localhost:5006/api",
  headers: {
    "Content-Type": "application/json",
  },
  withCredentials: true,
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
    console.log(
      `🚀 API Request: ${config.method?.toUpperCase()} ${config.url}`,
      // `\nHeaders:`, config.headers, // Uncomment for detailed header logging
      // `\nData:`, config.data // Uncomment for detailed data logging
    );
    return config;
  },
  (error) => {
    console.error("❌ Request Error:", error);
    return Promise.reject(error);
  },
);

apiClient.interceptors.response.use(
  (response) => {
    console.log(
      `✅ API Response: ${response.status} ${response.config.method?.toUpperCase()} ${response.config.url}`,
      // `\nHeaders:`, response.headers, // Uncomment for detailed header logging
      // `\nData:`, response.data // Uncomment for detailed data logging
    );
    // Log pagination header if present
    if (response.headers["pagination"]) {
      console.log("📄 Pagination Header:", response.headers["pagination"]);
    }
    return response;
  },
  async (error: AxiosError) => {
    // Use AxiosError type
    console.error("❌ Response Error Interceptor:", error);

    const originalRequest = error.config;

    if (error.response) {
      console.error(`Response Status: ${error.response.status}`);
      console.error("Response Data:", error.response.data);
      console.error("Response Headers:", error.response.headers);
    } else if (error.request) {
      console.error("No response received:", error.request);
    } else {
      console.error("Error setting up request:", error.message);
    }

    // Handle 401 Unauthorized (Token Expired / Invalid)
    if (
      error.response?.status === 401 &&
      originalRequest &&
      !(originalRequest as any)._retry
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

      (originalRequest as any)._retry = true; // Mark request as retried
      isRefreshing = true;

      try {
        console.log("Attempting token refresh...");
        const response = await apiClient.post("/accessToken");
        const { accessToken, userInfo } = response.data;

        if (!accessToken || !userInfo) {
          throw new Error("Invalid refresh token response from server.");
        }

        console.log("Token refreshed successfully.");
        localStorage.setItem("token", accessToken);
        localStorage.setItem("userInfo", JSON.stringify(userInfo));

        apiClient.defaults.headers.common["Authorization"] =
          `Bearer ${accessToken}`;

        if (originalRequest.headers) {
          originalRequest.headers["Authorization"] = `Bearer ${accessToken}`;
        }

        onTokenRefreshed(accessToken);
        isRefreshing = false;
        return apiClient(originalRequest);
      } catch (refreshError: any) {
        console.error("Token refresh failed:", refreshError);
        isRefreshing = false;
        localStorage.removeItem("token");
        localStorage.removeItem("userInfo");
        delete apiClient.defaults.headers.common["Authorization"];
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
      // let the component handle
      // return Promise.reject(error); // Or let it fall through
    }

    // Handle 422 Unprocessable Entity (Validation Errors)
    if (error.response?.status === 422) {
      const errorData = error.response.data as any;
      console.error("Validation Error (422):", errorData);
      if (errorData?.errors) {
        const firstErrorKey = Object.keys(errorData.errors)[0];
        const firstErrorMessage =
          errorData.errors[firstErrorKey]?.[0] || "Validation failed.";
        toast.error(`Validation Error: ${firstErrorMessage}`);
        return Promise.reject({ status: 422, errors: errorData.errors });
      } else {
        toast.error(errorData?.detail || "Validation error occurred.");
      }
      return Promise.reject(error);
    }

    // Handle other errors (404, 500, Network Error, etc.)
    let errorMessage = "An unexpected error occurred.";
    if (error.response) {
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
