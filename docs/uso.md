# RVM.UI.Tutor — uso

Tour guiado para Blazor: escurece a tela, recorta o elemento em foco e mostra um balão. API em
inglês (ADR-007); textos da biblioteca em pt-BR e en.

> Estado: **Fase 2** — tour por passos, destaque avulso, progresso, alvo que aparece depois, passo
> que espera clique/campo, tour entre páginas e faixa inferior no celular; Server e WebAssembly.
> Apresentador com narração por voz (§ 5).

## 1. Registrar

```csharp
// Program.cs
builder.Services.AddRvmTutor(o =>
{
    o.Language = "pt-BR";       // null = cultura de UI corrente
    o.AllowSkip = true;         // botão "Pular" e ESC pula
    o.CloseOnOverlayClick = false;
});

// Opcional: progresso no banco, por usuário. Sem isto, fica no localStorage do navegador.
builder.Services.AddScoped<ITutorProgressStore, MeuProgressoNoBanco>();
```

```razor
@* MainLayout.razor — uma vez, em modo interativo *@
<TutorHost />
```

O `<TutorHost/>` precisa estar numa parte **interativa** do app (Server ou WebAssembly). O CSS
vem pelo bundle `{App}.styles.css`, que o template do Blazor já inclui; o JS é carregado sozinho
(`_content/RVM.UI.Tutor/tutor.js`).

## 2. Escrever o roteiro

```csharp
public static class TourAlvos
{
    public const string Menu = "menu";
    public const string BotaoNovo = "lancamentos-novo";
}

public static class TourLancamentos
{
    public static readonly TourDefinition Primeiro = new("lancamentos-primeiro", Version: 1,
    [
        new(Target: TourAlvos.Menu, Title: "Lançamentos", Text: "Aqui ficam todos os custos da obra."),
        new(Target: TourAlvos.BotaoNovo, Title: "Novo lançamento", Text: "Clique para registrar um custo.",
            Placement: Placement.Right),
        new(Target: null, Title: "Pronto!", Text: "Sem alvo, o balão aparece no meio da tela."),
    ]);
}
```

```razor
<button data-tour="@TourAlvos.BotaoNovo">Novo</button>
```

| `TourStep` | |
|---|---|
| `Target` | valor do `data-tour`; `null` = balão centralizado |
| `Title`, `Text` | conteúdo do balão |
| `Placement` | `Auto` (padrão: baixo → cima → direita → esquerda), `Top`, `Bottom`, `Left`, `Right` |
| `WhenTargetMissing` | `Center` (padrão), `Skip` ou `End` quando o alvo não está na tela (conta como ausente: 0×0, `display:none`, `visibility:hidden`, ou fora da tela mesmo depois de o tour rolar até ele, como gaveta fechada por `transform`; abaixo da dobra, na página ou num painel com rolagem, conta como presente) |
| `Content` | `RenderFragment` extra, abaixo do texto |
| `Route` | página do passo (ex. `/lancamentos`); se o usuário estiver em outra, o tour navega e continua lá |
| `Advance` | `Button` (padrão), `ClickTarget` (avança no clique do alvo; o clique segue para o app) ou `InputFilled` (avança quando o campo do alvo é preenchido, ao sair dele ou Enter) — nos dois últimos o "Próximo" some |
| `WaitForTargetMs` | quanto esperar o alvo aparecer (dado que carrega, componente que abre); padrão 3000. Espera de mais de 400 ms mostra "Procurando o próximo passo…" com Pular e Fechar. Passo que costuma faltar (`Skip` para quem não tem dado): use uma espera curta |
| `Pose` | imagem do apresentador neste passo; `null` = a padrão dele (§ 5) |
| `Audio` | URL do mp3 narrado no passo, no idioma do texto (§ 5) |
| `PoseAnimated` | versão animada de `Pose` (WebM com alfa, ou WebP/GIF/SVG animado); aparece só enquanto a voz toca, nunca para quem pede menos movimento (§ 5) |

`TourDefinition.ShowAgainOnNewVersion` (padrão `true`): subir a `Version` faz o tour reaparecer
para quem viu a anterior.

## 3. Abrir e controlar

```csharp
@inject ITutor Tutor

await Tutor.StartIfNeverSeenAsync(TourLancamentos.Primeiro, usuarioId); // 1º login
await Tutor.StartAsync(TourLancamentos.Primeiro, usuarioId);             // menu "Tutoriais": retoma
await Tutor.StartAsync(TourLancamentos.Primeiro, usuarioId, force: true); // do começo
await Tutor.HighlightAsync(TourAlvos.BotaoNovo, "Seu lançamento foi salvo aqui.", TimeSpan.FromSeconds(5));
```

