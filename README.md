# RVM.UI.Tutor

Guided tours for Blazor apps (Server and WebAssembly), in the spirit of driver.js: dims the page,
highlights an element and explains it step by step. Zero third-party runtime dependencies, one small
isolated JS module, neutral look themed through `--tutor-*` CSS variables.

Tour guiado para apps Blazor (Server e WebAssembly), no estilo do driver.js: escurece a tela, destaca o
elemento e explica passo a passo. Visual neutro, configurável por variáveis CSS `--tutor-*`.

## Recursos

- Tour por passos e entre páginas, destaque avulso, passo que espera clique ou campo, alvo que aparece depois.
- Celular (faixa inferior), teclado e foco preso, ESC, ARIA, `prefers-reduced-motion`.
- Apresentador opcional: personagem no canto, pose animada e narração por passo.
- Progresso por interface (`ITutorProgressStore`) — a biblioteca não persiste nada sozinha.
- Textos em pt-BR e en.

## Uso

Guia completo: [docs/uso.md](docs/uso.md).

```bash
dotnet run --project samples/RVM.UI.Tutor.Samples      # vitrine
dotnet test                                            # testes unitários
cd e2e && npm ci && npx playwright test                # E2E contra o samples
```

## Licença

[MIT](LICENSE).
