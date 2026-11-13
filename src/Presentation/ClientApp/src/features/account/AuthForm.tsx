import { useId, useRef, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { authenticate, ApiError, isAbort } from '../../api/client';
import { usePersonal } from '../../app/Personal';
import { Notice } from '../../components/Notice';
import { useRetryDelay } from '../../hooks/useRetryDelay';

export function AuthForm({ register, returnTo }: { register?: boolean; returnTo: string }) {
  const personal = usePersonal(), navigate = useNavigate(), id = useId();
  const [showPassword, setShowPassword] = useState(false);
  const [email, setEmail] = useState(''), [password, setPassword] = useState(''), [name, setName] = useState('');
  const [preserve, setPreserve] = useState(true), [optIn, setOptIn] = useState(false), [busy, setBusy] = useState(false), [error, setError] = useState<unknown>(null);
  const locked = useRef(false), retry = useRetryDelay(error), errorBox = useRef<HTMLDivElement>(null);
  const guest = personal.data?.session.profile?.isGuest;
  const invalid = (field: string) => error instanceof ApiError && Object.keys(error.fields).some(key => key.toLowerCase().endsWith(field));
  async function submit(event: FormEvent) {
    event.preventDefault(); if (locked.current) return;
    locked.current = true; setBusy(true); setError(null);
    try {
      await personal.exclusive(async () => {
        const body = register ? { userName: name.trim(), email: email.trim(), password, preserveGuestProgress: preserve,
          timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC', weeklyEmailOptIn: optIn } : { email: email.trim(), password };
        await authenticate(register ? 'register' : 'login', body, personal.data?.session.csrfToken);
      });
      setPassword(''); navigate(returnTo, { replace: true });
    } catch (e) {
      if (!isAbort(e)) {
        setError(!register && e instanceof ApiError && e.status === 401 ? new ApiError(401, 'The email or password was not accepted. Please try again.') : e);
        requestAnimationFrame(() => errorBox.current?.focus());
      }
    } finally { locked.current = false; setBusy(false); }
  }
  return <section className="editor-panel auth-panel" aria-labelledby={`${id}-heading`}>
    <p className="auth-eyebrow">{register ? 'A place for your discoveries' : 'Welcome back'}</p>
    <h2 id={`${id}-heading`}>{register ? 'Create an account' : 'Sign in'}</h2><p className="panel-intro">{register ? 'Keep your taste and discoveries together, wherever you find music.' : 'Welcome back. Your discoveries are waiting.'}</p>
    <form onSubmit={submit} aria-labelledby={`${id}-heading`} aria-busy={busy}>
      {register && <label htmlFor={`${id}-name`}>Username<input id={`${id}-name`} autoComplete="username" maxLength={256} required value={name} onChange={e => setName(e.target.value)} aria-invalid={invalid('username')} aria-describedby={invalid('username') ? `${id}-error` : undefined} /></label>}
      <label htmlFor={`${id}-email`}>Email<input id={`${id}-email`} type="email" autoComplete="email" required maxLength={256} value={email} onChange={e => setEmail(e.target.value)} aria-invalid={invalid('email')} aria-describedby={invalid('email') ? `${id}-error` : undefined} /></label>
      <label htmlFor={`${id}-password`}>Password<span className="password-field"><input id={`${id}-password`} aria-label="Password" type={showPassword ? 'text' : 'password'} autoComplete={register ? 'new-password' : 'current-password'} required minLength={register ? 12 : undefined} value={password} onChange={e => setPassword(e.target.value)} aria-invalid={invalid('password')} aria-describedby={register ? `${id}-password-help` : invalid('password') ? `${id}-error` : undefined} /><button className="password-toggle" type="button" aria-label={showPassword ? 'Hide password' : 'Show password'} aria-pressed={showPassword} onClick={() => setShowPassword(value => !value)}>{showPassword ? 'Hide' : 'Show'}</button></span></label>
      {register && <><p id={`${id}-password-help`} className="field-help">Use at least 12 characters. Password managers and paste are welcome.</p>
        {guest && <label className="checkbox-label"><input type="checkbox" checked={preserve} onChange={e => setPreserve(e.target.checked)} />Keep my guest taste and discovery history</label>}
        <label className="checkbox-label"><input type="checkbox" checked={optIn} onChange={e => setOptIn(e.target.checked)} />Opt in to weekly emails when delivery becomes available</label></>}
      <button className="button button-primary" disabled={busy || personal.busy || retry > 0} type="submit">{busy ? (register ? 'Creating your account…' : 'Signing in…') : retry ? `Try again in ${retry}s` : register ? 'Create account' : 'Sign in'}</button>
      {!!error && <div ref={errorBox} id={`${id}-error`} tabIndex={-1}><Notice error={error} onRetry={error instanceof ApiError && error.status === 403 ? () => personal.reload().catch(setError) : undefined} /></div>}
    </form>
    {register ? <p className="field-help">Read our <Link to="/privacy">privacy information</Link> and <Link to="/sources">music sources</Link>.</p> : <p><Link to="/account/forgot-password">Forgot your password?</Link></p>}
  </section>;
}
