import { Toaster, toast } from "sonner";

export { toast };
export const ToastProvider = () => (
  <Toaster richColors position="bottom-right" />
);
