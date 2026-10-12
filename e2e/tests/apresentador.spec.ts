import { test, expect, type Page } from '@playwright/test';

// Apresentador e narração. O som não precisa sair: o play/pause do navegador é trocado por um
// registro (window.__falas), e o teste confere o que tocou e o que parou.

const balao = (page: Page) => page.getByRole('dialog');
// A pose parada; a animada é a camada .tutor-presenter--talking por cima, visível só com a voz (#60).
const guia = (page: Page) => page.locator('.tutor-presenter:not(.tutor-presenter--talking)');
const falando = (page: Page) => page.locator('.tutor-presenter--talking');

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    const w = window as unknown as { __falas: string[]; __vozes: HTMLMediaElement[] };
    w.__falas = [];
    w.__vozes = [];
    // Só a voz (Audio) entra no registro; o vídeo da pose animada não fala. O 'playing'/'pause'
    // simulados são o que o tutor.js escuta para mostrar e esconder a animada.
    HTMLMediaElement.prototype.play = function () {
      if (this instanceof HTMLVideoElement) return Promise.resolve();
      w.__falas.push(new URL(this.src).pathname);
      w.__vozes.push(this);
      queueMicrotask(() => this.dispatchEvent(new Event('playing')));
      return Promise.resolve();
    };
    HTMLMediaElement.prototype.pause = function () {
      if (this instanceof HTMLVideoElement) return;
      w.__falas.push('pausa');
      this.dispatchEvent(new Event('pause'));
    };
  });
});

const fimDaVoz = (page: Page) => page.evaluate(() =>
  (window as unknown as { __vozes: HTMLMediaElement[] }).__vozes.at(-1)!.dispatchEvent(new Event('ended')));

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

// #47/#60: a pose animada aparece só enquanto a voz toca e nunca para quem pede menos movimento.
test('pose animada só enquanto a voz toca', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await abrirTour(page);
  await expect(guia(page)).toHaveAttribute('src', 'apresentador/guia-acena.svg');
  await expect(falando(page)).toHaveAttribute('src', 'apresentador/guia-acena-animado.svg');
  await expect(falando(page)).toBeVisible();
  // A parada some enquanto ela fala: senão aparece por baixo do quadro animado (duas mãos, #60).
  await expect(guia(page)).toBeHidden();

  await fimDaVoz(page);
  await expect(falando(page)).toBeHidden();
  await expect(guia(page)).toBeVisible();

  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(falando(page)).toHaveAttribute('src', 'apresentador/guia-animado.svg');
  await expect(falando(page)).toBeVisible();
  await balao(page).getByRole('button', { name: 'Silenciar' }).click();
  await expect(falando(page)).toBeHidden();
});

test('pose parada para quem pede menos movimento, mesmo com a voz', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await abrirTour(page);
  await expect.poll(() => falas(page)).toContain('/apresentador/passo-1.mp3');
  await expect(guia(page)).toBeVisible();
  await expect(falando(page)).toBeHidden();
});

test('trocar de passo não tira o apresentador da tela (sem refazer a entrada)', async ({ page }) => {
  await abrirTour(page);
  await expect(guia(page)).toBeAttached(); // falando, a parada fica escondida sob a animada
  await page.evaluate(() => {
    const w = window as unknown as { __saidas: number };
    w.__saidas = 0;
    new MutationObserver(ms => {
      for (const m of ms) m.removedNodes.forEach(n => {
        if (n instanceof HTMLImageElement && n.matches('.tutor-presenter:not(.tutor-presenter--talking)')) w.__saidas++;
      });
    }).observe(document.body, { childList: true, subtree: true });
  });
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Menu' })).toBeVisible();
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Do seu jeito' })).toBeVisible();
  expect(await page.evaluate(() => (window as unknown as { __saidas: number }).__saidas)).toBe(0);
});
