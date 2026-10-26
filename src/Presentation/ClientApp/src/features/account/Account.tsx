import { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router';
import { api, apiResponse, identityEpoch, isAbort, signOut, useIdentity } from '../../api/client';
import type { Answers, Subscriber } from '../../api/models';
import { usePersonal } from '../../app/Personal';
import { Notice } from '../../components/Notice';
import { AuthForm } from './AuthForm';

interface ImportPreview { account: Answers | null; guest: Answers | null; conflicts: boolean; policy: string; }
export default function Account() {
  const identity = useIdentity(), personal = usePersonal(), navigate = useNavigate(), location = useLocation(), [search] = useSearchParams();
  const [profile, setProfile] = useState<Subscriber | null>(identity.user), [error, setError] = useState<unknown>(null), [message, setMessage] = useState('');
  const [preview, setPreview] = useState<ImportPreview | null>(null), [working, setWorking] = useState(false);
  const allowed = ['/discover', '/taste', '/history', '/account'];
  const returnTo = allowed.includes(search.get('returnTo') ?? '') ? search.get('returnTo')! : '/discover';
  const stateMessage = typeof location.state?.notice === 'string' ? location.state.notice : '';
  useEffect(() => {
    setPreview(null); setProfile(identity.user);
    if (!identity.user) return;
    const controller = new AbortController();
    api<Subscriber>('/api/auth/me', { signal: controller.signal }).then(setProfile).catch(e => { if (!isAbort(e)) setError(e); });
    return () => controller.abort();
  }, [identity.epoch]);
  async function run(operation: () => Promise<void>) {
    if (working || personal.busy) return;
    setWorking(true); setError(null); setMessage('');
    try { await operation(); } catch (e) { if (!isAbort(e)) setError(e); } finally { setWorking(false); }
  }
  async function download() {
    const epoch = identityEpoch();
    const response = await apiResponse(identity.user ? '/api/export/my-data' : '/api/discovery/session/data');
    const file = await response.blob();
    if (epoch !== identityEpoch()) throw new DOMException('Session changed', 'AbortError');
    const url = URL.createObjectURL(file), link = document.createElement('a');
    link.href = url; link.download = 'liner-notes-data.json'; document.body.append(link); link.click(); link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000); setMessage('Your data download is ready.');
  }
  async function remove() {
    const account = !!identity.user;
    if (!window.confirm(account ? 'Delete this account and its stored data? This cannot be undone.' : 'Clear this guest session, taste and history? This cannot be undone.')) return;
    await run(async () => {
      const result = await personal.exclusive(() => account
        ? api<{ accountDeleted?: boolean; localCopiesDeleted?: boolean } | undefined>('/api/subscribers/me', { method: 'DELETE' })
        : personal.write<undefined>('/api/discovery/session', undefined, 'DELETE'));
      const incomplete = result && 'localCopiesDeleted' in result && result.localCopiesDeleted === false;
      signOut(); navigate('/account', { replace: true, state: { notice: incomplete ? 'The account was deleted. Some saved local email copies could not be removed; operator cleanup is still required.' : account ? 'Your account and its stored data were deleted.' : 'Your guest session was cleared.' } });
    });
  }
  return <main id="main-content" className="page-container personal-main account-main" tabIndex={-1}>
    <header className="account-heading"><p className="editorial-meta coral-meta">Your corner of Liner Notes</p><h1>Your account</h1><p>Keep your discoveries, understand your taste, and stay in control of your data.</p></header>
    {!!stateMessage && <Notice>{stateMessage}</Notice>}
    {!identity.user ? <div className={`auth-layout${search.get('mode') === 'register' ? ' registration-first' : ''}`}><AuthForm returnTo={returnTo} /><AuthForm register returnTo={returnTo} /></div>
      : <section className="editor-panel account-profile" aria-label="Account details"><div><h2>Good to have you here.</h2><p>{profile?.email ?? identity.user.email}</p></div><button className="text-button" type="button" onClick={() => { signOut(); navigate('/account'); }}>Sign out</button>
        <dl><div><dt>Time zone</dt><dd>{profile?.timeZone ?? 'Not available'}</dd></div><div><dt>Weekly email preference</dt><dd>{profile?.weeklyEmailOptIn ? 'Opted in' : 'Not opted in'}</dd></div></dl>
        <Link className="text-link" to="/taste">Review your taste →</Link>
      </section>}
    {identity.user && personal.data?.session.guestImportAvailable && <section className="editor-panel import-panel"><h2>You also have guest discoveries</h2><p>Your current account takes priority. Review the guest answers before importing.</p>
      {!preview ? <button type="button" className="button button-secondary" disabled={working || personal.busy} onClick={() => run(async () => setPreview(await api<ImportPreview>('/api/discovery/session/import-preview')))}>Review guest progress</button>
        : <><div className="import-comparison">{([['Account answers', preview.account], ['Guest answers', preview.guest]] as const).map(([label, answers]) => <div key={label}><h3>{label}</h3><dl>{['goal', 'exploration', 'vocals'].map(key => <div key={key}><dt>{key}</dt><dd>{answers?.[key as keyof Answers]?.toString().replaceAll('-', ' ') ?? 'Not answered'}</dd></div>)}</dl></div>)}</div>
          <p>{preview.conflicts ? 'Some answers differ. Your saved account answers will be kept; guest answers fill only unanswered questions.' : 'Your account answers will be kept, with unanswered questions filled from guest progress.'}</p>
          <div className="action-row"><button type="button" className="button button-primary" disabled={working || personal.busy} onClick={() => run(async () => { await personal.exclusive(() => personal.write('/api/discovery/session/import')); setPreview(null); await personal.reload(); setMessage('Guest progress imported.'); })}>Import guest progress</button><button type="button" className="text-button" onClick={() => setPreview(null)}>Keep them separate</button></div></>}
    </section>}
    <section className="editor-panel data-panel" aria-labelledby="your-data"><h2 id="your-data">Your data</h2>
      {identity.user || personal.data?.session.profile ? <><p>{identity.user ? 'Download the account, taste, recommendation and feedback records provided by your JSON export.' : 'Guest taste and history are stored on the server, linked to a credential in this browser. They expire after seven inactive days.'}</p>
        <div className="action-row"><button className="button button-secondary" type="button" disabled={working || personal.busy} onClick={() => run(download)}>Download your data</button><Link className="text-link" to="/history">Review discovery history</Link></div>
        <div className="delete-section"><h3>{identity.user ? 'Delete account' : 'Clear guest session'}</h3><p>{identity.user ? 'Delete your account credentials and stored account data. This cannot be undone.' : 'Remove this guest profile, taste selections and discovery history.'}</p><button className="text-button danger-button" type="button" disabled={working || personal.busy} onClick={remove}>{identity.user ? 'Delete my account' : 'Clear my guest data'}</button></div></>
        : <p>No guest taste or discoveries are stored for this browser yet. You can explore Home without creating a profile.</p>}
    </section>
    {!!error && <Notice error={error} onRetry={() => personal.reload().catch(setError)} />}<p role="status" className="field-help">{working ? 'Working on your request…' : message}</p>
  </main>;
}
