import { useEffect, useState } from 'react';
import { ApiError } from '../api/client';

export function useRetryDelay(error: unknown) {
  const deadline = error instanceof ApiError ? error.retryAt : null;
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    setNow(Date.now());
    if (!deadline || deadline <= Date.now()) return;
    const timer = setInterval(() => { const time = Date.now(); setNow(time); if (time >= deadline) clearInterval(timer); }, 1000);
    return () => clearInterval(timer);
  }, [deadline]);
  return deadline ? Math.max(0, Math.ceil((deadline - now) / 1000)) : 0;
}
