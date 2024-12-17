import { motion, AnimatePresence } from "framer-motion";
import { DragDropContext, Draggable, Droppable } from "@hello-pangea/dnd";
import { Song } from "../api/types";

interface DraggableSongListProps {
  songs: Song[];
  onReorder: (reorderedSongs: Song[]) => void;
}

const reorder = <T,>(list: T[], startIndex: number, endIndex: number): T[] => {
  const result = Array.from(list);
  const [removed] = result.splice(startIndex, 1);
  result.splice(endIndex, 0, removed);
  return result;
};

export function DraggableSongList({
  songs,
  onReorder,
}: DraggableSongListProps) {
  return (
    <DragDropContext
      onDragEnd={(result) => {
        if (!result.destination) return;
        const items = reorder(
          songs,
          result.source.index,
          result.destination.index,
        );
        onReorder(items);
      }}
    >
      <Droppable droppableId="songs">
        {(provided) => (
          <div ref={provided.innerRef} {...provided.droppableProps}>
            <AnimatePresence>
              {songs.map((song: Song, index: number) => (
                <Draggable
                  key={song.id}
                  draggableId={song.id.toString()}
                  index={index}
                >
                  {(provided, snapshot) => (
                    <motion.div
                      ref={provided.innerRef}
                      {...provided.draggableProps}
                      initial={{ opacity: 0, y: 20 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: -20 }}
                      style={{
                        ...provided.draggableProps.style,
                        transform: snapshot.isDragging
                          ? "scale(1.02)"
                          : "scale(1)",
                      }}
                    >
                      <div className="border rounded-lg p-4 mb-2 bg-white shadow-sm">
                        <div
                          {...provided.dragHandleProps}
                          className="cursor-move"
                        >
                          {/* Song content */}
                        </div>
                      </div>
                    </motion.div>
                  )}
                </Draggable>
              ))}
              {provided.placeholder}
            </AnimatePresence>
          </div>
        )}
      </Droppable>
    </DragDropContext>
  );
}
