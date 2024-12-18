import { useNavigate, useParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Card, CardContent } from "./ui/card";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { apiClient } from "../api/client";
import { AddSong } from "./AddSong";
import {
  DragDropContext,
  Draggable,
  Droppable,
  DropResult,
} from "@hello-pangea/dnd";
import { toast } from "sonner";
import { Breadcrumb } from "./Breadcrumb";
import React, { useState } from "react";
import { Pencil, Trash } from "lucide-react"; // Optional icons for edit/delete

interface Song {
  id: number;
  title: string;
  artist: string;
  duration: number;
  orderId: number;
}

export function SongList() {
  const { categoryId, playlistId } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [editingId, setEditingId] = useState<number | null>(null);

  const {
    data = [],
    isLoading,
    error,
  } = useQuery<Song[]>({
    queryKey: ["songs", categoryId, playlistId],
    queryFn: async () => {
      try {
        const response = await apiClient.get(
          `/categories/${categoryId}/playlists/${playlistId}/songs`,
        );
        const songs = Array.isArray(response.data)
          ? response.data
          : response.data?.resources?.map((item: any) => item.resource) || [];
        return songs;
      } catch (err: any) {
        if (err.response?.status === 404) {
          return [];
        }
        throw err;
      }
    },
  });

  const updateMutation = useMutation({
    mutationFn: async ({ id, data }: { id: number; data: Partial<Song> }) => {
      return apiClient.put(
        `/categories/${categoryId}/playlists/${playlistId}/songs/${id}`,
        data,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["songs", categoryId, playlistId],
      });
      setEditingId(null);
      toast.success("Song updated successfully");
    },
    onError: (error: any) => {
      if (error.errors) {
        const firstError =
          error.errors.Title?.[0] ||
          error.errors.Artist?.[0] ||
          error.errors.Duration?.[0];
        if (firstError) {
          toast.error(firstError);
        } else {
          toast.error("Failed to update song");
        }
      }
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (songId: number) => {
      return apiClient.delete(
        `/categories/${categoryId}/playlists/${playlistId}/songs/${songId}`,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["songs", categoryId, playlistId],
      });
      toast.success("Song deleted successfully");
    },
    onError: (error: any) => {
      if (error.response?.status === 404) {
        toast.error("Song not found");
      } else {
        toast.error("Failed to delete song");
      }
    },
  });

  const onDragEnd = React.useCallback(
    (result: DropResult) => {
      if (!result.destination) return;

      if (result.source.index === result.destination.index) return;

      const songId = parseInt(result.draggableId.replace("song-", ""));

      const sortedSongs = [...data]
        .sort((a, b) => a.orderId - b.orderId)
        .map((song) => ({ ...song }));

      const draggedSong = sortedSongs.find((s) => s.id === songId);
      if (!draggedSong) return;

      const filteredSongs = sortedSongs.filter((s) => s.id !== songId);
      filteredSongs.splice(result.destination.index, 0, draggedSong);

      const updatedSongs = filteredSongs.map((song, index) => ({
        ...song,
        orderId: index + 1,
      }));

      queryClient.setQueryData(["songs", categoryId, playlistId], updatedSongs);

      updateMutation.mutate(
        {
          id: songId,
          data: {
            title: draggedSong.title,
            artist: draggedSong.artist,
            duration: draggedSong.duration,
            orderId: result.destination.index + 1,
          },
        },
        {
          onError: (error) => {
            console.error("Mutation Error:", error);
            queryClient.setQueryData(["songs", categoryId, playlistId], data);
            toast.error("Failed to update song order");
          },
          onSettled: () => {
            queryClient.invalidateQueries({
              queryKey: ["songs", categoryId, playlistId],
            });
          },
        },
      );
    },
    [data, updateMutation, categoryId, playlistId, queryClient],
  );

  if (isLoading) return <div className="text-center">Loading...</div>;
  if (error)
    return (
      <div className="text-center text-red-500">
        Error: {(error as Error).message}
      </div>
    );

  if (!data?.length)
    return (
      <div className="p-4">
        <Breadcrumb />
        <Button variant="outline" onClick={() => navigate(-1)} className="mb-4">
          ← Back to Playlist
        </Button>
        <h1 className="text-2xl font-bold mb-4">Songs</h1>
        <AddSong categoryId={categoryId!} playlistId={playlistId!} />
        <p className="text-gray-500 mt-4">
          No songs found. Add some songs to get started!
        </p>
      </div>
    );

  return (
    <div className="p-4 space-y-4">
      <Breadcrumb />
      <Button variant="outline" onClick={() => navigate(-1)} className="mb-4">
        ← Back to Playlist
      </Button>
      <h1 className="text-2xl font-bold">Songs</h1>
      <AddSong categoryId={categoryId!} playlistId={playlistId!} />
      <DragDropContext onDragEnd={onDragEnd}>
        <Droppable droppableId="songs-list">
          {(provided) => (
            <div
              {...provided.droppableProps}
              ref={provided.innerRef}
              className="space-y-4"
            >
              {data
                .slice()
                .sort((a, b) => a.orderId - b.orderId)
                .map((song, index) => (
                  <Draggable
                    key={`song-${song.id}`}
                    draggableId={`song-${song.id}`}
                    index={index}
                  >
                    {(provided) => (
                      <div
                        ref={provided.innerRef}
                        {...provided.draggableProps}
                        className="transition-transform hover:scale-[1.01]"
                      >
                        <Card className="bg-white dark:bg-gray-800 text-black dark:text-gray-100">
                          <CardContent className="p-4">
                            {editingId === song.id ? (
                              <form
                                onSubmit={(e) => {
                                  e.preventDefault();
                                  const formData = new FormData(
                                    e.currentTarget,
                                  );
                                  updateMutation.mutate({
                                    id: song.id,
                                    data: {
                                      title: formData.get("title") as string,
                                      artist: formData.get("artist") as string,
                                      duration: parseInt(
                                        formData.get("duration") as string,
                                      ),
                                      orderId: song.orderId,
                                    },
                                  });
                                }}
                                className="w-full space-y-4"
                              >
                                <Input
                                  name="title"
                                  label="Song Title"
                                  defaultValue={song.title}
                                  placeholder="Song Title"
                                  required
                                />
                                <Input
                                  name="artist"
                                  label="Artist"
                                  defaultValue={song.artist}
                                  placeholder="Artist"
                                  required
                                />
                                <Input
                                  name="duration"
                                  label="Duration (seconds)"
                                  type="number"
                                  defaultValue={song.duration}
                                  placeholder="Duration in seconds"
                                  required
                                />
                                <div className="flex gap-2">
                                  <Button type="submit">Save</Button>
                                  <Button
                                    type="button"
                                    variant="outline"
                                    onClick={() => setEditingId(null)}
                                  >
                                    Cancel
                                  </Button>
                                </div>
                              </form>
                            ) : (
                              <div className="flex items-center gap-4 w-full">
                                <div
                                  {...provided.dragHandleProps}
                                  className="flex flex-col gap-1 cursor-move"
                                  aria-label="Reorder song"
                                >
                                  {/* Draggable handle indicator */}
                                  <div className="flex gap-1">
                                    <div className="w-1 h-1 rounded-full bg-gray-400"></div>
                                    <div className="w-1 h-1 rounded-full bg-gray-400"></div>
                                  </div>
                                  <div className="flex gap-1">
                                    <div className="w-1 h-1 rounded-full bg-gray-400"></div>
                                    <div className="w-1 h-1 rounded-full bg-gray-400"></div>
                                  </div>
                                  <div className="flex gap-1">
                                    <div className="w-1 h-1 rounded-full bg-gray-400"></div>
                                    <div className="w-1 h-1 rounded-full bg-gray-400"></div>
                                  </div>
                                </div>
                                <div>
                                  <h3 className="font-medium">{song.title}</h3>
                                  <p className="text-sm text-gray-500">
                                    {song.artist}
                                  </p>
                                </div>
                                <div className="flex items-center gap-4 ml-auto">
                                  <div className="text-sm">
                                    {Math.floor(song.duration / 60)}:
                                    {(song.duration % 60)
                                      .toString()
                                      .padStart(2, "0")}
                                  </div>
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() => setEditingId(song.id)}
                                  >
                                    <Pencil className="h-4 w-4 mr-1" />
                                    Edit
                                  </Button>
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    disabled={deleteMutation.isPending}
                                    onClick={() => {
                                      if (
                                        confirm(
                                          "Are you sure you want to delete this song?",
                                        )
                                      ) {
                                        deleteMutation.mutate(song.id);
                                      }
                                    }}
                                  >
                                    {deleteMutation.isPending ? (
                                      "Deleting..."
                                    ) : (
                                      <>
                                        <Trash className="h-4 w-4 mr-1" />
                                        Delete
                                      </>
                                    )}
                                  </Button>
                                </div>
                              </div>
                            )}
                          </CardContent>
                        </Card>
                      </div>
                    )}
                  </Draggable>
                ))}
              {provided.placeholder}
            </div>
          )}
        </Droppable>
      </DragDropContext>
    </div>
  );
}
