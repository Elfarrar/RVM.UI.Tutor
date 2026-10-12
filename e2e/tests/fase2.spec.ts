import { test, expect, type Page } from '@playwright/test';

// Fase 2: tela que muda. Cada teste tem o próprio contexto (localStorage limpo); textos em pt-BR.

const balao = (page: Page) => page.getByRole('dialog');
const eventos = (page: Page) => page.getByRole('list', { name: 'Eventos do tour' });

test('@smoke tour entre páginas navega e continua na outra página', async ({ page }) => {
  await page.goto('/multi-pagina');
  await page.getByRole('button', { name: 'Iniciar tour entre páginas' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Resumo' })).toBeVisible();

  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(page).toHaveURL(/\/multi-pagina\/obras$/);
  await expect(balao(page).getByRole('heading', { name: 'Suas obras' })).toBeVisible();
  await expect(page.locator('.tutor-hole')).toHaveCount(1);

  await balao(page).getByRole('button', { name: 'Próximo' }).click();
  await expect(page).toHaveURL(/\/multi-pagina$/);
  await expect(balao(page).getByRole('heading', { name: 'De volta' })).toBeVisible();
});

test('@smoke passo que espera clique e campo, alvo tardio e alvo que nunca aparece', async ({ page }) => {
  await page.goto('/espera-acao');
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Comece por aqui' })).toBeVisible();
  await expect(balao(page).getByRole('button', { name: 'Próximo' })).toHaveCount(0);

  // O clique no alvo atravessa o recorte e chega ao app (que abre o formulário 1 s depois).
  await page.getByRole('button', { name: 'Nova obra' }).click();
  await expect(balao(page).getByRole('heading', { name: 'Dê um nome' })).toBeVisible();
  await expect(page.getByLabel('Nome da obra')).toBeFocused();
  await expect(balao(page).getByRole('button', { name: 'Próximo' })).toHaveCount(0);

  await page.getByLabel('Nome da obra').fill('Casa da Ana');
  await page.keyboard.press('Tab');
  await expect(balao(page).getByRole('heading', { name: 'Salvar' })).toBeVisible();

  await page.getByRole('button', { name: 'Salvar', exact: true }).click();
  await expect(page.getByRole('status')).toHaveText('Obra salva: Casa da Ana');
  await expect(balao(page)).toBeHidden();
  await expect(eventos(page)).toContainText('TargetMissing 4');
  await expect(eventos(page)).toContainText('Completed 4');
});

test('passo que espera clique não avança pela seta nem por clique fora do recorte', async ({ page }) => {
  await page.goto('/espera-acao');
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await expect(balao(page).getByText('1 de 4')).toBeVisible();

  await page.keyboard.press('ArrowRight');
  await page.mouse.click(1000, 650);
  await expect(balao(page).getByText('1 de 4')).toBeVisible();
  await expect(eventos(page)).not.toContainText('StepShown 2');
});

test('só teclado: Enter no alvo, digitar e Enter no Salvar completam o tour', async ({ page }) => {
  await page.goto('/espera-acao');
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await expect(balao(page).getByText('1 de 4')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Nova obra' })).toBeFocused();
  await page.keyboard.press('Enter');

  await expect(page.getByLabel('Nome da obra')).toBeFocused();
  await page.keyboard.type('Galpão');
  await page.keyboard.press('Enter');

  await expect(balao(page).getByText('3 de 4')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Salvar', exact: true })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('status')).toHaveText('Obra salva: Galpão');
  await expect(eventos(page)).toContainText('Completed 4');
});

test('Tab no passo de ação passa pelo alvo e pelo balão', async ({ page }) => {
  await page.goto('/espera-acao');
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await expect(page.getByRole('button', { name: 'Nova obra' })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(balao(page).getByRole('button', { name: 'Pular' })).toBeFocused();
  await page.keyboard.press('Tab');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: 'Nova obra' })).toBeFocused();
});

test('ESC enquanto espera o alvo tardio encerra sem deixar o fundo escuro', async ({ page }) => {
  await page.goto('/espera-acao');
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await page.getByRole('button', { name: 'Nova obra' }).click();
  await page.keyboard.press('Escape');
  await expect(page.getByLabel('Nome da obra')).toBeVisible();
  await expect(page.locator('.tutor-root')).toHaveCount(0);
  await expect(eventos(page)).toContainText('Skipped 2');
});

test.describe('celular', () => {
  test.use({ viewport: { width: 375, height: 667 } });

  test('@smoke abaixo de 480 px o balão vira faixa inferior e o alvo fica acima dela', async ({ page }) => {
    await page.goto('/tour-simples');
    await page.getByRole('button', { name: 'Iniciar tour' }).click();
    await expect(balao(page)).toHaveClass(/tutor-popover--sheet/);
    for (const passo of ['2 de 4', '3 de 4', '4 de 4']) {
      await balao(page).getByRole('button', { name: 'Próximo' }).click();
      await expect(balao(page).getByText(passo)).toBeVisible();
    }
    const faixa = (await balao(page).boundingBox())!;
    expect(faixa.x).toBe(0);
    expect(faixa.width).toBe(375);
    expect(faixa.y + faixa.height).toBe(667);
    const alvo = (await page.locator('[data-tour="rodape"]').boundingBox())!;
    expect(alvo.y + alvo.height).toBeLessThanOrEqual(faixa.y);
  });
});

test.describe('celular pequeno', () => {
  test.use({ viewport: { width: 320, height: 568 } });

  test('em 320 px a faixa não cobre o alvo e a página não rola na horizontal', async ({ page }) => {
    await page.goto('/espera-acao');
    await page.getByRole('button', { name: 'Iniciar tour' }).click();
    await expect(balao(page)).toHaveClass(/tutor-popover--sheet/);
    await expect(page.getByRole('button', { name: 'Nova obra' })).toBeFocused();
    const faixa = (await balao(page).boundingBox())!;
    const alvo = (await page.getByRole('button', { name: 'Nova obra' }).boundingBox())!;
    expect(alvo.y + alvo.height).toBeLessThanOrEqual(faixa.y);
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(320);
    // O espaço reservado no pé da página (página curta) é devolvido quando o tour fecha.
    await page.keyboard.press('Escape');
    await expect(balao(page)).toHaveCount(0);
    expect(await page.evaluate(() => document.body.style.paddingBottom)).toBe('');
  });
});

test('@smoke em WebAssembly o tour roda igual, com textos em pt-BR', async ({ page }) => {
  await page.goto('/wasm/tour-simples');
  // O botão só habilita depois que o runtime WASM sobe.
  await page.getByRole('button', { name: 'Iniciar tour' }).click();
  await expect(page.getByText('Rodando em: WebAssembly')).toBeVisible();
  await expect(balao(page).getByText('1 de 4')).toBeVisible();

  await page.keyboard.press('ArrowRight');
  await expect(balao(page).getByRole('heading', { name: 'Novo lançamento' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(balao(page)).toBeHidden();
  await expect(eventos(page)).toContainText('Skipped 2');
});

test('tema: escuro forçado e variáveis --tutor-* próprias', async ({ page }) => {
  await page.goto('/tema');
  await page.getByLabel('Escuro').check();
  await page.getByRole('button', { name: 'Ver o tour neste tema' }).click();
  await expect(balao(page)).toHaveCSS('background-color', 'rgb(30, 41, 59)');
  await page.keyboard.press('Escape');

  await page.getByLabel('Personalizado (variáveis --tutor-*)').check();
  await page.getByRole('button', { name: 'Ver o tour neste tema' }).click();
  await expect(balao(page).getByRole('button', { name: 'Próximo' })).toHaveCSS('background-color', 'rgb(21, 128, 61)');
  await expect(balao(page)).toHaveCSS('background-color', 'rgb(255, 255, 255)');
});
