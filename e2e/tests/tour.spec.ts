import { test, expect, type Page } from '@playwright/test';

// Cada teste tem o próprio contexto de navegador (localStorage limpo): roda em paralelo sem
// depender de outro. O samples fixa os textos da biblioteca em pt-BR.

const balao = (page: Page) => page.getByRole('dialog');

async function abrirTour(page: Page) {
  await page.goto('/tour-simples');
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await expect(balao(page)).toBeVisible();
}

test('@smoke tour simples vai do primeiro ao último passo e conclui', async ({ page }) => {
  await abrirTour(page);
  await expect(balao(page).getByRole('heading', { name: 'Menu' })).toBeVisible();
  await expect(balao(page).getByText('1 de 4')).toBeVisible();

  for (const titulo of ['Novo lançamento', 'Lançamentos', 'Ajuda']) {
    await balao(page).getByRole('button', { name: 'Próximo' }).click();
    await expect(balao(page).getByRole('heading', { name: titulo })).toBeVisible();
  }
  await balao(page).getByRole('button', { name: 'Concluir' }).click();

  await expect(balao(page)).toBeHidden();
  await expect(page.getByRole('list', { name: 'Eventos do tour' })).toContainText('Completed 4');
});

test('recorte acompanha o alvo e o balão não cobre o alvo', async ({ page }) => {
  await abrirTour(page);
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Novo lançamento' })).toBeVisible();

  const alvo = await page.locator('[data-tour="novo-lancamento"]').boundingBox();
  const furo = await page.locator('.tutor-hole').boundingBox();
  const caixa = await balao(page).boundingBox();
  expect(alvo && furo && caixa).toBeTruthy();
  // O furo contém o alvo (com folga) e o balão fica à direita (Placement.Right), sem sobrepor.
  expect(furo!.x).toBeLessThanOrEqual(alvo!.x);
  expect(furo!.x + furo!.width).toBeGreaterThanOrEqual(alvo!.x + alvo!.width);
  expect(caixa!.x).toBeGreaterThanOrEqual(alvo!.x + alvo!.width);
});

test('rola até o alvo no fim da página', async ({ page }) => {
  await abrirTour(page);
  // Espera cada passo: clicar de novo antes do render pegaria o botão do passo anterior.
  for (const passo of ['2 de 4', '3 de 4', '4 de 4']) {
    await balao(page).getByRole('button', { name: 'Próximo' }).click();
    await expect(balao(page).getByText(passo)).toBeVisible();
  }
  await expect(balao(page).getByRole('heading', { name: 'Ajuda' })).toBeVisible();
  await expect(page.locator('[data-tour="rodape"]')).toBeInViewport();
});

test('teclado: setas navegam, ESC pula e o foco volta ao botão de origem', async ({ page }) => {
  await abrirTour(page);
  await expect(balao(page).getByRole('button', { name: 'Próximo' })).toBeFocused();

  await page.keyboard.press('ArrowRight');
  await expect(balao(page).getByText('2 de 4')).toBeVisible();
  await page.keyboard.press('ArrowLeft');
  await expect(balao(page).getByText('1 de 4')).toBeVisible();

  await page.keyboard.press('Escape');
  await expect(balao(page)).toBeHidden();
  await expect(page.getByRole('list', { name: 'Eventos do tour' })).toContainText('Skipped 1');
  await expect(page.getByRole('button', { name: 'Iniciar tour' })).toBeFocused();
});

test('clicar no fundo não tira o foco: o ESC continua pulando', async ({ page }) => {
  await abrirTour(page);
  await page.mouse.click(900, 600);
  await page.keyboard.press('Escape');
  await expect(balao(page)).toBeHidden();
});

test('alvo oculto conta como ausente e o balão vai para o centro', async ({ page }) => {
  await abrirTour(page);
  await page.locator('[data-tour="lista-lancamentos"]').evaluate(el => { (el as HTMLElement).style.display = 'none'; });
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByText('2 de 4')).toBeVisible();
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Lançamentos' })).toBeVisible();
  await expect(balao(page)).toHaveClass(/tutor-popover--centered/);
  await expect(page.getByRole('list', { name: 'Eventos do tour' })).toContainText('TargetMissing 3');
});

