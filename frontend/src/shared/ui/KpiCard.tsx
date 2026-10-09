import CIcon from '@coreui/icons-react';
import { CCardBody } from '@coreui/react';
import { useId } from 'react';
import { Link } from 'react-router';

export type KpiColor = 'primary' | 'info' | 'warning' | 'danger' | 'success';

export interface KpiCardProps {
  title: string;
  value: string;
  unit?: string;
  period: string;
  description?: string;
  href?: string;
  /** Template stat widget: a colored icon box beside the number. */
  icon?: string[];
  color?: KpiColor;
}

/** One headline number with its unit and the period it was computed for. */
export function KpiCard({ title, value, unit, period, description, href, icon, color = 'primary' }: KpiCardProps) {
  const titleId = useId();
  return (
    <article aria-labelledby={titleId} className="card shadow-sm h-100 position-relative">
      <CCardBody className="d-flex align-items-start gap-3">
        {icon !== undefined && (
          <span className={`d-inline-flex align-items-center justify-content-center rounded-3 flex-shrink-0 tgysm-kpi-icon bg-${color}-subtle text-${color}-emphasis`} aria-hidden="true">
            <CIcon icon={icon} size="xl" />
          </span>
        )}
        <div className="d-flex flex-column gap-1 min-w-0">
          <h2 id={titleId} className="small text-body-secondary text-uppercase fw-semibold mb-0">
            {href === undefined ? (
              title
            ) : (
              <Link to={href} className="stretched-link text-reset text-decoration-none">
                {title}
              </Link>
            )}
          </h2>
          <p className="mb-0">
            <span className="fs-4 fw-semibold">{value}</span>
            {unit !== undefined && <span className="ms-1 text-body-secondary">{unit}</span>}
          </p>
          <p className="small text-body-secondary mb-0">{period}</p>
          {description !== undefined && <p className="small mb-0">{description}</p>}
        </div>
      </CCardBody>
    </article>
  );
}
