import { Directive, ElementRef, HostListener, Input, OnChanges, forwardRef, inject } from '@angular/core';
import { AbstractControl, ControlValueAccessor, NG_VALIDATORS, NG_VALUE_ACCESSOR, ValidationErrors, Validator } from '@angular/forms';

const vndFormatter = new Intl.NumberFormat('vi-VN', {
  style: 'currency', currency: 'VND', minimumFractionDigits: 0, maximumFractionDigits: 2,
});

@Directive({
  selector: 'input[vndInput]',
  standalone: true,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => VndInput), multi: true },
    { provide: NG_VALIDATORS, useExisting: forwardRef(() => VndInput), multi: true },
  ],
})
export class VndInput implements ControlValueAccessor, Validator, OnChanges {
  private element = inject(ElementRef<HTMLInputElement>);
  private focused = false;
  private onChange: (value: number | null) => void = () => {};
  private onTouched: () => void = () => {};
  private onValidatorChange: () => void = () => {};

  @Input() min: string | null = null;
  @Input() max: string | number | null = null;
  @Input() allowNegative = false;

  ngOnChanges(): void { this.onValidatorChange(); }

  private clean(text: string): string {
    const negative = this.allowNegative && text.trimStart().startsWith('-');
    return (negative ? '-' : '') + text.replace(/[^\d,]/g, '');
  }

  writeValue(value: number | null): void {
    if (value === null || value === undefined) {
      this.element.nativeElement.value = '';
      return;
    }
    const number = Number(value);
    this.element.nativeElement.value = this.focused ? String(number).replace('.', ',') : vndFormatter.format(number);
  }

  registerOnChange(fn: (value: number | null) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  registerOnValidatorChange(fn: () => void): void { this.onValidatorChange = fn; }

  setDisabledState(disabled: boolean): void { this.element.nativeElement.disabled = disabled; }

  validate(control: AbstractControl): ValidationErrors | null {
    if (control.value === null || control.value === '') return null;
    const value = Number(control.value);
    if (this.min !== null && value < Number(this.min)) return { min: { min: Number(this.min), actual: value } };
    if (this.max !== null && value > Number(this.max)) return { max: { max: Number(this.max), actual: value } };
    return Number.isFinite(value) ? null : { number: true };
  }

  @HostListener('focus')
  onFocus(): void {
    this.focused = true;
    this.element.nativeElement.value = this.clean(this.element.nativeElement.value);
  }

  @HostListener('input')
  onInput(): void {
    const input = this.element.nativeElement;
    const raw = this.clean(input.value);
    const comma = raw.indexOf(',');
    const normalized = comma < 0 ? raw : `${raw.slice(0, comma + 1)}${raw.slice(comma + 1).replace(/,/g, '')}`;
    input.value = normalized;
    if (normalized.endsWith(',') || normalized === '-') return;
    const number = normalized.replace(',', '.');
    this.onChange(number === '' ? null : Number(number));
  }

  @HostListener('blur')
  onBlur(): void {
    this.focused = false;
    const input = this.element.nativeElement;
    const normalized = this.clean(input.value).replace(',', '.');
    let value = normalized === '' || normalized === '-' ? null : Number(normalized);
    if (value !== null && this.min !== null && value < Number(this.min)) value = Number(this.min);
    this.onChange(value);
    this.onTouched();
    input.value = value === null || !Number.isFinite(value) ? '' : vndFormatter.format(value);
  }
}
