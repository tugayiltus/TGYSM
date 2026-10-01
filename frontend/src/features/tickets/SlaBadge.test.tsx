import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import '../../shared/i18n';

import type { TicketSla } from './api';
import { SlaBadge } from './SlaBadge';
import { slaState } from './slaState';

const RUNNING: TicketSla = { metric: 'first_response', dueAt: '2026-10-05T11:00:00Z', remainingSeconds: 9000, isPaused: false, isWarned: false, isBreached: false, result: null };

describe('SlaBadge', () => {
  it('shows the remaining business time of a running timer', () => {
    render(<SlaBadge sla={RUNNING} />);

    expect(screen.getByText('İlk yanıt:')).toBeInTheDocument();
    expect(screen.getByText('Kalan 2 sa 30 dk')).toBeInTheDocument();
  });

  it('marks a paused timer', () => {
    render(<SlaBadge sla={{ ...RUNNING, isPaused: true, dueAt: null }} />);

    expect(screen.getByText('Duraklatıldı · kalan 2 sa 30 dk')).toBeInTheDocument();
  });

  it('marks a warned timer as at risk', () => {
    render(<SlaBadge sla={{ ...RUNNING, isWarned: true, remainingSeconds: 2700 }} />);

    expect(screen.getByText('Risk · kalan 45 dk')).toBeInTheDocument();
  });

  it('shows how late a breached timer is', () => {
    render(<SlaBadge sla={{ ...RUNNING, isWarned: true, isBreached: true, remainingSeconds: -3660 }} />);

    expect(screen.getByText('İhlal · 1 sa 1 dk gecikme')).toBeInTheDocument();
  });

  it('shows the final result of a stopped timer', () => {
    render(<SlaBadge sla={{ ...RUNNING, remainingSeconds: null, dueAt: null, result: 'met' }} />);

    expect(screen.getByText('Karşılandı')).toBeInTheDocument();
  });

  it('derives the state in order: result, breach, pause, warning', () => {
    expect(slaState({ ...RUNNING, result: 'breached', remainingSeconds: null })).toBe('metBreached');
    expect(slaState({ ...RUNNING, result: 'cancelled', remainingSeconds: null })).toBe('cancelled');
    expect(slaState({ ...RUNNING, remainingSeconds: -1 })).toBe('breached');
    expect(slaState({ ...RUNNING, isPaused: true, isWarned: true })).toBe('paused');
    expect(slaState(RUNNING)).toBe('running');
  });
});
