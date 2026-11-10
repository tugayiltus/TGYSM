/**
 * @input react-hook-form `control` and field `name`, plus Astryx field props.
 * @output Astryx inputs that read and write the form value and show its Turkish error.
 * @position Shared by every migrated form screen.
 */
import { CheckboxInput } from '@astryxdesign/core/CheckboxInput';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { Selector } from '@astryxdesign/core/Selector';
import { Switch } from '@astryxdesign/core/Switch';
import { TextArea } from '@astryxdesign/core/TextArea';
import { TextInput } from '@astryxdesign/core/TextInput';
import type { ComponentProps } from 'react';
import { useController, type Control, type FieldPath, type FieldValues } from 'react-hook-form';

import { fieldError } from '../api/errorMessage';

/**
 * Astryx inputs are controlled, so each field reads and writes the form value through `useController` (ADR-0027).
 * The validation message is an error code or an i18n key. It becomes the Turkish `status`.
 */
interface Bound<T extends FieldValues> {
  control: Control<T>;
  name: FieldPath<T>;
}

/** The binding supplies these props. Callers pass all other props to the Astryx component. */
type Supplied = 'value' | 'onChange' | 'onBlur' | 'ref' | 'status' | 'name';

const ERROR_STATUS = 'error';

function useBoundField<T extends FieldValues>({ control, name }: Bound<T>) {
  const {
    field: { ref, value, onChange, onBlur },
    fieldState,
  } = useController({ control, name });
  const message = fieldError(fieldState.error);
  const status = message === undefined ? undefined : ({ type: ERROR_STATUS, message } as const);
  return { ref, value: value as unknown, onChange, onBlur, status };
}

export type FormTextInputProps<T extends FieldValues> = Bound<T> & Omit<ComponentProps<typeof TextInput>, Supplied>;

export function FormTextInput<T extends FieldValues>({ control, name, ...props }: FormTextInputProps<T>) {
  const { ref, value, onChange, onBlur, status } = useBoundField({ control, name });
  return <TextInput {...props} ref={ref} value={(value as string | undefined) ?? ''} onChange={onChange} onBlur={onBlur} status={status} />;
}

export type FormTextAreaProps<T extends FieldValues> = Bound<T> & Omit<ComponentProps<typeof TextArea>, Supplied>;

export function FormTextArea<T extends FieldValues>({ control, name, ...props }: FormTextAreaProps<T>) {
  const { ref, value, onChange, onBlur, status } = useBoundField({ control, name });
  return <TextArea {...props} ref={ref} value={(value as string | undefined) ?? ''} onChange={onChange} onBlur={onBlur} status={status} />;
}

export interface SelectOption {
  value: string;
  label: string;
}

export type FormSelectorProps<T extends FieldValues> = Bound<T> &
  Omit<ComponentProps<typeof Selector>, Supplied | 'options' | 'hasClear' | 'changeAction'> & {
    options: readonly SelectOption[];
    /** Optional choice: a clear button empties it. The form value is then `''`. */
    isClearable?: boolean;
    /** Runs after the form value changes, e.g. to reset a field that depends on this one. */
    onValueChange?: (value: string) => void;
  };

const NO_VALUE = '';

export function FormSelector<T extends FieldValues>({ control, name, options, isClearable = false, onValueChange, ...props }: FormSelectorProps<T>) {
  const { value, onChange: setValue, status } = useBoundField({ control, name });
  const onChange = (next: string) => {
    setValue(next);
    onValueChange?.(next);
  };
  const current = (value as string | undefined) ?? NO_VALUE;
  if (isClearable) {
    return (
      <Selector
        {...props}
        options={[...options]}
        hasClear
        value={current === NO_VALUE ? null : current}
        onChange={(next: string | null) => onChange(next ?? NO_VALUE)}
        status={status}
      />
    );
  }
  return <Selector {...props} options={[...options]} value={current} onChange={onChange} status={status} />;
}

export type FormCheckboxProps<T extends FieldValues> = Bound<T> & Omit<ComponentProps<typeof CheckboxInput>, Supplied>;

export function FormCheckbox<T extends FieldValues>({ control, name, ...props }: FormCheckboxProps<T>) {
  const { ref, value, onChange, onBlur, status } = useBoundField({ control, name });
  return <CheckboxInput {...props} ref={ref} value={Boolean(value)} onChange={onChange} onBlur={onBlur} status={status} />;
}

export type FormSwitchProps<T extends FieldValues> = Bound<T> & Omit<ComponentProps<typeof Switch>, Supplied>;

export function FormSwitch<T extends FieldValues>({ control, name, ...props }: FormSwitchProps<T>) {
  const { value, onChange } = useBoundField({ control, name });
  return <Switch {...props} value={Boolean(value)} onChange={onChange} />;
}

export type FormNumberInputProps<T extends FieldValues> = Bound<T> & Omit<ComponentProps<typeof NumberInput>, Supplied | 'hasClear'>;

export function FormNumberInput<T extends FieldValues>({ control, name, ...props }: FormNumberInputProps<T>) {
  const { value, onChange, status } = useBoundField({ control, name });
  return <NumberInput {...props} value={value as number | null} onChange={onChange} status={status} />;
}
