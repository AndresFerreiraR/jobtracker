import type { Locator, Page } from '@playwright/test';

export class JobsListPage {
  readonly heading: Locator;
  readonly createTitle: Locator;
  readonly createDescription: Locator;
  readonly createCustomerId: Locator;
  readonly createStreet: Locator;
  readonly createCity: Locator;
  readonly createState: Locator;
  readonly createZip: Locator;
  readonly createSubmit: Locator;
  readonly statusFilter: Locator;
  readonly resetFilters: Locator;

  constructor(public readonly page: Page) {
    this.heading = page.getByRole('heading', { name: /^jobs$/i });
    this.createTitle = page.getByLabel(/title/i);
    this.createDescription = page.getByLabel(/description/i);
    this.createCustomerId = page.getByLabel(/customer id/i);
    this.createStreet = page.getByLabel(/street/i);
    this.createCity = page.getByLabel(/city/i);
    this.createState = page.getByLabel(/state/i);
    this.createZip = page.getByLabel(/zip/i);
    this.createSubmit = page.getByRole('button', { name: /create job/i });
    this.statusFilter = page.getByLabel(/status/i).first();
    this.resetFilters = page.getByRole('button', { name: /reset/i });
  }

  async goto() {
    await this.page.goto('/jobs');
    await this.heading.waitFor();
  }

  async openJob(title: string) {
    await this.page.getByRole('link', { name: title }).click();
  }

  async createJob(input: {
    title: string;
    description: string;
    customerId: string;
    street: string;
    city: string;
    state: string;
    zipCode: string;
  }) {
    await this.createTitle.fill(input.title);
    await this.createDescription.fill(input.description);
    await this.createCustomerId.fill(input.customerId);
    await this.createStreet.fill(input.street);
    await this.createCity.fill(input.city);
    await this.createState.fill(input.state);
    await this.createZip.fill(input.zipCode);
    await this.createSubmit.click();
  }
}
