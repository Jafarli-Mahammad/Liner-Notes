import { useEffect, useRef } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, Link, Outlet, RouterProvider, useLocation, useRouteError, useNavigation } from 'react-router';
import { Header } from './app/Header';
import InformationPage from './features/information/Information';
import EmailSecurity from './features/account/EmailSecurity';
import { Home } from './features/home/Home';
import { PersonalLayout } from './app/Personal';
import { Loading } from './components/Loading';
import './styles.css';
import { restoreSession, errorText } from './api/client';

function NotFound() { return <main id="main-content" className="page-container home-main" tabIndex={-1}><h1>Page not found</h1><p><Link to="/">Return Home</Link></p></main>; }

function Shell() {
  const navigation = useNavigation();
  const { pathname } = useLocation(), previous = useRef('');
  useEffect(() => {
    const titles: Record<string, string> = { '/': 'Home', '/discover': 'Discover', '/taste': 'Your taste', '/history': 'History', '/account': 'Account', '/privacy': 'Privacy', '/sources': 'Music sources', '/support': 'Support', '/account/confirm-email': 'Confirm email', '/account/reset-password': 'Reset password', '/account/forgot-password': 'Recover account' };
    document.title = `${titles[pathname] ?? (pathname.startsWith('/history/') ? 'Past discovery' : 'Page not found')} — Liner Notes`;
    if (previous.current && previous.current !== pathname) {
      window.scrollTo(0, 0);
      requestAnimationFrame(() => document.getElementById('main-content')?.focus());
    }
    previous.current = pathname;
  }, [pathname, previous]);
  return <><a className="skip-link" href="#main-content">Skip to content</a><Header />{navigation.state !== 'idle' && <div className="navigation-loading" role="status"><span className="sr-only">Opening page…</span></div>}<Outlet /><footer className="page-container site-footer"><nav aria-label="Service information"><Link to="/privacy">Privacy</Link> · <Link to="/sources">Music sources</Link> · <Link to="/support">Support</Link></nav></footer></>;
}
function RouteError() {
  useRouteError();
  return <main id="main-content" className="page-container personal-main"><h1>This page could not open</h1><p>Please reload to try again. Your saved discoveries remain on the server.</p><button className="button button-secondary" type="button" onClick={() => location.reload()}>Reload page</button></main>;
}
const router = createBrowserRouter([{ element: <Shell />, hydrateFallbackElement: <main id="main-content" className="page-container personal-main"><Loading label="Opening Liner Notes…" /></main>, errorElement: <RouteError />, children: [
  { index: true, element: <Home /> },
  ...['privacy', 'sources', 'support'].map(path => ({ path, element: <InformationPage /> })),
  ...['confirm-email', 'reset-password', 'forgot-password'].map(path => ({ path: 'account/' + path, element: <EmailSecurity /> })),
  { element: <PersonalLayout />, children: [
    { path: 'discover', lazy: async () => ({ Component: (await import('./features/discovery/Discover')).default }) },
    { path: 'taste', lazy: async () => ({ Component: (await import('./features/taste/Taste')).default }) },
    { path: 'history', lazy: async () => ({ Component: (await import('./features/history/History')).default }) },
    { path: 'account', lazy: async () => ({ Component: (await import('./features/account/Account')).default }) },
    { path: 'history/:batchId', lazy: async () => ({ Component: (await import('./features/discovery/Discover')).default }) },
  ] },
  { path: '*', element: <NotFound /> },
] }]);
const root = createRoot(document.getElementById('root')!);
async function start() {
  if (['/privacy', '/sources', '/support', '/account/confirm-email', '/account/reset-password', '/account/forgot-password'].includes(location.pathname)) {
    root.render(<RouterProvider router={router} />);
    void restoreSession().catch(() => {});
    return;
  }
  root.render(<main id="main-content" className="page-container personal-main"><Loading label="Opening Liner Notes…" /></main>);
  try { await restoreSession(); root.render(<RouterProvider router={router} />); }
  catch (error) { root.render(<main id="main-content" className="page-container personal-main"><h1>Your session could not be checked</h1><p role="alert">{errorText(error)}</p><button className="button button-secondary" onClick={() => void start()}>Try again</button></main>); }
}
void start();
