import { useEffect, useRef, useState } from 'react';
import { Link, NavLink } from 'react-router';
import logo from '../assets/liner-notes-logo.png';
import { useIdentity } from '../api/client';

const personalLinks = [
  ['Discover', '/discover'], ['Your taste', '/taste'], ['History', '/history'],
] as const;

export function Header() {
  const identity = useIdentity();
  const [open, setOpen] = useState(false);
  const menuButton = useRef<HTMLButtonElement>(null);
  const header = useRef<HTMLElement>(null);
  useEffect(() => {
    const close = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && open) { setOpen(false); menuButton.current?.focus(); }
    };
    const outside = (event: PointerEvent) => {
      if (event.target instanceof Node && !header.current?.contains(event.target)) setOpen(false);
    };
    const breakpoint = matchMedia('(min-width: 768px)');
    const resize = () => { if (breakpoint.matches) setOpen(false); };
    document.addEventListener('keydown', close);
    document.addEventListener('pointerdown', outside);
    breakpoint.addEventListener('change', resize);
    return () => {
      document.removeEventListener('keydown', close);
      document.removeEventListener('pointerdown', outside);
      breakpoint.removeEventListener('change', resize);
    };
  }, [open]);
  return <header className="site-header" ref={header}>
    <div className="page-container header-inner">
      <div className="header-left">
        <Link to="/" className="brand" aria-label="Liner Notes home" translate="no"><img src={logo} width="2172" height="724" alt="Liner Notes" decoding="async" /></Link>
        <nav className="desktop-nav" aria-label="Main navigation">
          <NavLink to="/" end className={({ isActive }) => `nav-link${isActive ? ' nav-current' : ''}`}>Home</NavLink>
          {personalLinks.map(([label, href]) => <NavLink className={({ isActive }) => `nav-link${isActive ? ' nav-current' : ''}`} key={href} to={href}>{label}</NavLink>)}
        </nav>
      </div>
      <div className="header-actions"><NavLink className="sign-in" to="/account">{identity.user ? 'Account' : 'Sign in'}</NavLink>
      <button className="menu-toggle" type="button" ref={menuButton} aria-label={open ? 'Close navigation' : 'Open navigation'}
        aria-expanded={open} aria-controls="mobile-navigation" onClick={() => setOpen(value => !value)}>
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
          {open ? <path d="m6 6 12 12M6 18 18 6" /> : <path d="M4 6h16M4 12h16M4 18h16" />}
        </svg>
      </button>
      </div>
    </div>
    <nav id="mobile-navigation" className="mobile-nav page-container" aria-label="Mobile navigation" hidden={!open}
      onClick={() => setOpen(false)}
      onBlur={event => { if (!event.currentTarget.contains(event.relatedTarget) && event.relatedTarget !== menuButton.current) setOpen(false); }}>
      <NavLink to="/" end onClick={() => setOpen(false)}>Home</NavLink>
      {personalLinks.map(([label, href]) => <NavLink key={href} to={href}>{label}</NavLink>)}
      <NavLink to="/account">{identity.user ? 'Account' : 'Sign in'}</NavLink>
    </nav>
  </header>;
}
