import { describe, expect, it } from 'vitest';
import BigNumber from 'bignumber.js';
import { suggestDoubleProgression } from '@/models/double-progression';
import { Weight } from '@/models/weight';

const kg = (value: number) => new Weight(value, 'kilograms');

describe('suggestDoubleProgression', () => {
  const base = {
    repMin: 8,
    repMax: 12,
    weight: kg(60),
    loadIncrement: new BigNumber(2.5),
    tracksLoad: true,
  };

  it('adds a rep while the last session is under the top of the range', () => {
    expect(suggestDoubleProgression({ ...base, lastReps: 8 })).toMatchObject({
      reps: 9,
      reason: 'add_reps',
    });
    expect(suggestDoubleProgression({ ...base, lastReps: 8 })?.weight.toNumber()).toBe(60);
  });

  it('keeps adding reps up to the top', () => {
    expect(suggestDoubleProgression({ ...base, lastReps: 11 })?.reps).toBe(12);
  });

  it('adds the load increment at the top of the range and starts the reps again', () => {
    const suggestion = suggestDoubleProgression({ ...base, lastReps: 12 });
    expect(suggestion?.reason).toBe('add_weight');
    expect(suggestion?.reps).toBe(8);
    expect(suggestion?.weight.toNumber()).toBe(62.5);
  });

  it('does not invent a load for a movement that tracks none', () => {
    expect(suggestDoubleProgression({ ...base, lastReps: 12, tracksLoad: false, weight: kg(0) })).toBeUndefined();
    expect(suggestDoubleProgression({ ...base, lastReps: 10, tracksLoad: false })?.reps).toBe(11);
  });
});