| `ITutor` | |
|---|---|
| `StartAsync(tour, user?, force)` | abre; com `user`, retoma do passo salvo se ficou em andamento nesta versão |
| `StartIfNeverSeenAsync(tour, user)` | abre do começo só se o usuário nunca viu esta versão (fechado no meio conta como visto: retoma-se pelo `StartAsync`); se o progresso não puder ser lido, não abre; devolve `true` se abriu |
| `NextAsync` / `BackAsync` / `SkipAsync` / `CloseAsync` | navegação (`Close` guarda o passo para retomar) |
| `HighlightAsync(target, text, duration?)` | destaque avulso, sem tour; ignorado com tour aberto |
| `IsActive`, `CurrentStep` | estado |
| `EventRaised` | `Started`, `StepShown`, `Completed`, `Skipped`, `Closed`, `TargetMissing` |

O recorte deixa o clique passar para o alvo; fora dele, o fundo bloqueia. No passo que espera
ação o foco vai para o alvo e o Tab passa por ele: quem usa só teclado aciona com Enter/Espaço (ou
sai do campo). Se a ação não puder acontecer (botão desabilitado, campo já preenchido), o
"Próximo" volta, para ninguém ficar preso.

Abaixo de **480 px** o balão vira uma faixa na parte de baixo e o alvo é rolado para cima dela.
No iPhone, para a faixa respeitar a barra inferior, o app precisa de `viewport-fit=cover` no
`<meta name="viewport">`.

Cuidados com `Route`:
- a rota é relativa à base do app (`<base href>`); passo com rota navega pelo roteador interativo —
  página só estática (ou de outro modo de render) recarrega a página e o tour se perde;
- passo **depois** de um `ClickTarget` cujo clique já navega não precisa de `Route` (o alvo é
  esperado na página nova por `WaitForTargetMs`).

Teclado: **→** próximo (não em passo que espera ação), **←** anterior (não quando se está digitando num campo), **Enter** no
botão focado, **ESC** pula (ou fecha, se `AllowSkip = false`), **Tab** fica preso no balão. Ao
terminar, o foco volta a quem o tinha. Clicar no fundo não tira o foco do balão.

Falha ao ler/gravar progresso (localStorage bloqueado, banco fora) não trava o tour: vira log de
aviso e o tour segue como se não houvesse progresso.

## 4. Progresso

```csharp
public interface ITutorProgressStore
{
    Task<TutorProgress?> GetAsync(string tourId, string user);
    Task SaveAsync(TutorProgress progress);
}
// TutorProgress(TourId, User, Version, Status: InProgress|Completed|Skipped, LastStep, UpdatedAt)
```

Salvo a cada passo mostrado (`InProgress`), ao concluir e ao pular. Sem `user`, nada é salvo.
`User` é texto livre: app com vários clientes em que a mesma pessoa vê o tour em cada um passa
`"cliente:usuario"` (decisão de 12/10/2026, sem campo de tenant).

## 5. Apresentador e narração

Um personagem no canto inferior da tela apresenta o tour, e os passos com `Audio` são narrados. A
biblioteca não traz personagem: ele vem do app ou da pele do RVM.UI (a Aly mora lá, ADR-002).

```csharp
builder.Services.AddRvmTutor(o =>
{
    o.Presenter = new TourPresenter("Aly", "img/aly.png", "img/aly.webp"); // todos os tours; 3º = animada (opcional)
    o.ShowPresenter = true;                                // mostra o apresentador (padrão)
});

var tour = new TourDefinition("lancamentos-primeiro", 1,
[
    new(Target: null, Title: "Oi!", Text: "Eu sou a Aly...", Pose: "img/aly-acena.png", Audio: "audio/pt/oi.mp3",
        PoseAnimated: "img/aly-acena.webp"),
    new(Target: TourAlvos.Menu, Title: "Menu", Text: "...", Audio: "audio/pt/menu.mp3"),
])
{
    Presenter = outraPersonagem, // opcional: troca a do registro só neste tour
    ShowPresenter = false,       // opcional: sem apresentador (nem voz) neste tour
};
```

- **Imagem:** retrato 2:3 com fundo transparente (PNG/WebP/SVG). Fica no canto direito, ou no
  esquerdo se o direito cobre o alvo ou o balão. Altura até `PresenterMaxHeight` (padrão 300 px) e
  `PresenterViewportRatio` da janela (padrão 0,45); sem canto livre nesse tamanho, encolhe até caber
  (mínimo 140 px) e só então some. Some também abaixo de 480 px. Decorativa (`alt=""`): quem fala é o balão.
  ```csharp
  builder.Services.AddRvmTutor(o => { o.PresenterMaxHeight = 750; o.PresenterViewportRatio = 0.8; }); // grande
  ```
