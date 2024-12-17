import { Pause, Play } from "lucide-react";
import { Button } from "./ui/button";
import { useState } from "react";

// Helper function for formatting time
const formatTime = (time: number): string => {
  const minutes = Math.floor(time / 60);
  const seconds = Math.floor(time % 60);
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
};

export function AudioPlayer() {
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime] = useState(0);
  const [duration] = useState(0);

  return (
    <div className="fixed bottom-0 left-0 right-0 bg-white shadow-lg p-4">
      <div className="container mx-auto flex items-center gap-4">
        <Button
          variant="secondary"
          size="icon"
          onClick={() => setIsPlaying(!isPlaying)}
        >
          {isPlaying ? <Pause /> : <Play />}
        </Button>

        <div className="flex-1">
          <div className="w-full bg-gray-200 rounded-full h-1.5">
            <div
              className="bg-primary h-1.5 rounded-full transition-all"
              style={{ width: `${(currentTime / duration) * 100}%` }}
            />
          </div>
        </div>

        <span className="text-sm text-gray-500">
          {formatTime(currentTime)} / {formatTime(duration)}
        </span>
      </div>
    </div>
  );
}
