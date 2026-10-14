import { attachCoachHistory, historyFromSessions } from '@/services/coach-history';
import { SessionBlueprint } from '@/models/blueprint-models';
import { PotentialSet, RecordedSet, RecordedWeightedExercise, Session } from '@/models/session-models';
import { makeWeightedBlueprint } from '@/models/session-models/__test__/helpers';
import { Weight } from '@/models/weight';
import { LocalDate, OffsetDateTime } from '@js-joda/core';
import { describe, expect, it } from 'vitest';

function loggedSession(id: string, date: string, weightKg: number): Session {
  const blueprint = makeWeightedBlueprint({ name: 'Squat' });
  const exercise = new RecordedWeightedExercise(
    blueprint,
    [
      new PotentialSet(
        RecordedSet.of({
          repsCompleted: 5,
          completionDateTime: OffsetDateTime.parse(`${date}T12:00:00Z`),
        }),
        new Weight(weightKg, 'kilograms'),
        blueprint.repsTargetForSet(0),
      ),
    ],
    undefined,
  );
  return new Session(
    id,
    new SessionBlueprint('Legs', [blueprint], ''),
    [exercise],
    LocalDate.parse(date),
    undefined,
    undefined,
  );
}

describe('coach history', () => {
  it('wraps history in the markers the server strips back out', () => {
    const text = attachCoachHistory('Plan the next one', {
      today: '2026-10-14',
      lastSessionDate: '2026-10-01',
      sessions: [],
    });
    expect(text.startsWith('<<mw-coach-history>>')).toBe(true);
    expect(text).toContain('<<end-mw-coach-history>>');
    expect(text.endsWith('Plan the next one')).toBe(true);
    expect(text).toContain('"lastSessionDate":"2026-10-01"');
  });

  it('sends the newest finished session and skips the one in progress', () => {
    const older = loggedSession('old', '2026-10-01', 100);
    const newer = loggedSession('new', '2026-10-10', 110);
    const active = loggedSession('active', '2026-10-14', 120);
    const history = historyFromSessions([older, active, newer], 'active', LocalDate.parse('2026-10-14'));
    expect(history.today).toBe('2026-10-14');
    expect(history.lastSessionDate).toBe('2026-10-10');
    expect(history.sessions.map((session) => session.date)).toEqual(['2026-10-10', '2026-10-01']);
    expect(history.sessions[0].exercises[0]).toEqual({ name: 'Squat', sets: 1, reps: 5, weightKg: 110 });
  });
});
