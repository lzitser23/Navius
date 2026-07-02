import { defineConfig, devices } from '@playwright/test';

const PORT = 5247;
// `dotnet` may not be on PATH (e.g. installed user-local). Override with DOTNET_EXE.
const dotnet = process.env.DOTNET_EXE || 'dotnet';

export default defineConfig({
  testDir: './specs',
  timeout: 45_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: `http://localhost:${PORT}`,
    headless: true,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    // cwd is this config's directory (tests/e2e), so reach back to the repo root.
    command: `${dotnet} run --project ../../playground/Navius.Playground/Navius.Playground.csproj -c Debug --urls http://localhost:${PORT}`,
    url: `http://localhost:${PORT}`,
    timeout: 180_000,
    reuseExistingServer: !process.env.CI,
    stdout: 'ignore',
    stderr: 'pipe',
  },
});
