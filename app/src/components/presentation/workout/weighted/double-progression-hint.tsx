import { spacing, useAppTheme } from '@/hooks/useAppTheme';
import { suggestDoubleProgression } from '@/models/double-progression';
import { RecordedWeightedExercise } from '@/models/session-models';
import { Weight } from '@/models/weight';
import { useTranslate } from '@tolgee/react';
import { Text } from 'react-native-paper';

/** One line under the sets: add a rep up to the top of the range, then add load. */
export function DoubleProgressionHint({
  recordedExercise,
  previous,
}: {
  recordedExercise: RecordedWeightedExercise;
  previous: RecordedWeightedExercise | undefined;
}) {
  const { t } = useTranslate();
  const { colors } = useAppTheme();
  const nextIndex = recordedExercise.potentialSets.findIndex((set) => !set.set);
  const setIndex = nextIndex === -1 ? 0 : nextIndex;
  const lastReps = previous?.potentialSets[setIndex]?.set?.repsCompleted;
  const weight = recordedExercise.potentialSets[setIndex]?.weight;
  if (lastReps === undefined || !weight) {
    return null;
  }
  const target = recordedExercise.repsTargetForSet(setIndex);
  const suggestion = suggestDoubleProgression({
    lastReps,
    repMin: target.min,
    repMax: target.max,
    weight,
    loadIncrement: recordedExercise.blueprint.weightIncrement,
    tracksLoad: recordedExercise.tracksResistance,
  });
  if (!suggestion) {
    return null;
  }
  const message =
    suggestion.reason === 'add_reps'
      ? t('progression.suggest.add_reps', { reps: suggestion.reps })
      : t('progression.suggest.add_weight', {
          reps: suggestion.reps,
          weight: new Weight(suggestion.weight, weight.unit).shortLocaleFormat(),
        });
  return (
    <Text variant="bodySmall" style={{ color: colors.onSurfaceVariant, marginTop: spacing[2] }}>
      {message}
    </Text>
  );
}
