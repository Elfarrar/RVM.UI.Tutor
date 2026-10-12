import { test, expect } from '@playwright/test';

test('@smoke samples abre a página inicial', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'RVM.UI.Tutor' })).toBeVisible();
  // Sem blazor.web.js ou sem o CSS isolado a barra de erro aparece: o titulo sozinho nao prova nada.
  await expect(page.locator('#blazor-error-ui')).toBeHidden();
});

test('@smoke /health responde Healthy', async ({ request }) => {
  const resposta = await request.get('/health');
  expect(resposta.ok()).toBeTruthy();
  expect(await resposta.text()).toBe('Healthy');
});
