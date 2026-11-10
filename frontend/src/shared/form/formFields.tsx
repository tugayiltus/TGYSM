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
 * Astryx fields bound to react-hook-form (ADR-0027). Astryx inputs are controlled, so each field reads and writes the
 * form value through `useController`; the validation message (an error code or i18n key) becomes the Turkish `status`.
 */
interface Bound<T extends FieldValues> {
  control: Control<T>;
  name: FieldPath<T>;
}

/**
 * Props the binding supplies; callers pass everything else straight to the Astryx component. `hasClear` is left out:
 * it switches Astryx to a nullable value, which a bound field adds when a form first needs it.
 */
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
  Omit<ComponentProps<typeof Selector>, Supplied | 'options' | 'hasClear'> & { options: readonly SelectOption[] };

export function FormSelector<T extends FieldValues>({ control, name, options, ...props }: FormSelectorProps<T>) {
  const { value, onChange, status } = useBoundField({ control, name });
  return <Selector {...props} options={[...options]} value={value as string} onChange={onChange} status={status} />;
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
