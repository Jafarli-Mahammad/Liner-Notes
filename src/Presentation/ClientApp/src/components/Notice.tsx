import { ApiError, errorText } from '../api/client';

export function Notice({ error, children, onRetry }: { error?: unknown; children?: React.ReactNode; onRetry?: () => void }) {
  return <div className={`notice${error ? ' notice-error' : ''}`} role={error ? 'alert' : 'status'}>
    {error ? <><p>{errorText(error)}</p>{error instanceof ApiError && Object.keys(error.fields).length > 0 && <ul>{Object.values(error.fields).flat().map((message, index) => <li key={index}>{message}</li>)}</ul>}</> : children}
    {onRetry && <button className="text-button" type="button" onClick={onRetry}>Refresh saved information</button>}
  </div>;
}
