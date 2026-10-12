import { test, expect, type Page } from '@playwright/test';

// Apresentador e narração. O som não precisa sair: o play/pause do navegador é trocado por um
// registro (window.__falas), e o teste confere o que tocou e o que parou.

const balao = (page: Page) => page.getByRole('dialog');
const guia = (page: Page) => page.locator('.tutor-presenter');

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    const w = window as unknown as { __falas: string[] };
    w.__falas = [];
    HTMLMediaElement.prototype.play = function () {
      w.__falas.push(new URL(this.src).pathname);
      return Promise.resolve();
    };
    HTMLMediaElement.prototype.pause = function () {
      w.__falas.push('pausa');
    };
  });
});

const falas = (page: Page) => page.evaluate(() => (window as unknown as { __falas: string[] }).__falas);

async function abrirTour(page: Page) {
  await page.goto('/apresentador');
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Oi!' })).toBeVisible();
}

test('@smoke apresentador aparece no canto e narra cada passo', async ({ page }) => {
  await abrirTour(page);
  await expect(guia(page)).toHaveAttribute('src', 'apresentador/guia-acena.svg');
  await expect.poll(() => falas(page)).toEqual(['/apresentador/passo-1.mp3']);

  // Não cobre o balão.
  const img = (await guia(page).boundingBox())!;
  const caixa = (await balao(page).boundingBox())!;
  expect(img.x >= caixa.x + caixa.width || img.x + img.width <= caixa.x || img.y >= caixa.y + caixa.height).toBe(true);

  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Menu' })).toBeVisible();
  await expect(guia(page)).toHaveAttribute('src', 'apresentador/guia.svg');
  await expect.poll(() => falas(page)).toEqual(['/apresentador/passo-1.mp3', 'pausa', '/apresentador/passo-2.mp3']);
});

test('passo sem áudio não mostra Silenciar e para a fala anterior', async ({ page }) => {
  await abrirTour(page);
  for (const passo of ['2 de 4', '3 de 4', '4 de 4']) {
    await balao(page).getByRole('button', { name: 'Próximo' }).click();
    await expect(balao(page).getByText(passo)).toBeVisible();
  }
  await expect(balao(page).getByRole('button', { name: 'Silenciar' })).toHaveCount(0);
  await expect(guia(page)).toBeVisible();
  await expect.poll(async () => (await falas(page)).at(-1)).toBe('pausa');
});

test('silenciar para a fala e o próximo passo fica quieto até reativar', async ({ page }) => {
  await abrirTour(page);
  await balao(page).getByRole('button', { name: 'Silenciar' }).click();
  await expect(balao(page).getByRole('button', { name: 'Ativar som' })).toBeFocused();
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Menu' })).toBeVisible();
  expect(await falas(page)).toEqual(['/apresentador/passo-1.mp3', 'pausa']);

  await balao(page).getByRole('button', { name: 'Ativar som' }).click();
  await expect.poll(async () => (await falas(page)).at(-1)).toBe('/apresentador/passo-2.mp3');
});

test('ocultar o Guia tira imagem e voz, e a escolha vale ao retomar', async ({ page }) => {
  await abrirTour(page);
  await balao(page).getByRole('button', { name: 'Ocultar Guia' }).click();
  await expect(guia(page)).toHaveCount(0);
  await expect(balao(page).getByRole('button', { name: 'Mostrar Guia' })).toBeFocused();
  await expect(balao(page).getByRole('button', { name: 'Silenciar' })).toHaveCount(0);

  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Menu' })).toBeVisible();
  expect(await falas(page)).toEqual(['/apresentador/passo-1.mp3', 'pausa']);

  await page.keyboard.press('Escape');
  await expect(balao(page)).toBeHidden();
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Oi!' })).toBeVisible();
  await expect(balao(page).getByRole('button', { name: 'Mostrar Guia' })).toBeVisible();
  await expect(guia(page)).toHaveCount(0);
});

test('tela estreita esconde o apresentador e mantém a faixa', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 667 });
  await abrirTour(page);
  await expect(page.locator('.tutor-popover--sheet')).toBeVisible();
  await expect(guia(page)).toHaveCount(0);
});

// #47: a pose animada vale só para quem não pediu menos movimento; o navegador escolhe pelo <picture>.
const imagemCarregada = (page: Page) =>
  guia(page).evaluate(img => new URL((img as HTMLImageElement).currentSrc).pathname);

test('pose animada no navegador normal', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await abrirTour(page);
  await expect.poll(() => imagemCarregada(page)).toBe('/apresentador/guia-acena-animado.svg');
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect.poll(() => imagemCarregada(page)).toBe('/apresentador/guia-animado.svg');
});

test('pose parada para quem pede menos movimento', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await abrirTour(page);
  await expect.poll(() => imagemCarregada(page)).toBe('/apresentador/guia-acena.svg');
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect.poll(() => imagemCarregada(page)).toBe('/apresentador/guia.svg');
});

test('trocar de passo não tira o apresentador da tela (sem refazer a entrada)', async ({ page }) => {
  await abrirTour(page);
  await expect(guia(page)).toBeVisible();
  await page.evaluate(() => {
    const w = window as unknown as { __saidas: number };
    w.__saidas = 0;
    new MutationObserver(ms => {
      for (const m of ms) m.removedNodes.forEach(n => { if (n.nodeName === 'PICTURE') w.__saidas++; });
    }).observe(document.body, { childList: true, subtree: true });
  });
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Menu' })).toBeVisible();
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Do seu jeito' })).toBeVisible();
  expect(await page.evaluate(() => (window as unknown as { __saidas: number }).__saidas)).toBe(0);
});
