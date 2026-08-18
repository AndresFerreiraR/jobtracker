import type { Locator, Page } from '@playwright/test';

export class JobDetailsPage {
  readonly heading: Locator;
  readonly statusBadge: Locator;
  readonly scheduleButton: Locator;
  readonly startButton: Locator;
  readonly completeButton: Locator;
  readonly cancelButton: Locator;
  readonly scheduledInput: Locator;
  readonly confirmScheduleButton: Locator;
  readonly signaturePad: Locator;
  readonly submitCompleteButton: Locator;
  readonly toastRegion: Locator;

  constructor(public readonly page: Page) {
    this.heading = page.locator('h1');
    this.statusBadge = page.getByTestId('status-badge').or(page.locator('h1').locator('..').locator('[role="status"]'));
    this.scheduleButton = page.getByRole('button', { name: /^schedule$/i });
    this.startButton = page.getByRole('button', { name: /^start$/i });
    this.completeButton = page.getByRole('button', { name: /^complete$/i });
    this.cancelButton = page.getByRole('button', { name: /^cancel$/i });
    this.scheduledInput = page.getByLabel(/scheduled date/i);
    this.confirmScheduleButton = page.getByRole('button', { name: /^confirm$/i });
    this.signaturePad = page.getByRole('img', { name: /signature pad/i });
    this.submitCompleteButton = page.getByRole('button', { name: /complete job/i });
    this.toastRegion = page.getByRole('region', { name: /notifications/i });
  }

  async schedule(datetimeLocal: string) {
    await this.scheduleButton.click();
    await this.scheduledInput.fill(datetimeLocal);
    await this.confirmScheduleButton.click();
  }

  async start() {
    await this.startButton.click();
  }

  async completeWithSignature() {
    await this.completeButton.click();
    const box = await this.signaturePad.boundingBox();
    if (!box) throw new Error('Signature pad not visible');
    await this.page.mouse.move(box.x + 10, box.y + 10);
    await this.page.mouse.down();
    await this.page.mouse.move(box.x + 200, box.y + 80, { steps: 12 });
    await this.page.mouse.move(box.x + 300, box.y + 40, { steps: 12 });
    await this.page.mouse.up();
    await this.submitCompleteButton.click();
  }
}
