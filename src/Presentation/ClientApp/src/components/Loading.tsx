export function Loading({ label = 'Loading your discoveries…', shape = 'cards' }: { label?: string; shape?: 'cards' | 'form' | 'list' }) {
  return <div className={`content-loading loading-${shape}`} role="status" aria-busy="true">
    <span className="sr-only">{label}</span>
    <div className="loading-shapes" aria-hidden="true">
      <div className="skeleton-line" /><div className="skeleton-line skeleton-short" />
      {shape === 'cards' ? <div className="loading-cover skeleton-block" />
        : shape === 'form' ? <><div className="loading-field skeleton-block" /><div className="loading-field skeleton-block" /></>
          : <><div className="skeleton-line" /><div className="skeleton-line skeleton-short" /></>}
    </div>
  </div>;
}
