import { Volume2 } from "lucide-react";
import { Button } from "./ui/button";
import { motion } from "framer-motion";

export function VolumeSlider() {
  return (
    <div className="group relative">
      <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 opacity-0 group-hover:opacity-100 transition-opacity">
        <div className="h-24 w-1 bg-gray-200 rounded-full">
          <div className="h-1/2 w-full bg-primary rounded-full" />
        </div>
      </div>
      <Button variant="outline" size="icon">
        <Volume2 />
      </Button>
    </div>
  );
}

export function PlaylistProgress() {
  const progress = 45; // Example progress

  return (
    <div className="relative pt-1">
      <div className="flex mb-2 items-center justify-between">
        <div>
          <span className="text-xs font-semibold inline-block py-1 px-2 uppercase rounded-full text-primary bg-primary/10">
            Currently Playing
          </span>
        </div>
        <div className="text-right">
          <span className="text-xs font-semibold inline-block text-primary">
            {progress}%
          </span>
        </div>
      </div>
      <div className="overflow-hidden h-2 mb-4 text-xs flex rounded bg-primary/10">
        <motion.div
          initial={{ width: 0 }}
          animate={{ width: `${progress}%` }}
          transition={{ duration: 0.5 }}
          className="shadow-none flex flex-col text-center whitespace-nowrap text-white justify-center bg-primary"
        />
      </div>
    </div>
  );
}
