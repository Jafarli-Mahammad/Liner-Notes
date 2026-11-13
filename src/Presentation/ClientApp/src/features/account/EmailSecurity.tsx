import { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router';
import { api, ApiError, signOut } from '../../api/client';
import { Notice } from '../../components/Notice';

export default function EmailSecurity() {
  const { pathname, hash } = useLocation();
  const confirm = pathname.endsWith('/confirm-email'), reset = pathname.endsWith('/reset-password');
  const [capability, setCapability] = useState(() => {
    const params = new URLSearchParams(window.location.hash.slice(1));
    const value = { userId: params.get('id') ?? '', token: params.get('token') ?? '' };
    if (window.location.hash) history.replaceState(history.state, '', window.location.pathname);
    return value;
  });
  useEffect(() => {
    if (!hash) return;
    const params = new URLSearchParams(hash.slice(1));
    setCapability({ userId: params.get('id') ?? '', token: params.get('token') ?? '' });
    history.replaceState(history.state, '', window.location.pathname);
  }, [hash]);
  const [email, setEmail] = useState(''), [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false), [done, setDone] = useState(false), [error, setError] = useState<unknown>(null);
  const title = confirm ? 'Confirm your email' : reset ? 'Choose a new password' : 'Reset your password';
  async function submit(event: React.FormEvent) {
    event.preventDefault(); if (busy) return;
    setBusy(true); setError(null);
    try {
      if (confirm || reset) {
        await api('/api/account-email/' + (confirm ? 'confirm' : 'reset'), { method: 'POST', body: { ...capability, ...(reset ? { password } : {}) } });
        setCapability({ userId: '', token: '' }); setPassword('');
        if (reset) signOut();
      } else await api('/api/account-email/request-reset', { method: 'POST', body: { email: email.trim() } });
      setDone(true);
    } catch (value) { setError(value instanceof ApiError && value.status === 400 ? new Error('This link is invalid or expired, or the password was not accepted. Request a new message.') : value); }
    finally { setBusy(false); }
  }
  const missing = (confirm || reset) && (!capability.token || !capability.userId);
  return <main id="main-content" className="page-container personal-main account-main" tabIndex={-1}>
    <section className="editor-panel auth-panel"><h1>{title}</h1>
      {done ? <><p role="status">{confirm ? 'Email confirmed. Your weekly email choice has not changed.' : reset ? 'Password changed. Sign in again with your new password.' : 'If this address is eligible, a recovery message will be sent. Check your inbox and spam folder.'}</p><Link to="/account">Return to your account</Link></>
        : <><p>{confirm ? 'Submit below to confirm the address from your email. Opening this page alone does not change your account.' : reset ? 'Use at least 12 characters. Password managers and paste are welcome.' : 'Enter your account email address. We will not reveal whether an account exists.'}</p>
          {missing ? <p role="alert">The link is missing or incomplete. Open the full link in your email or request a new message from Account.</p> : <form onSubmit={submit} aria-busy={busy}>
            {!confirm && !reset && <label htmlFor="recovery-email">Email<input id="recovery-email" type="email" autoComplete="email" required maxLength={256} value={email} onChange={event => setEmail(event.target.value)} /></label>}
            {reset && <label htmlFor="recovery-password">New password<input id="recovery-password" type="password" autoComplete="new-password" required minLength={12} maxLength={1024} value={password} onChange={event => setPassword(event.target.value)} /></label>}
            <button className="button button-primary" type="submit" disabled={busy}>{busy ? 'Saving…' : confirm ? 'Confirm email' : reset ? 'Save new password' : 'Send recovery message'}</button>
          </form>}
          <p><Link to="/account">Return to Account</Link>{reset && <> · <Link to="/account/forgot-password">Request a new recovery link</Link></>}</p>
        </>}
      {!!error && <Notice error={error} />}
    </section>
  </main>;
}
