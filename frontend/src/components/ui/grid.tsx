export const Grid = {
  Container: ({
    children,
    className,
  }: {
    children: React.ReactNode;
    className?: string;
  }) => <div className={`container mx-auto px-4 ${className}`}>{children}</div>,
  Row: ({
    children,
    className,
  }: {
    children: React.ReactNode;
    className?: string;
  }) => <div className={`grid gap-4 md:gap-6 ${className}`}>{children}</div>,
};
