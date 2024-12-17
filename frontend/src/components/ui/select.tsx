import { forwardRef } from "react";
import { cn } from "../../lib/utils.ts";

export interface SelectProps
  extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  error?: string;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(
  ({ className, label, error, children, ...props }, ref) => {
    return (
      <div className="space-y-2">
        {label && (
          <label className="text-sm font-medium leading-none">{label}</label>
        )}
        <select
          className={cn(
            "flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 transition-colors duration-200",
            error && "border-red-500 focus-visible:ring-red-500",
            className,
          )}
          ref={ref}
          {...props}
        >
          {children}
        </select>
        {error && <p className="text-sm text-red-500">{error}</p>}
      </div>
    );
  },
);

Select.displayName = "Select";
