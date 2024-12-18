import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import { CategoryList } from "./components/CategoryList";
import { PlaylistList } from "./components/PlaylistList";
import { SongList } from "./components/SongList";
import { LoginForm } from "./components/LoginForm";
import { RegisterForm } from "./components/RegisterForm";
import { AuthProvider } from "./auth/AuthContext";
import { PrivateRoute } from "./components/PrivateRoute";
import { ToastProvider } from "./components/Toast";
import { Layout } from "./components/Layout";

const router = createBrowserRouter([
  {
    path: "/",
    element: (
      <Layout>
        <CategoryList />
      </Layout>
    ),
  },
  {
    path: "/login",
    element: (
      <Layout>
        <LoginForm />
      </Layout>
    ),
  },
  {
    path: "/register",
    element: (
      <Layout>
        <RegisterForm />
      </Layout>
    ),
  },
  {
    path: "/categories/:categoryId/playlists",
    element: (
      <Layout>
        <PrivateRoute>
          <PlaylistList />
        </PrivateRoute>
      </Layout>
    ),
  },
  {
    path: "/categories/:categoryId/playlists/:playlistId/songs",
    element: (
      <Layout>
        <PrivateRoute>
          <SongList />
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
