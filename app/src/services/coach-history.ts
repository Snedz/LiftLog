import { RecordedWeightedExercise } from '@/models/session-models';
import { Session } from '@/models/session-models/session';
import { LocalDate } from '@js-joda/core';

export const coachHistoryStart = '<<mw-coach-history>>';
export const coachHistoryEnd = '<<end-mw-coach-history>>';

export interface CoachHistoryExercise {
  name: string;
  sets: number;
  reps: number;
  weightKg: number;
}

export interface CoachHistorySession {
  date: string;
  name: string;
  exercises: CoachHistoryExercise[];
}

export interface CoachHistory {
  today: string;
  lastSessionDate: string | null;
  sessions: CoachHistorySession[];
}

export function attachCoachHistory(userText: string, history: CoachHistory): string {
  return `${coachHistoryStart}\n${JSON.stringify(history)}\n${coachHistoryEnd}\n${userText}`;
}

/** The last few finished sessions, newest first, for the server-side coach. */
export function historyFromSessions(
  sessions: Session[],
  activeSessionId: string | undefined,
  today: LocalDate = LocalDate.now(),
): CoachHistory {
  const finished = sessions
    .filter((session) => session.id !== activeSessionId)
    .map(summarizeSession)
    .filter((session) => session.exercises.length > 0)
    .sort((a, b) => (a.date < b.date ? 1 : a.date > b.date ? -1 : 0))
    .slice(0, 3);
  return {
    today: today.toString(),
    lastSessionDate: finished[0]?.date ?? null,
    sessions: finished,
  };
}

function summarizeSession(session: Session): CoachHistorySession {
  const exercises = session.recordedExercises.flatMap((exercise) => {
    if (!(exercise instanceof RecordedWeightedExercise)) {
      return [];
    }
    const done = exercise.potentialSets.filter((set) => set.set);
    const last = done.at(-1);
    if (!last?.set) {
      return [];
    }
    const kg = last.weight.convertTo('kilograms').value.toNumber();
    return [
      {
        name: exercise.blueprint.name,
        sets: done.length,
        reps: last.set.repsCompleted,
        weightKg: Math.round(kg * 100) / 100,
      },
    ];
  });
  return {
    date: session.date.toString(),
    name: session.blueprint.name,
    exercises,
  };
}
