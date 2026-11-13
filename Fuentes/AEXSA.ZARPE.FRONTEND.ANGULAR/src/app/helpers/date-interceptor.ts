import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { map } from 'rxjs';

const ISO_DATE_REGEX = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/;

function convertDates(body: any): any {
  if (body === null || body === undefined) return body;
  if (typeof body === 'string' && ISO_DATE_REGEX.test(body)) return new Date(body);
  if (Array.isArray(body)) return body.map(convertDates);
  if (typeof body === 'object') {
    for (const key of Object.keys(body)) {
      body[key] = convertDates(body[key]);
    }
  }
  return body;
}

export const dateInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    map(event => event instanceof HttpResponse
      ? event.clone({ body: convertDates(event.body) })
      : event
    )
  );