- **Animação:** `TourPresenter.ImageAnimated` e `TourStep.PoseAnimated` são opcionais: vídeo WebM com
  alfa (ou MP4) vira `<video>`; WebP/GIF/SVG animado, `<img>`. A animada fica por cima da parada e
  **só aparece enquanto a voz do passo toca**: a voz acabou, o tour fechou ou o passo não tem áudio,
  fica a parada. Nunca com `prefers-reduced-motion: reduce`, nem no Safari para vídeo (ele não mostra
  o alfa do WebM). Passo com `Pose` própria e sem `PoseAnimated` fica parado: não herda a animação da
  imagem padrão.
- **Quem decide:** quem configura o tour (`ShowPresenter` no registro ou no tour, `Audio` em cada
  passo). Quem assiste não tem botão de ocultar nem de silenciar (#66).
- **Voz:** a voz é do apresentador: com `ShowPresenter = false`, ele não fala. Tour **sem** apresentador
  narra os passos com `Audio` do mesmo jeito. O áudio para ao trocar de passo e ao fechar (X ou Esc —
  o jeito de quem assiste parar a voz, WCAG 1.4.2), e **só começa depois de um clique ou tecla na
  página**: tour aberto sozinho no 1º login espera o primeiro gesto do usuário.
- **Idioma:** o texto do passo é do app, e o áudio também: monte o tour com o `Audio` do idioma
  do usuário, como já faz com `Title` e `Text`.

### Gerar os áudios

Os mp3 são gerados antes, offline, com o **Kokoro TTS** (`kokoro-onnx`, modelo Apache-2.0). Nada
disso vai no pacote. Vozes femininas usadas na Aly: `pf_dora` (pt-BR) e `af_bella` (en).

```bash
# instalação (uma vez)
python3 -m venv ~/.local/share/kokoro/venv
~/.local/share/kokoro/venv/bin/pip install kokoro-onnx soundfile
mkdir -p ~/.local/share/kokoro/model && cd ~/.local/share/kokoro/model
for f in kokoro-v1.0.onnx voices-v1.0.bin; do
  curl -fLO https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/$f
done

# gerar: cada fala do roteiro vira <saida>/<nome>.mp3 (só as que mudaram)
~/.local/share/kokoro/venv/bin/python -I tools/gerar-audio.py roteiro.json wwwroot/audio/pt
```

```json
{ "voz": "pf_dora", "idioma": "pt-br", "velocidade": 1.0,
  "falas": { "oi": "Oi! Eu sou a Aly.", "menu": "Pelo menu você chega a todas as páginas." } }
```

O texto da fala é o mesmo do `Text` do passo: mudou um, mude o outro e gere de novo.

## 6. Tema

Tudo por variáveis CSS, definidas em qualquer ancestral (ex.: `:root`):

| Variável | Uso |
|---|---|
| `--tutor-overlay` | fundo escurecido |
| `--tutor-bg`, `--tutor-fg`, `--tutor-muted`, `--tutor-border` | balão |
| `--tutor-accent`, `--tutor-accent-fg` | botão primário e foco |
| `--tutor-radius`, `--tutor-shadow`, `--tutor-font` | forma e tipografia |
| `--tutor-z` | camada (padrão 10000) |
| `--tutor-transition` | animação do recorte e da entrada do apresentador (zeradas com `prefers-reduced-motion`) |
| `--tutor-presenter-shadow` | sombra do apresentador (`filter`) |

Claro/escuro segue `prefers-color-scheme`; para forçar, `data-tutor-theme="dark"` ou `"light"` num
ancestral. A pele do ecossistema (tokens Protask) mora no **RVM.UI**, nunca aqui (ADR-002).

## 7. WebAssembly

Funciona igual em WASM. Um cuidado: os textos pt da biblioteca vêm de um assembly satélite, que o
WASM só baixa para a **cultura corrente na subida**. Se o app usa `Language = "pt-BR"`, fixe a
cultura no `Program.cs` do projeto Client **antes** do `RunAsync`, e carregue os dados de
globalização (pt-BR não está no pacote padrão quando o navegador é en-US):

```csharp
var ptBR = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = ptBR;
CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = ptBR;
```

```xml
<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>
```

## 8. Samples e E2E

```
dotnet run --project samples/RVM.UI.Tutor.Samples      # /tour-simples, /destaque, /multi-pagina,
                                                        # /espera-acao, /tema, /apresentador,
                                                        # /wasm/tour-simples
cd e2e && npm ci && npx playwright test                 # sobe o samples sozinho na porta 5280
```

⚠️ Windows com **Smart App Control** pode bloquear as DLLs recém-compiladas do samples
(`0x800711C7`). Contorno: rodar o samples num container Linux e apontar o E2E com
`BASE_URL=http://127.0.0.1:5280`.
