import { useNavigate, useParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Card, CardHeader, CardTitle } from "./ui/card";
import { Button } from "./ui/button";
import { apiClient } from "../api/client";
import { CreatePlaylist } from "./CreatePlaylist";
import { useState } from "react";
import { Breadcrumb } from "./Breadcrumb";
import { toast } from "sonner";
import { Pencil, Trash, Eye } from "lucide-react";

interface Playlist {
  id: number;
  name: string;
  description: string;
  categoryId: number;
  createdOn: string;
}

export function PlaylistList() {
  const navigate = useNavigate();
  const { categoryId } = useParams();
  const [editingId, setEditingId] = useState<number | null>(null);
  const queryClient = useQueryClient();

  const deleteMutation = useMutation({
    mutationFn: (playlistId: number) =>
      apiClient.delete(`/categories/${categoryId}/playlists/${playlistId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["playlists", categoryId] });
      toast.success("Playlist deleted successfully");
    },
    onError: () => toast.error("Failed to delete playlist"),
  });

  const updateMutation = useMutation({
    mutationFn: ({
      id,
      data,
    }: {
      id: number;
      data: { name: string; description: string };
    }) => apiClient.put(`/categories/${categoryId}/playlists/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["playlists", categoryId] });
      setEditingId(null);
      toast.success("Playlist updated successfully");
    },
    onError: (error: any) => {
      if (error.errors) {
        const firstError =
          error.errors.Name?.[0] || error.errors.Description?.[0];
        if (firstError) {
          toast.error(firstError);
        } else {
          toast.error("Failed to update playlist");
        }
      }
    },
  });

  const { data, isLoading } = useQuery({
    queryKey: ["playlists", categoryId],
    queryFn: async () => {
      const response = await apiClient.get(
        `/categories/${categoryId}/playlists`,
      );
      return response.data;
    },
  });

  if (isLoading) return <div className="text-center">Loading...</div>;

  return (
    <div className="space-y-4">
      <Breadcrumb />
      <Button variant="outline" onClick={() => navigate(-1)}>
        ← Back to Categories
      </Button>
      <div className="flex justify-between items-center">
        <h1 className="text-3xl font-bold">Playlists</h1>
        <CreatePlaylist categoryId={categoryId!} />
      </div>
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {data?.map((playlist: Playlist) => (
          <Card
            key={playlist.id}
            className="bg-white dark:bg-gray-800 text-black dark:text-gray-100 p-4 transition-transform hover:scale-105 hover:shadow-lg"
          >
            <CardHeader>
              {editingId === playlist.id ? (
                <form
                  onSubmit={(e) => {
                    e.preventDefault();
                    const formData = new FormData(e.currentTarget);
                    updateMutation.mutate({
                      id: playlist.id,
                      data: {
                        name: formData.get("name") as string,
                        description: formData.get("description") as string,
                      },
                    });
                  }}
                  className="space-y-2"
                >
                  <input
                    name="name"
                    defaultValue={playlist.name}
                    className="border p-2 rounded w-full bg-gray-100 dark:bg-gray-700"
                    placeholder="Playlist Name"
                    required
                  />
                  {updateMutation.error?.errors?.Name && (
                    <p className="text-red-500 text-sm">
                      {updateMutation.error.errors.Name[0]}
                    </p>
                  )}

                  <textarea
                    name="description"
                    defaultValue={playlist.description}
                    className="border p-2 rounded w-full bg-gray-100 dark:bg-gray-700"
                    placeholder="Description"
                  />
                  {updateMutation.error?.errors?.Description && (
                    <p className="text-red-500 text-sm">
                      {updateMutation.error.errors.Description[0]}
                    </p>
                  )}

                  <div className="flex gap-2 mt-2">
                    <Button type="submit" disabled={updateMutation.isPending}>
                      {updateMutation.isPending ? "Saving..." : "Save"}
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() => setEditingId(null)}
                      disabled={updateMutation.isPending}
                    >
                      Cancel
                    </Button>
                  </div>
                </form>
              ) : (
                <>
                  <CardTitle className="text-xl font-semibold mb-2">
                    {playlist.name}
                  </CardTitle>
                  <p className="text-sm text-gray-700 dark:text-gray-300 mb-4">
                    {playlist.description}
                  </p>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      onClick={() => setEditingId(playlist.id)}
                    >
                      <Pencil className="h-4 w-4 mr-1" /> Edit
                    </Button>
                    <Button
                      variant="outline"
                      disabled={deleteMutation.isPending}
                      onClick={() => {
                        if (confirm("Delete this playlist?")) {
                          deleteMutation.mutate(playlist.id);
                        }
                      }}
                    >
                      {deleteMutation.isPending ? (
                        "Deleting..."
                      ) : (
                        <>
                          <Trash className="h-4 w-4 mr-1" /> Delete
                        </>
                      )}
                    </Button>
                    <Button
                      variant="secondary"
                      onClick={() =>
                        navigate(
                          `/categories/${categoryId}/playlists/${playlist.id}/songs`,
                        )
                      }
                    >
                      <Eye className="h-4 w-4 mr-1" /> View Songs
                    </Button>
                  </div>
                </>
              )}
            </CardHeader>
          </Card>
        ))}
      </div>
    </div>
  );
}
