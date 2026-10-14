import { illustrationForExercise } from '@/services/exercise-illustrations.generated';
import { spacing, useAppTheme } from '@/hooks/useAppTheme';
import { T } from '@tolgee/react';
import { Image, View } from 'react-native';
import { Text } from 'react-native-paper';

/** Offline workout-guide frame for a built-in exercise, when the English name matches. */
export function ExerciseIllustration({ name }: { name: string }) {
  const source = illustrationForExercise(name);
  const { colors } = useAppTheme();
  if (source === undefined) {
    return null;
  }
  return (
    <View style={{ alignItems: 'flex-start', gap: spacing[1] }}>
      <Image
        source={source}
        accessibilityLabel={name}
        resizeMode="contain"
        style={{ width: 96, height: 96, backgroundColor: colors.surfaceVariant, borderRadius: 8 }}
      />
      <Text variant="bodySmall" style={{ color: colors.onSurfaceVariant }}>
        <T keyName="exercise.illustration.credit" />
      </Text>
    </View>
  );
}
