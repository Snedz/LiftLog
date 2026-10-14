import { applyProgression, ProgressionRule, WeightedExerciseBlueprint } from '@/models/blueprint-models';
import { PotentialSet, RecordedWeightedExercise } from '@/models/session-models';
import { Weight } from '@/models/weight';
import BigNumber from 'bignumber.js';

export type DoubleProgressionReason = 'add_reps' | 'add_weight';

export interface DoubleProgressionSuggestion {
  reps: number;
  weight: BigNumber;
  reason: DoubleProgressionReason;
}

/**
 * What to do next time: add a rep until the top of the range, then add the load increment and
 * start the reps again. The move itself is {@link applyProgression} on that two-step ladder.
 */
export function suggestDoubleProgression(input: {
  lastReps: number;
  repMin: number;
  repMax: number;
  weight: Weight;
  loadIncrement: BigNumber;
  tracksLoad: boolean;
}): DoubleProgressionSuggestion | undefined {
  if (input.lastReps < 1) {
    return undefined;
  }
  const repMin = Math.max(1, Math.min(input.repMin, input.repMax));
  const repMax = Math.max(repMin, input.repMax);
  const rules = [
    ProgressionRule.of({
      axis: 'reps',
      step: new BigNumber(1),
      ceiling: new BigNumber(repMax),
      onCeiling: 'reset',
    }),
  ];
  if (input.tracksLoad && input.loadIncrement.isGreaterThan(0)) {
    rules.push(ProgressionRule.load(input.loadIncrement));
  }
  const blueprint = WeightedExerciseBlueprint.of({
    plannedSets: [{ reps: { min: repMin, max: repMin } }],
    resistance: input.tracksLoad ? 'external' : 'none',
    progression: rules,
  });
  const current = new RecordedWeightedExercise(
    blueprint,
    [
      PotentialSet.of({
        weight: input.weight,
        target: { min: input.lastReps, max: input.lastReps },
      }),
    ],
    undefined,
  );
  const next = applyProgression(rules, current);
  const moved = next.potentialSets[0];
  if (!moved) {
    return undefined;
  }
  const sameReps = moved.target.max === input.lastReps;
  const sameWeight = moved.weight.value.isEqualTo(input.weight.value);
  if (sameReps && sameWeight) {
    return undefined;
  }
  return {
    reps: moved.target.max,
    weight: moved.weight.value,
    reason: sameWeight ? 'add_reps' : 'add_weight',
  };
}
