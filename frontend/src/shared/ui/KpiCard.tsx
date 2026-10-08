import { CCardBody } from '@coreui/react';
import { useId } from 'react';
import { Link } from 'react-router';

export interface KpiCardProps {
  title: string;
  value: string;
  unit?: string;
  period: string;
  description?: string;
  href?: string;
}

/** One headline number with its unit and the period it was computed for. */
export function KpiCard({ title, value, unit, period, description, href }: KpiCardProps) {
  const titleId = useId();
  return (
    <article aria-labelledby={titleId} className="card h-100">
      <CCardBody className="d-flex flex-column gap-1">
        <h2 id={titleId} className="fs-6 fw-semibold mb-0">
          {href === undefined ? title : <Link to={href}>{title}</Link>}
        </h2>
        <p className="mb-0">
          <span className="fs-3 fw-bold">{value}</span>
          {unit !== undefined && <span className="ms-1 text-body-secondary">{unit}</span>}
        </p>
        <p className="small text-body-secondary mb-0">{period}</p>
        {description !== undefined && <p className="small mb-0">{description}</p>}
      </CCardBody>
    </article>
  );
}
