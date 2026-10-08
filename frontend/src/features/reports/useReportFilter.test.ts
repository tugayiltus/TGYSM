import { describe, expect, it } from 'vitest';

import { exportFileName, figureListHref, isReportName, selectionQuery } from './api';
import { filterQuery, isValidFilterValue, readReportFilter } from './useReportFilter';

const GUID = '00000000-0000-7000-8000-0000000000b1';

describe('report filter values', () => {
  it('keeps valid values of the report and drops invalid or irrelevant ones', () => {
    const params = new URLSearchParams({
      from: '2026-01-01',
      to: '2026-02-30',
      priority: 'p9',
      status: 'open',
      teamId: GUID,
      assigneeId: 'not-a-guid',
      assetType: 'laptop',
      kind: 'request',
    });

    expect(readReportFilter(params, 'incidents')).toEqual({ from: '2026-01-01', status: 'open', teamId: GUID });
    expect(readReportFilter(params, 'operations')).toMatchObject({ kind: 'request' });
    expect(readReportFilter(params, 'assets')).toEqual({ assetType: 'laptop' });
  });

  it('drops a day range whose start is after its end', () => {
    const params = new URLSearchParams('from=2026-05-10&to=2026-05-01&priority=p1');
    expect(readReportFilter(params, 'incidents')).toEqual({ priority: 'p1' });
    expect(readReportFilter(new URLSearchParams('from=2026-05-01&to=2026-05-01'), 'incidents')).toEqual({ from: '2026-05-01', to: '2026-05-01' });
  });

  it('refuses days outside the server range', () => {
    expect(isValidFilterValue('from', '1999-12-31')).toBe(false);
    expect(isValidFilterValue('to', '2101-01-01')).toBe(false);
    expect(isValidFilterValue('slaState', 'at_risk')).toBe(true);
  });

  it('writes set filters in the server key order', () => {
    expect(filterQuery({ status: 'open', from: '2026-01-01', priority: '' }).toString()).toBe('from=2026-01-01&status=open');
  });
});

describe('drill-down selection', () => {
  it('puts metric, bucket and the denominator part before the filters', () => {
    expect(selectionQuery({ metric: 'mttr', bucket: '', part: 'denominator' }, { priority: 'p1' }).toString()).toBe('metric=mttr&part=denominator&priority=p1');
    expect(figureListHref('incidents', 'open_backlog', { priority: 'p1' }, '8+')).toBe('/reports/incidents/list?metric=open_backlog&bucket=8%2B&priority=p1');
  });

  it('knows the report names', () => {
    expect(isReportName('warranty')).toBe(true);
    expect(isReportName('payroll')).toBe(false);
    expect(isReportName(undefined)).toBe(false);
  });

  it('reads the download name from Content-Disposition', () => {
    expect(exportFileName("attachment; filename=\"a.xlsx\"; filename*=UTF-8''olay%20raporu.xlsx", 'incidents')).toBe('olay raporu.xlsx');
    expect(exportFileName('attachment; filename="rapor.xlsx"', 'incidents')).toBe('rapor.xlsx');
    expect(exportFileName(null, 'warranty')).toBe('warranty.xlsx');
    expect(exportFileName("attachment; filename=\"rapor.xlsx\"; filename*=UTF-8''bozuk%E0%A4", 'incidents')).toBe('rapor.xlsx');
    expect(exportFileName("attachment; filename*=UTF-8''bozuk%E0%A4", 'incidents')).toBe('incidents.xlsx');
  });
});