// #41: gaveta fechada no celular (visibility:hidden + transform) mantém o retângulo.
for (const [caso, estilo] of [
  ['visibility:hidden', 'visibility: hidden'],
  ['gaveta fora da tela por transform', 'position: fixed; left: 0; top: 0; transform: translateX(-100%)'],
] as const) {
  test(`alvo com ${caso} conta como ausente`, async ({ page }) => {
    await abrirTour(page);
    // Página rolada: a gaveta fixa continua fora da tela (ela não acompanha a rolagem).
    await page.evaluate(() => { document.body.style.paddingBottom = '3000px'; scrollTo(0, 500); });
    await page.locator('[data-tour="lista-lancamentos"]').evaluate((el, css) => { (el as HTMLElement).style.cssText = css; }, estilo);
    // Pelo teclado: com a página rolada, o balão dos passos do topo fica fora da tela junto do alvo.
    await page.keyboard.press('ArrowRight');
    await expect(balao(page).getByText('2 de 4')).toBeAttached();
    await page.keyboard.press('ArrowRight');
    await expect(balao(page).getByRole('heading', { name: 'Lançamentos' })).toBeVisible();
    await expect(balao(page)).toHaveClass(/tutor-popover--centered/);
    await expect(page.getByRole('list', { name: 'Eventos do tour' })).toContainText('TargetMissing 3');
  });
}

test('alvo abaixo da dobra de um painel com rolagem própria conta como presente', async ({ page }) => {
  await abrirTour(page);
  // Shell de app: o painel rola, a janela não. O tour rola o painel até o alvo.
  await page.locator('[data-tour="lista-lancamentos"]').evaluate(el => {
    const painel = document.createElement('div');
    painel.style.cssText = 'height: 120px; overflow: auto';
    const espaco = document.createElement('div');
    espaco.style.height = '2000px';
    el.replaceWith(painel);
    painel.append(espaco, el);
  });
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByText('2 de 4')).toBeVisible();
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Lançamentos' })).toBeVisible();
  await expect(balao(page)).not.toHaveClass(/tutor-popover--centered/);
  await expect(page.locator('[data-tour="lista-lancamentos"]')).toBeInViewport();
  await expect(page.getByRole('list', { name: 'Eventos do tour' })).not.toContainText('TargetMissing');
});

for (const rota of ['/tour-simples', '/wasm/tour-simples']) test(`passo Skip no meio do tour vai para o passo seguinte (${rota})`, async ({ page }) => {
  await page.goto(rota);
  await page.getByRole('button', { name: 'Tour com passo pulado' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Boas-vindas' })).toBeVisible();
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  // Esperando o alvo: aviso com Pular/Fechar em vez de só o fundo escuro.
  const aviso = page.getByRole('dialog', { name: 'Procurando o próximo passo…' });
  await expect(aviso).toBeVisible();
  await expect(aviso.getByRole('button', { name: 'Pular' })).toBeVisible();
  await expect(aviso).toBeFocused();
  await expect(balao(page).getByRole('heading', { name: 'Lançamentos' })).toBeVisible({ timeout: 8000 });
  await expect(aviso).toBeHidden();
  await expect(page.getByRole('list', { name: 'Eventos do tour' })).toContainText('TargetMissing 3');
  await expect(balao(page).getByText('4 de 4')).toBeVisible();
  await balao(page).getByRole('button', { name: 'Concluir' }).click();
  await expect(balao(page)).toBeHidden();
});

test('Tab fica preso no balão', async ({ page }) => {
  await abrirTour(page);
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByText('2 de 4')).toBeVisible();
  // Fechar, Pular, Voltar, Próximo: seis Tabs dão a volta e continuam dentro.
  for (let i = 0; i < 6; i++) {
    await page.keyboard.press('Tab');
    await expect(balao(page).locator(':focus')).toHaveCount(1);
  }
});

test('fechar guarda o passo e "continuar" retoma de onde parou', async ({ page }) => {
  await abrirTour(page);
  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(balao(page).getByText('2 de 4')).toBeVisible();

  await balao(page).getByRole('button', { name: 'Fechar' }).click();
  await expect(balao(page)).toBeHidden();

  await page.getByRole('button', { name: 'Continuar de onde parei' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Novo lançamento' })).toBeVisible();
});

test('@smoke destaque avulso aparece no item salvo e fecha', async ({ page }) => {
  await page.goto('/destaque');
  await page.getByRole('button', { name: 'Salvar lançamento' }).click();
  await expect(balao(page)).toContainText('Seu lançamento foi salvo aqui.');
  await expect(page.locator('.tutor-hole')).toHaveCount(1);

  await balao(page).getByRole('button', { name: 'Fechar' }).click();
  await expect(balao(page)).toBeHidden();
});
