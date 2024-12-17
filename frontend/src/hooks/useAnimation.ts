import { useState, useEffect } from "react";

export interface AnimationStyles {
  opacity: number;
  transform: string;
  transition?: string;
}

export interface SlideInOptions {
  delay?: number;
  duration?: number;
  tension?: number;
  friction?: number;
}

export function useSlideIn(options: SlideInOptions = {}) {
  const { delay = 0, duration = 300, tension = 280, friction = 20 } = options;

  // Use tension and friction to generate a custom cubic-bezier curve
  const cubicBezier = calculateCubicBezier(tension, friction);

  const [styles, setStyles] = useState<AnimationStyles>({
    opacity: 0,
    transform: "translateY(20px)",
    transition: `all ${duration}ms ${cubicBezier}`,
  });

  useEffect(() => {
    const timeoutId = setTimeout(() => {
      setStyles({
        opacity: 1,
        transform: "translateY(0)",
        transition: `all ${duration}ms ${cubicBezier}`,
      });
    }, delay);

    return () => clearTimeout(timeoutId);
  }, [delay, duration, cubicBezier]);

  return {
    style: styles,
    opacity: styles.opacity,
    transform: styles.transform,
  };
}

export function useFadeIn(options: SlideInOptions = {}) {
  const { delay = 0, duration = 300, tension = 280, friction = 20 } = options;

  // Use tension and friction to generate a custom cubic-bezier curve
  const cubicBezier = calculateCubicBezier(tension, friction);

  const [opacity, setOpacity] = useState(0);

  useEffect(() => {
    const timeoutId = setTimeout(() => {
      setOpacity(1);
    }, delay);

    return () => clearTimeout(timeoutId);
  }, [delay]);

  return {
    style: {
      opacity,
      transition: `opacity ${duration}ms ${cubicBezier}`,
    },
    opacity,
  };
}

// Helper function to calculate cubic-bezier based on tension and friction
function calculateCubicBezier(tension: number, friction: number): string {
  // Normalize tension and friction to affect the cubic-bezier curve
  const x1 = Math.min(1, tension / 500);
  const x2 = Math.min(1, friction / 100);

  // Generate a cubic-bezier curve that responds to tension and friction
  return `cubic-bezier(${x1}, 0, 0.2, ${x2})`;
}
