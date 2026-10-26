import { useEffect, useRef } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, Link, Outlet, RouterProvider, useLocation, useRouteError } from 'react-router';
import { Header } from './app/Header';
import { Home } from './features/home/Home';
import { PersonalLayout } from './app/Personal';
import './styles.css';

function NotFound() { return <main id="main-content" className="page-container home-main" tabIndex={-1}><h1>Page not found</h1><p><Link to="/">Return Home</Link></p></main>; }

function Shell() {
  const { pathname } = useLocation(), previous = useRef('');
  useEffect(() => {
    const titles: Record<string, string> = { '/': 'Home', '/discover': 'Discover', '/taste': 'Your taste', '/history': 'History', '/account': 'Account' };
    document.title = `${titles[pathname] ?? (pathname.startsWith('/history/') ? 'Past discovery' : 'Page not found')} — Liner Notes`;
    if (previous.current && previous.current !== pathname) {
      window.scrollTo(0, 0);
      requestAnimationFrame(() => document.getElementById('main-content')?.focus());
    }
    previous.current = pathname;
  }, [pathname, previous]);
  return <><a className="skip-link" href="#main-content">Skip to content</a><Header /><Outlet /></>;
}
function RouteError() {
  useRouteError();
  return <main id="main-content" className="page-container personal-main"><h1>This page could not open</h1><p>Please reload to try again. Your saved discoveries remain on the server.</p><button className="button button-secondary" type="button" onClick={() => location.reload()}>Reload page</button></main>;
}
const router = createBrowserRouter([{ element: <Shell />, errorElement: <RouteError />, children: [
  { index: true, element: <Home /> },
  { element: <PersonalLayout />, children: [
    { path: 'discover', lazy: async () => ({ Component: (await import('./features/discovery/Discover')).default }) },
    { path: 'taste', lazy: async () => ({ Component: (await import('./features/taste/Taste')).default }) },
    { path: 'history', lazy: async () => ({ Component: (await import('./features/history/History')).default }) },
    { path: 'account', lazy: async () => ({ Component: (await import('./features/account/Account')).default }) },
    { path: 'history/:batchId', lazy: async () => ({ Component: (await import('./features/discovery/Discover')).default }) },
  ] },
  { path: '*', element: <NotFound /> },
] }]);
createRoot(document.getElementById('root')!).render(<RouterProvider router={router} />);
