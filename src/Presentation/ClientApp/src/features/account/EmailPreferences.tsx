import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router';
import { api } from '../../api/client';
import { Notice } from '../../components/Notice';
interface EmailStatus { confirmed: boolean; optedIn: boolean; suppressed: boolean; emailEnabled: boolean; }
export function EmailPreferences({ email }: { email: string }) {
  const [status, setStatus] = useState<EmailStatus | null>(null), [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null), [message, setMessage] = useState('');
  const load = useCallback(async () => { setStatus(await api<EmailStatus>('/api/account-email/status')); }, []);
  useEffect(() => { load().catch(setError); }, [load]);
  async function save(optIn?: boolean) {
    if (busy) return; const previous = status; let saved = false;
    if (optIn !== undefined) setStatus(value => value ? { ...value, optedIn: optIn } : value);
    setBusy(true); setError(null); setMessage('');
    try {
      if (optIn === undefined) {
        await api('/api/account-email/request-confirmation', { method: 'POST', body: { email } });
        setMessage('If eligible, a confirmation message will be sent. Check your inbox and spam folder. Wait at least a minute before requesting another.');
      } else {
        await api('/api/account-email/preference', { method: 'POST', body: { optIn } });
        saved = true;
        await load(); setMessage(optIn ? 'Weekly email opt-in saved. Delivery also requires a confirmed address.' : 'Weekly emails stopped. Messages already accepted cannot be recalled.');
      }
    } catch (value) { if (!saved && optIn !== undefined) setStatus(previous); setError(value); } finally { setBusy(false); }
  }
  return <section aria-labelledby="email-preferences" className="email-preferences"><h3 id="email-preferences">Email and delivery</h3>
    {status && <><p>Email address: {status.confirmed ? 'Confirmed' : 'Not yet confirmed'}. {!status.emailEnabled && 'External delivery is not enabled on this instance.'}</p>
      {!status.confirmed && <button type="button" className="button button-secondary" disabled={busy || !status.emailEnabled} onClick={() => void save()}>Send confirmation email</button>}
      <label className="checkbox-label"><input type="checkbox" checked={status.optedIn} disabled={busy} onChange={event => void save(event.target.checked)} />Receive weekly recommendations by email</label>
      <p className="field-help">Delivery uses the saved weekday and UTC hour. Confirmation does not opt you in automatically.</p>
      {status.suppressed && <p>Email delivery is paused after a delivery failure or complaint. <Link to="/support">Contact the operator privately</Link>.</p>}
    </>}
    {!!error && <Notice error={error} onRetry={() => load().catch(setError)} />}<p role="status">{busy ? 'Saving…' : message}</p>
  </section>;
}
