import { CFormInput, CFormLabel } from '@coreui/react';
import { useId, useState, type KeyboardEvent } from 'react';

import { toSearchKey } from '../lib/turkish';

import { FieldMessages } from './FieldMessages';
import type { SelectOption } from './SelectField';

export interface ComboboxFieldProps {
  label: string;
  options: readonly SelectOption[];
  value: string;
  onChange: (value: string) => void;
  /** Shown in the list when nothing matches the typed text. */
  emptyText: string;
  error?: string;
  hint?: string;
  disabled?: boolean;
}

const NONE = -1;

/**
 * Searchable single select built from free CoreUI form controls (ARIA 1.2 combobox with a listbox popup).
 * Typing filters with the Turkish search key; ArrowUp/Down move, Enter picks, Escape closes.
 */
export function ComboboxField({ label, options, value, onChange, emptyText, error, hint, disabled }: ComboboxFieldProps) {
  const baseId = useId();
  const inputId = `${baseId}-input`;
  const listId = `${baseId}-list`;
  const hintId = hint ? `${baseId}-hint` : undefined;
  const errorId = error ? `${baseId}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined;

  const selectedLabel = options.find((option) => option.value === value)?.label ?? '';
  // null = not typing: the input shows the selected option's label.
  const [query, setQuery] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(NONE);

  const key = toSearchKey(query ?? '');
  const matches = query === null || key === '' ? options : options.filter((option) => toSearchKey(option.label).includes(key));
  const optionId = (index: number) => `${baseId}-option-${index}`;

  const choose = (option: SelectOption) => {
    onChange(option.value);
    setQuery(null);
    setOpen(false);
    setActive(NONE);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        setOpen(true);
        setActive((current) => Math.min(current + 1, matches.length - 1));
        break;
      case 'ArrowUp':
        event.preventDefault();
        setActive((current) => Math.max(current - 1, 0));
        break;
      case 'Enter': {
        const option = matches[active];
        if (open && option !== undefined) {
          event.preventDefault();
          choose(option);
        }
        break;
      }
      case 'Escape':
        if (open) {
          event.preventDefault();
          event.stopPropagation();
          setOpen(false);
          setActive(NONE);
        }
        break;
      default:
        break;
    }
  };

  return (
    <div className="d-flex flex-column position-relative">
      <CFormLabel htmlFor={inputId}>{label}</CFormLabel>
      <CFormInput
        id={inputId}
        role="combobox"
        autoComplete="off"
        aria-autocomplete="list"
        aria-expanded={open}
        aria-controls={listId}
        aria-activedescendant={open && active !== NONE ? optionId(active) : undefined}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        invalid={Boolean(error)}
        disabled={disabled}
        value={query ?? selectedLabel}
        onChange={(event) => {
          setQuery(event.target.value);
          setOpen(true);
          setActive(NONE);
        }}
        onClick={() => setOpen(true)}
        onBlur={() => {
          setOpen(false);
          setQuery(null);
        }}
        onKeyDown={onKeyDown}
      />
      {open && (
        <ul id={listId} role="listbox" aria-label={label} className="list-group position-absolute top-100 start-0 end-0 shadow z-3 overflow-auto tgysm-combobox-list">
          {matches.length === 0 ? (
            <li className="list-group-item text-body-secondary" role="presentation">
              {emptyText}
            </li>
          ) : (
            matches.map((option, index) => (
              <li
                key={option.value}
                id={optionId(index)}
                role="option"
                aria-selected={index === active}
                className={`list-group-item list-group-item-action ${index === active ? 'active' : ''}`}
                // mousedown, not click: picking must happen before the input's blur closes the list.
                onMouseDown={(event) => {
                  event.preventDefault();
                  choose(option);
                }}
              >
                {option.label}
              </li>
            ))
          )}
        </ul>
      )}
      <FieldMessages hint={hint} hintId={hintId} error={error} errorId={errorId} />
    </div>
  );
}
