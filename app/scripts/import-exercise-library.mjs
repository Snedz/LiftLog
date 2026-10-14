/**
 * Refreshes the offline exercise catalog from yuhonas/free-exercise-db (Unlicense)
 * and copies a Bryl Lim workout-guide frame (CC BY-SA 4.0 artwork, MIT package)
 * for every exercise whose name matches.
 *
 * Run from app/: node scripts/import-exercise-library.mjs
 * The app does not download anything at runtime. This script writes the bundled files.
 */
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const freeExerciseDb =
  'https://raw.githubusercontent.com/yuhonas/free-exercise-db/f00c92c7dcf1216a928a52c3706c7ce8e2f71ed5/dist/exercises.json';
const workoutGuideManifest =
  'https://raw.githubusercontent.com/bryllim/workout-guide/aac599224bb9780305239607ef98540b7e0ce389/packages/workout-guide/manifest.json';
const workoutGuideRaw =
  'https://raw.githubusercontent.com/bryllim/workout-guide/aac599224bb9780305239607ef98540b7e0ce389/packages/workout-guide/';

function normalizeExerciseName(value) {
  return value
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/&/g, ' and ')
    .replace(/[^a-z0-9]+/g, ' ')
    .trim()
    .replace(/\s+/g, ' ');
}

function formatExercise(exercise) {
  const muscles = (key) => {
    const inline = `[${exercise[key].map((value) => JSON.stringify(value)).join(', ')}]`;
    const line = `      "${key}": ${inline}`;
    if (line.length <= 100) {
      return inline;
    }
    return `[\n${exercise[key].map((value) => `        ${JSON.stringify(value)}`).join(',\n')}\n      ]`;
  };
  return [
    '    {',
    `      "name": ${JSON.stringify(exercise.name)},`,
    `      "force": ${JSON.stringify(exercise.force)},`,
    `      "level": ${JSON.stringify(exercise.level)},`,
    `      "mechanic": ${JSON.stringify(exercise.mechanic)},`,
    `      "equipment": ${JSON.stringify(exercise.equipment)},`,
    `      "primaryMuscles": ${muscles('primaryMuscles')},`,
    `      "secondaryMuscles": ${muscles('secondaryMuscles')},`,
    `      "category": ${JSON.stringify(exercise.category)}`,
    '    }',
  ].join('\n');
}

async function fetchJson(url) {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`${url} returned ${response.status}`);
  }
  return response.json();
}

const [upstream, guides] = await Promise.all([fetchJson(freeExerciseDb), fetchJson(workoutGuideManifest)]);
const guidesByName = new Map(guides.map((guide) => [normalizeExerciseName(guide.name), guide]));
const upstreamByName = new Map(upstream.map((exercise) => [exercise.name, exercise]));

const catalogPath = path.join(root, 'assets/exercises.json');
const catalog = JSON.parse(await readFile(catalogPath, 'utf8'));
const known = new Set(catalog.exercises.map((exercise) => exercise.name));
const additions = upstream
  .filter((exercise) => !known.has(exercise.name))
  .map((exercise) => ({
    name: exercise.name,
    force: exercise.force,
    level: exercise.level,
    mechanic: exercise.mechanic,
    equipment: exercise.equipment,
    primaryMuscles: exercise.primaryMuscles,
    secondaryMuscles: exercise.secondaryMuscles,
    category: exercise.category,
  }));

// Keep the bundled order. Insert a new exercise before the first name that sorts after it.
let catalogText = await readFile(catalogPath, 'utf8');
for (const exercise of additions) {
  const block = formatExercise(exercise);
  const follower = catalog.exercises.find((existing) => existing.name.localeCompare(exercise.name) > 0);
  if (!follower) {
    catalogText = catalogText.replace(/\n  ]\n}\n?$/, `,\n${block}\n  ]\n}\n`);
  } else {
    const anchor = `      "name": ${JSON.stringify(follower.name)},`;
    const at = catalogText.indexOf(anchor);
    if (at < 0) {
      throw new Error(`could not find ${follower.name} to insert ${exercise.name}`);
    }
    const objectStart = catalogText.lastIndexOf('\n    {', at);
    catalogText = `${catalogText.slice(0, objectStart)}\n${block},${catalogText.slice(objectStart)}`;
  }
}
await writeFile(catalogPath, catalogText.endsWith('\n') ? catalogText : `${catalogText}\n`);

const englishPath = path.join(root, 'assets/exercises/en.json');
const english = JSON.parse(await readFile(englishPath, 'utf8'));
const names = [...Object.keys(english)];
for (const exercise of additions) {
  const index = names.findIndex((name) => name.localeCompare(exercise.name) > 0);
  names.splice(index === -1 ? names.length : index, 0, exercise.name);
}
const nextEnglish = {};
for (const name of names) {
  const source = upstreamByName.get(name);
  const previous = english[name] ?? {};
  nextEnglish[name] = {
    ...previous,
    name: previous.name ?? name,
    instructions: source ? source.instructions.join('\n') : (previous.instructions ?? ''),
  };
}
await writeFile(englishPath, `${JSON.stringify(nextEnglish, null, 2)}\n`);

const matches = upstream.flatMap((exercise) => {
  const guide = guidesByName.get(normalizeExerciseName(exercise.name));
  if (!guide) {
    return [];
  }
  const frame = guide.frames.find((candidate) => candidate.index === 2) ?? guide.frames[0];
  return [{ name: exercise.name, slug: guide.slug, path: frame.path.replace(/\.svg$/, '.png') }];
});

const artDir = path.join(root, 'assets/exercise-illustrations');
await mkdir(artDir, { recursive: true });
const manifest = {};
for (const match of matches) {
  const fileName = `${match.slug}.png`;
  const response = await fetch(new URL(match.path, workoutGuideRaw));
  if (!response.ok) {
    throw new Error(`${match.path} returned ${response.status}`);
  }
  await writeFile(path.join(artDir, fileName), Buffer.from(await response.arrayBuffer()));
  manifest[match.name] = fileName;
}
await writeFile(path.join(artDir, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`);

const requires = Object.entries(manifest)
  .map(
    ([name, fileName]) =>
      `  ${JSON.stringify(name)}: require('../../assets/exercise-illustrations/${fileName}') as number,`,
  )
  .join('\n');
const moduleText = `// Generated by scripts/import-exercise-library.mjs. Do not edit.
// Mid-movement frames from bryllim/workout-guide, attached where the exercise name matches.

export const exerciseIllustrations: Record<string, number> = {
${requires}
};

export function illustrationForExercise(name: string): number | undefined {
  return exerciseIllustrations[name];
}
`;
await writeFile(path.join(root, 'src/services/exercise-illustrations.generated.ts'), moduleText);

console.log(`catalog additions ${additions.length}, illustrations ${matches.length}`);
