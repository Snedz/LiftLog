import { describe, expect, it } from 'vitest';
import { defaultBarWeight, formatPlatesPerSide, platesPerSide } from '@/models/plate-calculator';

describe('platesPerSide', () => {
  it('loads a 100 kg bar with 20 kg of plates on each side', () => {
    const load = platesPerSide(100, 'kilograms');
    expect(load.platesPerSide).toEqual([25, 15]);
    expect(load.achievable).toBe(true);
    expect(load.remainder).toBe(0);
    expect(formatPlatesPerSide(load.platesPerSide, 'kilograms')).toBe('25kg + 15kg');
  });

  it('uses two of the same plate when that is the greedy load', () => {
    const load = platesPerSide(60, 'kilograms', 20);
    expect(load.platesPerSide).toEqual([20]);
    expect(formatPlatesPerSide([20, 20, 2.5], 'kilograms')).toBe('2×20kg + 2.5kg');
  });

  it('reports the remainder when the target is not a plate combination', () => {
    const load = platesPerSide(61, 'kilograms', 20);
    expect(load.platesPerSide).toEqual([20]);
    expect(load.achievable).toBe(false);
    expect(load.remainder).toBeCloseTo(0.5, 5);
  });

  it('is the empty bar when the target is the bar or less', () => {
    expect(platesPerSide(20, 'kilograms').platesPerSide).toEqual([]);
    expect(platesPerSide(15, 'kilograms').achievable).toBe(true);
    expect(formatPlatesPerSide([], 'kilograms')).toBe('');
  });

  it('uses pound plates and a 45 lb bar', () => {
    expect(defaultBarWeight('pounds')).toBe(45);
    const load = platesPerSide(100, 'pounds');
    expect(load.barWeight).toBe(45);
    expect(load.platesPerSide).toEqual([25, 2.5]);
    expect(formatPlatesPerSide(load.platesPerSide, 'pounds')).toBe('25lb + 2.5lb');
  });
});
