import { defineConfig, devices } from '@playwright/test';

// Padrão do ecossistema: padrao-rvm § 12 (TASK-833, 14/09/2026). NÃO afrouxar para "resolver" teste
// instável: workers 1, vídeo ligado, retries 2 e waitForTimeout foram exatamente o que deixou o E2E
// do ERPAgro em 5–11 min. Teste que quebra em paralelo tem dependência escondida — conserte o teste.
// Sem projeto `setup`: o samples não tem login.

const porta = 5280;
const baseURL = process.env.BASE_URL ?? `http://127.0.0.1:${porta}`;

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  workers: process.env.CI ? '50%' : undefined,
  retries: process.env.CI ? 1 : 0,
  forbidOnly: !!process.env.CI,
  timeout: 30_000,
  expect: { timeout: 5_000 },
  outputDir: 'test-results',
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    trace: 'on-first-retry',
    video: 'off',
    screenshot: 'only-on-failure',
  },
  // Sobe o samples e espera o /health (ADR-008). Local: reaproveita um samples já rodando.
  webServer: process.env.BASE_URL
    ? undefined
    : {
        command: `dotnet run --project ../samples/RVM.UI.Tutor.Samples -c Release --no-launch-profile --urls ${baseURL}`,
        url: `${baseURL}/health`,
        reuseExistingServer: !process.env.CI,
        // Fora de Development o `dotnet run` NAO serve os static web assets (blazor.web.js, CSS
        // isolado): a pagina abre, mas sem Blazor e com a barra de erro (medido no bootstrap).
        env: { ASPNETCORE_ENVIRONMENT: 'Development' },
        timeout: 180_000,
      },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
