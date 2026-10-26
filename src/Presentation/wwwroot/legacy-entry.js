// Keep deferred personal routes on their existing interface during the Home checkpoint.
window.addEventListener('DOMContentLoaded', () => {
  const tab = location.hash.slice(1);
  if (['tab-discovery', 'tab-seeding', 'tab-auth', 'tab-your-data'].includes(tab)) {
    document.querySelector(`[data-nav-tab][aria-controls="${tab}"]`)?.click();
  }
});
