import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const appRoot = path.resolve(import.meta.dirname, '../..');

describe('bundled exercise illustrations', () => {
  const catalog = JSON.parse(readFileSync(path.join(appRoot, 'assets/exercises.json'), 'utf8')) as {
    exercises: { name: string }[];
  };
  const manifest = JSON.parse(
    readFileSync(path.join(appRoot, 'assets/exercise-illustrations/manifest.json'), 'utf8'),
  ) as Record<string, string>;
  const generated = readFileSync(path.join(appRoot, 'src/services/exercise-illustrations.generated.ts'), 'utf8');
  const names = new Set(catalog.exercises.map((exercise) => exercise.name));

  it('imports the free-exercise-db movements that were missing from the previous list', () => {
    expect(names.has('Kettlebell Halo')).toBe(true);
    expect(names.has('Kettlebell Halo with Overhead Extension')).toBe(true);
    expect(names.has('Kettlebell Overhead Triceps Extension')).toBe(true);
  });

  it('attaches an offline frame only when the exercise name matches', () => {
    expect(Object.keys(manifest).length).toBeGreaterThan(40);
    for (const [name, fileName] of Object.entries(manifest)) {
      expect(names.has(name)).toBe(true);
      expect(fileName).toMatch(/^[a-z0-9-]+\.png$/);
      expect(existsSync(path.join(appRoot, 'assets/exercise-illustrations', fileName))).toBe(true);
      expect(generated).toContain(`require('../../assets/exercise-illustrations/${fileName}')`);
    }
    expect(manifest['Dumbbell Bench Press']).toBe('dumbbell-bench-press.png');
    expect(manifest['Barbell Bench Press']).toBeUndefined();
    expect(manifest.Plank).toBe('plank.png');
  });
});
