// Screenshots para a entrega (regra do ecossistema: tela que mudou vai com imagem).
// Uso: BASE_URL=http://127.0.0.1:5280 OUT=<pasta> node screenshots.mjs
import { chromium } from '@playwright/test';

const base = process.env.BASE_URL ?? 'http://127.0.0.1:5280';
const out = process.env.OUT ?? 'screenshots';
const browser = await chromium.launch();
const visible = page => page.locator('.tutor-popover:not([style*="hidden"])').waitFor();
const shot = (page, name) => page.screenshot({ path: `${out}/${name}.png`, animations: 'disabled' });

async function newPage(options = {}) {
  return browser.newPage({ viewport: { width: 1280, height: 720 }, ...options });
}

for (const scheme of ['light', 'dark']) {
  const page = await newPage({ colorScheme: scheme });
  await page.goto(`${base}/tour-simples`);
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await page.getByRole('button', { name: 'Próximo' }).click();
  await page.getByRole('heading', { name: 'Novo lançamento' }).waitFor();
  await visible(page);
  await shot(page, `tour-passo-2-${scheme}`);

  await page.goto(`${base}/destaque`);
  await page.getByRole('button', { name: 'Salvar lançamento' }).click();
  await visible(page);
  await shot(page, `destaque-${scheme}`);
  await page.close();
}

// Fase 2
{
  const page = await newPage();
  await page.goto(`${base}/espera-acao`);
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await visible(page);
  await shot(page, 'espera-clique');
  await page.getByRole('button', { name: 'Nova obra' }).click();
  await page.getByRole('heading', { name: 'Dê um nome' }).waitFor();
  await visible(page);
  await shot(page, 'espera-campo');

  await page.goto(`${base}/multi-pagina`);
  await page.getByRole('button', { name: 'Iniciar tour entre páginas' }).click();
  await page.getByRole('button', { name: 'Próximo' }).click();
  await page.getByRole('heading', { name: 'Suas obras' }).waitFor();
  await visible(page);
  await shot(page, 'multi-pagina-passo-2');

  await page.goto(`${base}/tema`);
  await page.getByLabel('Personalizado (variáveis --tutor-*)').check();
  await page.getByRole('button', { name: 'Ver o tour neste tema' }).click();
  await visible(page);
  await shot(page, 'tema-personalizado');

  await page.goto(`${base}/wasm/tour-simples`);
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await visible(page);
  await shot(page, 'wasm-tour');
  await page.close();
}
{
  const page = await newPage({ viewport: { width: 375, height: 667 } });
  await page.goto(`${base}/tour-simples`);
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await page.getByRole('button', { name: 'Próximo' }).click();
  await page.getByRole('heading', { name: 'Novo lançamento' }).waitFor();
  await page.locator('.tutor-popover--sheet').waitFor();
  await shot(page, 'celular-faixa-inferior');
  await page.close();
}
// Apresentador (TUTOR #38)
for (const scheme of ['light', 'dark']) {
  const page = await newPage({ colorScheme: scheme });
  await page.goto(`${base}/apresentador`);
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await page.locator('.tutor-presenter').first().waitFor({ state: 'attached' });
  await visible(page);
  await shot(page, `apresentador-passo-1-${scheme}`);
  for (let i = 0; i < 2; i++) await page.getByRole('button', { name: 'Próximo' }).click();
  await page.getByRole('heading', { name: 'Quando quiser parar' }).waitFor();
  await visible(page);
  await shot(page, `apresentador-passo-3-${scheme}`);
  await page.close();
}
await browser.close();
