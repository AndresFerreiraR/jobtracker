import { test, expect } from '@playwright/test';
import { JobsListPage } from '../pom/jobs-list.page';
import { JobDetailsPage } from '../pom/job-details.page';

const RUN_ID = Date.now().toString(36);

function isoDate(offsetHours = 24): string {
  const d = new Date(Date.now() + offsetHours * 3600 * 1000);
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

test.describe('Job lifecycle (happy path)', () => {
  test('create → schedule → start → complete', async ({ page }) => {
    const list = new JobsListPage(page);
    const details = new JobDetailsPage(page);

    await list.goto();

    const title = `E2E Roof Job ${RUN_ID}`;
    await list.createJob({
      title,
      description: 'Playwright happy-path E2E',
      customerId: '00000000-0000-0000-0000-000000000042',
      street: '742 Evergreen Terrace',
      city: 'Springfield',
      state: 'IL',
      zipCode: '62704',
    });

    await expect(page.getByRole('link', { name: title })).toBeVisible();

    await list.openJob(title);
    await expect(details.heading).toHaveText(title);

    await details.schedule(isoDate(24));
    await expect(page.getByText(/scheduled/i).first()).toBeVisible();

    await details.start();
    await expect(page.getByText(/in ?progress/i).first()).toBeVisible();

    await details.completeWithSignature();
    await expect(page.getByText(/completed/i).first()).toBeVisible();
  });
});
