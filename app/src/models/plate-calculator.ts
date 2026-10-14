import { WeightUnit } from '@/models/weight';

/** Competition and commercial plates, heaviest first. */
export const PLATES_KG = [25, 20, 15, 10, 5, 2.5, 1.25] as const;
export const PLATES_LB = [45, 35, 25, 10, 5, 2.5] as const;

export interface PlateLoad {
  barWeight: number;
  targetWeight: number;
  /** Heaviest-first plates on one side of the bar. */
  platesPerSide: number[];
  /** Weight still missing on one side after the closest load. */
  remainder: number;
  achievable: boolean;
}

export function defaultBarWeight(unit: WeightUnit): number {
  return unit === 'pounds' ? 45 : 20;
}

export function platesForUnit(unit: WeightUnit): readonly number[] {
  return unit === 'pounds' ? PLATES_LB : PLATES_KG;
}

/**
 * Plates per side for a barbell. The target is the whole bar, including the bar itself.
 * A target at or under the bar is the empty bar.
 */
export function platesPerSide(target: number, unit: WeightUnit, barWeight = defaultBarWeight(unit)): PlateLoad {
  const bar = Math.max(barWeight, 0);
  const total = Math.max(target, 0);
  if (total <= bar + 1e-9) {
    return { barWeight: bar, targetWeight: total, platesPerSide: [], remainder: 0, achievable: true };
  }
  let remaining = (total - bar) / 2;
  const used: number[] = [];
  for (const plate of platesForUnit(unit)) {
    while (remaining + 1e-9 >= plate) {
      used.push(plate);
      remaining -= plate;
    }
  }
  if (remaining < 0.05) {
    remaining = 0;
  }
  return {
    barWeight: bar,
    targetWeight: total,
    platesPerSide: used,
    remainder: remaining,
    achievable: remaining === 0,
  };
}

/** "2×20kg + 1×2.5kg". Empty when the bar is unloaded. */
export function formatPlatesPerSide(plates: number[], unit: WeightUnit): string {
  const suffix = unit === 'pounds' ? 'lb' : 'kg';
  const counts: { plate: number; count: number }[] = [];
  for (const plate of plates) {
    const last = counts[counts.length - 1];
    if (last && last.plate === plate) {
      last.count += 1;
    } else {
      counts.push({ plate, count: 1 });
    }
  }
  return counts
    .map(({ plate, count }) => {
      const label = Number.isInteger(plate) ? String(plate) : String(plate);
      return count === 1 ? `${label}${suffix}` : `${count}×${label}${suffix}`;
    })
    .join(' + ');
}
