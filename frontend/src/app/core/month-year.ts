import { Pipe, PipeTransform } from '@angular/core';

/** Formats a "yyyy-MM" month key as "MM/yyyy" for display. */
export function formatMonthYear(month: string): string {
  const match = /^(\d{4})-(\d{2})$/.exec(month);
  return match ? `${match[2]}/${match[1]}` : month;
}

@Pipe({ name: 'monthYear' })
export class MonthYearPipe implements PipeTransform {
  transform(month: string): string {
    return formatMonthYear(month);
  }
}
