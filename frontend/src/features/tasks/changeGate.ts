import type { ChangePhase, ChangeStatus } from '../changes/api';

/**
 * Mirrors backend ChangeGate (ADR-0032) so a blocked button explains itself; the server stays the authority
 * (409 changes.phase_not_active). The backout start is not part of the task view, so a Geri dönüş task is
 * judged by the change status alone and the server decides the rest.
 */
export function canStartInPhase(status: ChangeStatus, phase: ChangePhase): boolean {
  switch (phase) {
    case 'preparation':
      return status === 'scheduled' || status === 'implementing' || status === 'verifying';
    case 'implementation':
      return status === 'implementing';
    case 'verification':
      return status === 'verifying';
    case 'backout':
      return status === 'implementing' || status === 'verifying';
  }
}

/** As starting, except that an Uygulama task may still be finished while verifying. */
export function canCompleteInPhase(status: ChangeStatus, phase: ChangePhase): boolean {
  return phase === 'implementation' ? status === 'implementing' || status === 'verifying' : canStartInPhase(status, phase);
}
