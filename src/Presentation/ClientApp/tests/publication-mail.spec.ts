import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test('privacy and source pages are public without allocating a guest profile', async ({ page }) => {
  const writes: string[] = [];
  page.on('request', request => { if (request.method() !== 'GET' && request.method() !== 'HEAD') writes.push(request.url()); });
  await page.route('**/api/public-information', route => route.fulfill({ json: { operatorName: 'Test operator', privacyContact: 'mailto:privacy@example.test', emailEnabled: false, mailProcessor: null, policyVersion: '2026-11-13' } }));
  await page.goto('/privacy');
  await expect(page.getByRole('heading', { name: 'Privacy and your data', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Contact the operator privately' })).toHaveAttribute('href', 'mailto:privacy@example.test');
  await expect(page.getByText('External email delivery is not enabled on this instance.', { exact: false })).toBeVisible();
  await page.getByRole('navigation', { name: 'Service information' }).getByRole('link', { name: 'Music sources' }).click();
  await expect(page.getByRole('heading', { name: 'Music sources and rights', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'CC BY-NC-SA 3.0', exact: true })).toBeVisible();
  expect(writes).toEqual([]);
  expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag22aa']).analyze()).violations).toEqual([]);
});

test('confirmation removes the fragment and needs an explicit submission', async ({ page }) => {
  let requests = 0;
  await page.route('**/api/account-email/confirm', async route => {
    requests++;
    expect(route.request().postDataJSON()).toEqual({ userId: '11111111-1111-4111-8111-111111111111', token: 'fixture-capability' });
    await route.fulfill({ status: 204 });
  });
  await page.goto('/account/confirm-email#id=11111111-1111-4111-8111-111111111111&token=fixture-capability');
  await expect(page.getByRole('heading', { name: 'Confirm your email' })).toBeVisible();
  expect(new URL(page.url()).hash).toBe(''); expect(requests).toBe(0);
  await page.getByRole('button', { name: 'Confirm email', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Email confirmed'); expect(requests).toBe(1);
});

test('recovery accepts password managers, posts the capability and clears credentials', async ({ page }) => {
  let requests = 0;
  await page.route('**/api/account-email/reset', async route => {
    requests++;
    expect(route.request().postDataJSON()).toMatchObject({ token: 'fixture-recovery', password: 'A long fixture password' });
    await route.fulfill({ status: 204 });
  });
  await page.goto('/account/reset-password#id=11111111-1111-4111-8111-111111111111&token=fixture-recovery');
  await page.getByLabel('New password', { exact: true }).fill('A long fixture password');
  expect((await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag22aa']).analyze()).violations).toEqual([]);
  expect(new URL(page.url()).hash).toBe(''); expect(requests).toBe(0);
  await page.getByRole('button', { name: 'Save new password' }).click();
  await expect(page.getByRole('status')).toContainText('Sign in again');
  expect(requests).toBe(1);
});

test('unknown addresses receive the same recovery acknowledgement', async ({ page }) => {
  await page.route('**/api/account-email/request-reset', route => route.fulfill({ json: { message: 'Generic acknowledgement' } }));
  await page.goto('/account/forgot-password');
  await page.getByLabel('Email', { exact: true }).fill('unknown@example.test');
  await page.getByRole('button', { name: 'Send recovery message' }).click();
  await expect(page.getByRole('status')).toContainText('If this address is eligible');
});

test('incomplete and expired recovery links have a useful recovery path', async ({ page }) => {
  await page.goto('/account/reset-password');
  await expect(page.getByRole('alert')).toContainText('missing or incomplete');
  await page.route('**/api/account-email/reset', route => route.fulfill({ status: 400, json: {} }));
  await page.goto('/account/reset-password#id=11111111-1111-4111-8111-111111111111&token=expired');
  await page.getByLabel('New password', { exact: true }).fill('Fixture password long enough');
  await page.getByRole('button', { name: 'Save new password' }).click();
  await expect(page.getByRole('alert')).toContainText('invalid or expired');
  await expect(page.getByRole('link', { name: 'Request a new recovery link' })).toBeVisible();
});
