using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using RVM.UI.Tutor.Internal;

namespace RVM.UI.Tutor.Tests;

public class TutorHostTests : BunitContext
{
    private readonly FakeProgressStore _store = new();
    private readonly BunitJSModuleInterop _js;
    private LayoutChange _layout = new(true, 100, 100, 120, 40, 1280, 720);

    public TutorHostTests()
    {
        Services.AddSingleton<ITutorProgressStore>(_store);
        Services.AddRvmTutor(o => o.Language = "pt-BR");
        var module = JSInterop.SetupModule("./_content/RVM.UI.Tutor/tutor.js");
        _js = module.SetupModule("createHost", _ => true);
        _js.Setup<LayoutChange>("locate", _ => true).SetResult(_layout);
        _js.Setup<SizeInfo>("measure", _ => true).SetResult(new SizeInfo(300, 150));
        _js.SetupVoid("focus", _ => true).SetVoidResult();
        _js.SetupVoid("deactivate", _ => true).SetVoidResult();
        _js.SetupVoid("arm", _ => true).SetVoidResult();
        _js.SetupVoid("speak", _ => true).SetVoidResult();
    }

    private void ArmAction() => _js.Setup<LayoutChange>("locate", _ => true).SetResult(_layout with { ActionArmed = true });

    private TutorService Tutor => Services.GetRequiredService<TutorService>();

    private IRenderedComponent<TutorHost> RenderWithTour(TourDefinition tour, TutorOptions? options = null)
    {
        var cut = Render<TutorHost>();
        cut.InvokeAsync(() => Tutor.StartAsync(tour)).Wait();
        cut.WaitForAssertion(() => Assert.Contains("top:", cut.Find("[role=dialog]").GetAttribute("style")));
        return cut;
    }

    [Fact]
    public void Sem_tour_nao_desenha_nada()
    {
        var cut = Render<TutorHost>();
        Assert.Empty(cut.FindAll(".tutor-root"));
    }

    [Fact]
    public void Passo_mostra_titulo_texto_progresso_e_botoes_em_pt_BR()
    {
        var cut = RenderWithTour(TestData.Tour(2));

        var dialog = cut.Find("[role=dialog]");
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        Assert.Equal(cut.Find(".tutor-title").Id, dialog.GetAttribute("aria-labelledby"));
        Assert.Equal(cut.Find(".tutor-text").Id, dialog.GetAttribute("aria-describedby"));
        Assert.Equal("Passo 1", cut.Find(".tutor-title").TextContent);
        Assert.Equal("1 de 2", cut.Find(".tutor-progress").TextContent);
        Assert.Equal(["Pular", "Próximo"], cut.FindAll(".tutor-actions button").Select(b => b.TextContent.Trim()));
        Assert.Equal("Fechar", cut.Find(".tutor-close").GetAttribute("aria-label"));
        Assert.Contains("tutor-popover--bottom", dialog.ClassName);
        Assert.Single(_js.Invocations["focus"]);
    }

    [Fact]
    public void Recorte_e_o_alvo_mais_a_folga()
    {
        var cut = RenderWithTour(TestData.Tour());
        var hole = cut.Find(".tutor-hole");
        Assert.Equal("92", hole.GetAttribute("x"));
        Assert.Equal("92", hole.GetAttribute("y"));
        Assert.Equal("136", hole.GetAttribute("width"));
        Assert.Equal("56", hole.GetAttribute("height"));
        Assert.Equal("8", hole.GetAttribute("rx"));
        Assert.Equal("url(#" + cut.Find("mask").Id + ")", cut.Find(".tutor-overlay-fill").GetAttribute("mask"));
    }

    [Fact]
    public void Proximo_ate_concluir_fecha_e_desativa_o_js()
    {
        var cut = RenderWithTour(TestData.Tour(2));
        cut.Find(".tutor-btn--primary").Click();
        cut.WaitForAssertion(() => Assert.Equal("2 de 2", cut.Find(".tutor-progress").TextContent));
        Assert.Equal(["Voltar", "Concluir"], cut.FindAll(".tutor-actions button").Select(b => b.TextContent.Trim()));

        cut.Find(".tutor-btn--primary").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".tutor-root")));
        cut.WaitForAssertion(() => Assert.Single(_js.Invocations["deactivate"]));
        Assert.Equal(TutorState.Completed, Tutor.Engine.State);
    }

    [Fact]
    public void Voltar_volta_um_passo()
    {
        var cut = RenderWithTour(TestData.Tour());
        cut.Find(".tutor-btn--primary").Click();
        cut.WaitForAssertion(() => Assert.Equal("2 de 3", cut.Find(".tutor-progress").TextContent));
        cut.FindAll(".tutor-actions button").Single(b => b.TextContent == "Voltar").Click();
        cut.WaitForAssertion(() => Assert.Equal("1 de 3", cut.Find(".tutor-progress").TextContent));
    }

    [Theory]
    [InlineData("ArrowRight", "2 de 3")]
    [InlineData("ArrowLeft", "1 de 3")]
    public void Setas_navegam(string key, string expected)
    {
        var cut = RenderWithTour(TestData.Tour());
        cut.InvokeAsync(() => cut.Instance.OnKey(key));
        cut.WaitForAssertion(() => Assert.Equal(expected, cut.Find(".tutor-progress").TextContent));
    }

    [Fact]
    public void Esc_pula()
    {
        var cut = RenderWithTour(TestData.Tour());
        cut.InvokeAsync(() => cut.Instance.OnKey("Escape"));
        cut.WaitForAssertion(() => Assert.Equal(TutorState.Skipped, Tutor.Engine.State));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".tutor-root")));
        cut.WaitForAssertion(() => Assert.Single(_js.Invocations["deactivate"]));
    }

    [Fact]
    public async Task Fim_por_politica_End_antes_do_foco_ainda_desliga_os_observers_do_js()
    {
        _js.Setup<LayoutChange>("locate", _ => true).SetResult(_layout with { Found = false });
        var tour = new TourDefinition("t", 1, [new TourStep("some", "A", "a", WhenTargetMissing: MissingTargetPolicy.End)]);
        var cut = Render<TutorHost>();
        await cut.InvokeAsync(() => Tutor.StartAsync(tour));

        cut.WaitForAssertion(() => Assert.Single(_js.Invocations["deactivate"]));
        Assert.Empty(cut.FindAll(".tutor-root"));
        Assert.Empty(_js.Invocations["focus"]);
    }

    [Fact]
    public void Janela_que_muda_de_tamanho_mede_o_balao_de_novo()
    {
        var cut = RenderWithTour(TestData.Tour());
        Assert.Single(_js.Invocations["measure"]);

        cut.InvokeAsync(() => cut.Instance.OnLayoutChanged(_layout with { ViewportWidth = 400 }));
        cut.WaitForAssertion(() => Assert.Equal(2, _js.Invocations["measure"].Count));

        cut.InvokeAsync(() => cut.Instance.OnLayoutChanged(_layout with { ViewportWidth = 400, X = 50 }));
        cut.WaitForAssertion(() => Assert.Equal("42", cut.Find(".tutor-hole").GetAttribute("x")));
        Assert.Equal(2, _js.Invocations["measure"].Count);
    }

    [Fact]
    public void Esc_sem_permissao_de_pular_so_fecha_e_nao_mostra_Pular()
    {
        Services.GetRequiredService<TutorOptions>().AllowSkip = false;
        var cut = RenderWithTour(TestData.Tour());
        Assert.DoesNotContain(cut.FindAll(".tutor-actions button"), b => b.TextContent == "Pular");

        cut.InvokeAsync(() => cut.Instance.OnKey("Escape"));
        cut.WaitForAssertion(() => Assert.Equal(TutorState.Idle, Tutor.Engine.State));
    }

    [Fact]
    public void Botao_fechar_fecha_e_outras_teclas_nao_fazem_nada()
    {
        var cut = RenderWithTour(TestData.Tour());
        cut.InvokeAsync(() => cut.Instance.OnKey("a"));
        Assert.Equal(TutorState.Showing, Tutor.Engine.State);

        cut.Find(".tutor-close").Click();
        cut.WaitForAssertion(() => Assert.Equal(TutorState.Idle, Tutor.Engine.State));
    }

    [Fact]
    public void Clique_no_fundo_so_fecha_se_a_opcao_mandar()
    {
        var cut = RenderWithTour(TestData.Tour());
        cut.Find(".tutor-blocker").Click();
        Assert.Equal(TutorState.Showing, Tutor.Engine.State);

        Services.GetRequiredService<TutorOptions>().CloseOnOverlayClick = true;
        cut.Find(".tutor-blocker").Click();
        cut.WaitForAssertion(() => Assert.Equal(TutorState.Idle, Tutor.Engine.State));
    }

    [Fact]
    public void Alvo_ausente_mostra_centralizado_sem_recorte_nem_seta()
    {
        _layout = _layout with { Found = false };
        _js.Setup<LayoutChange>("locate", _ => true).SetResult(_layout);
        var cut = RenderWithTour(TestData.Tour());

        Assert.Contains("tutor-popover--centered", cut.Find("[role=dialog]").ClassName);
        Assert.Empty(cut.FindAll(".tutor-hole"));
        Assert.Empty(cut.FindAll(".tutor-arrow"));
    }

    [Fact]
    public void Passo_Skip_no_meio_do_tour_vai_para_o_proximo_passo_com_alvo()
    {
        // #37: no Portal, o "Próximo" do passo 1 levava a um passo Skip e a tela ficava só com o véu.
        _js.Setup<LayoutChange>("locate", i => (string?)i.Arguments[0] == "alvo-2").SetResult(_layout with { Found = false });
        var tour = new TourDefinition("skip", 1,
        [
            new TourStep("alvo-1", "Passo 1", "Texto 1"),
            new TourStep("alvo-2", "Passo 2", "Texto 2", WhenTargetMissing: MissingTargetPolicy.Skip),
            new TourStep("alvo-3", "Passo 3", "Texto 3"),
        ]);
        var cut = RenderWithTour(tour);

        cut.Find(".tutor-btn--primary").Click();

        cut.WaitForAssertion(() => Assert.Equal("Passo 3", cut.Find(".tutor-title").TextContent));
        cut.WaitForAssertion(() => Assert.Contains("top:", cut.Find("[role=dialog]").GetAttribute("style")));
        Assert.Equal(["alvo-1", "alvo-2", "alvo-3"], _js.Invocations["locate"].Select(i => (string?)i.Arguments[0]));
        Assert.Equal(TutorState.Showing, Tutor.Engine.State);
    }

    [Fact]
    public void Espera_longa_do_alvo_mostra_o_aviso_e_some_quando_o_alvo_chega()
    {
        var cut = RenderWithTour(TestData.Tour());
        var locate2 = _js.Setup<LayoutChange>("locate", i => (string?)i.Arguments[0] == "alvo-2");

        cut.Find(".tutor-btn--primary").Click();

        cut.WaitForAssertion(() => Assert.Equal("Procurando o próximo passo…", cut.Find(".tutor-waiting .tutor-text").TextContent));
        var waiting = cut.Find(".tutor-waiting");
        Assert.Equal("dialog", waiting.GetAttribute("role"));
        Assert.Equal(cut.Find(".tutor-waiting .tutor-text").Id, waiting.GetAttribute("aria-labelledby"));
        Assert.Equal(["Pular", "Fechar"], cut.FindAll(".tutor-waiting button").Select(b => b.TextContent.Trim()));
        Assert.Empty(cut.FindAll(".tutor-title"));
        cut.WaitForAssertion(() => Assert.Equal(2, _js.Invocations["focus"].Count)); // o aviso recebe o foco (teclado e leitor de tela)

        locate2.SetResult(_layout);
        cut.WaitForAssertion(() => Assert.Equal("Passo 2", cut.Find(".tutor-title").TextContent));
        Assert.Empty(cut.FindAll(".tutor-waiting"));
    }

    [Fact]
    public void Aviso_continua_entre_dois_passos_pulados_seguidos()
    {
        var tour = new TourDefinition("pulos", 1,
        [
            new TourStep("alvo-1", "Passo 1", "Texto 1"),
            new TourStep("alvo-2", "Passo 2", "Texto 2", WhenTargetMissing: MissingTargetPolicy.Skip),
            new TourStep("alvo-3", "Passo 3", "Texto 3", WhenTargetMissing: MissingTargetPolicy.Skip),
            new TourStep("alvo-4", "Passo 4", "Texto 4"),
        ]);
        var cut = RenderWithTour(tour);
        var locate2 = _js.Setup<LayoutChange>("locate", i => (string?)i.Arguments[0] == "alvo-2");
        var locate3 = _js.Setup<LayoutChange>("locate", i => (string?)i.Arguments[0] == "alvo-3");
        cut.Find(".tutor-btn--primary").Click();
        cut.WaitForElement(".tutor-waiting");

        locate2.SetResult(_layout with { Found = false });

        // Já no passo 3, sem esperar outros 400 ms: o aviso não some entre um pulo e outro.
        cut.WaitForAssertion(() => Assert.Equal(["alvo-1", "alvo-2", "alvo-3"], _js.Invocations["locate"].Select(i => (string?)i.Arguments[0])));
        Assert.Single(cut.FindAll(".tutor-waiting"));
        Assert.Equal(2, _js.Invocations["focus"].Count); // continua focado: não roubou o foco de novo

        locate3.SetResult(_layout with { Found = false });
        cut.WaitForAssertion(() => Assert.Equal("Passo 4", cut.Find(".tutor-title").TextContent));
        Assert.Empty(cut.FindAll(".tutor-waiting"));
    }

    [Fact]
    public async Task Aviso_de_espera_vira_faixa_inferior_na_tela_estreita()
    {
        _js.Setup<LayoutChange>("locate", _ => true).SetResult(_layout with { ViewportWidth = 390, ViewportHeight = 800 });
        var cut = Render<TutorHost>();
        await cut.InvokeAsync(() => Tutor.StartAsync(TestData.Tour()));
        cut.WaitForElement(".tutor-popover--sheet .tutor-btn--primary");
        _js.Setup<LayoutChange>("locate", i => (string?)i.Arguments[0] == "alvo-2");
        cut.Find(".tutor-btn--primary").Click();

        cut.WaitForAssertion(() => Assert.Contains("tutor-popover--sheet", cut.Find(".tutor-waiting").ClassName));
        Assert.DoesNotContain("tutor-waiting--center", cut.Find(".tutor-waiting").ClassName);
    }

    [Fact]
    public void Pular_no_aviso_de_espera_pula_o_tour()
    {
        var cut = RenderWithTour(TestData.Tour());
        _js.Setup<LayoutChange>("locate", i => (string?)i.Arguments[0] == "alvo-2");
        cut.Find(".tutor-btn--primary").Click();
        cut.WaitForElement(".tutor-waiting");

        cut.Find(".tutor-waiting .tutor-btn--ghost").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".tutor-root")));
        Assert.Equal(TutorState.Skipped, Tutor.Engine.State);
    }

    [Fact]
    public async Task Passo_sem_espera_de_alvo_nao_mostra_o_aviso()
    {
        var tour = new TourDefinition("sem-espera", 1,
        [
            new TourStep("alvo-1", "Passo 1", "Texto 1"),
            new TourStep("alvo-2", "Passo 2", "Texto 2", WaitForTargetMs: 0),
        ]);
        var cut = RenderWithTour(tour);
        _js.Setup<LayoutChange>("locate", i => (string?)i.Arguments[0] == "alvo-2");
        cut.Find(".tutor-btn--primary").Click();

        await Task.Delay(600);
        Assert.Empty(cut.FindAll(".tutor-waiting"));
    }

    [Fact]
    public void Conteudo_extra_e_progresso_opcional()
    {
        Services.GetRequiredService<TutorOptions>().ShowProgress = false;
        var tour = new TourDefinition("t", 1,
            [new TourStep("a", "T", "x", Placement.Right, Content: b => b.AddMarkupContent(0, "<em>extra</em>"))]);
        var cut = RenderWithTour(tour);

        Assert.Equal("extra", cut.Find(".tutor-content em").TextContent);
        Assert.Empty(cut.FindAll(".tutor-progress"));
        Assert.Contains("tutor-popover--right", cut.Find("[role=dialog]").ClassName);
        Assert.StartsWith("top:", cut.Find(".tutor-arrow").GetAttribute("style"));
    }

    [Fact]
    public void Mudanca_de_layout_reposiciona_o_recorte()
    {
        var cut = RenderWithTour(TestData.Tour());
        cut.InvokeAsync(() => cut.Instance.OnLayoutChanged(new LayoutChange(true, 400, 300, 100, 20, 1280, 720)));
        cut.WaitForAssertion(() => Assert.Equal("392", cut.Find(".tutor-hole").GetAttribute("x")));
        Assert.Throws<ArgumentNullException>(() => cut.Instance.OnLayoutChanged(null!));
    }

    [Fact]
    public void Mudanca_de_layout_sem_tour_e_ignorada()
    {
        var cut = Render<TutorHost>();
        cut.InvokeAsync(() => cut.Instance.OnLayoutChanged(_layout));
        Assert.Empty(cut.FindAll(".tutor-root"));
    }

    [Fact]
    public async Task Tour_iniciado_antes_do_host_aparece_quando_o_host_renderiza()
    {
        await Tutor.StartAsync(TestData.Tour());
        var cut = Render<TutorHost>();
        cut.WaitForAssertion(() => Assert.Equal("Passo 1", cut.Find(".tutor-title").TextContent));
    }

    [Fact]
    public async Task Destaque_mostra_o_texto_e_fecha_no_fundo_no_x_e_no_esc()
    {
        var cut = Render<TutorHost>();
        foreach (var close in new Action[]
                 {
                     () => cut.Find(".tutor-blocker").Click(),
                     () => cut.Find(".tutor-close").Click(),
                     () => cut.InvokeAsync(() => cut.Instance.OnKey("Escape")),
                 })
        {
            await cut.InvokeAsync(() => Tutor.HighlightAsync("salvo", "Salvo aqui"));
            cut.WaitForAssertion(() => Assert.Contains("top:", cut.Find("[role=dialog]").GetAttribute("style")));
            Assert.Equal("Salvo aqui", cut.Find(".tutor-text").TextContent);
            Assert.Empty(cut.FindAll(".tutor-actions"));
            Assert.Equal(cut.Find(".tutor-text").Id, cut.Find("[role=dialog]").GetAttribute("aria-labelledby"));

            await cut.InvokeAsync(() => cut.Instance.OnKey("ArrowRight"));
            close();
            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".tutor-root")));
        }
    }

    // ---- Fase 2 ----

    [Fact]
    public void Fora_do_recorte_quatro_bloqueadores_e_sem_recorte_um_so()
    {
        var cut = RenderWithTour(TestData.Tour());
        Assert.Equal(4, cut.FindAll(".tutor-blocker").Count);

        cut.InvokeAsync(() => cut.Instance.OnLayoutChanged(_layout with { Found = false }));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".tutor-blocker")));
    }

    [Fact]
    public void Locate_recebe_a_espera_e_o_modo_de_avanco_do_passo()
    {
        var tour = new TourDefinition("t", 1, [new TourStep("a", "T", "x", Advance: AdvanceMode.InputFilled, WaitForTargetMs: 1234)]);
        RenderWithTour(tour);
        var call = Assert.Single(_js.Invocations["locate"]);
        Assert.Equal(["a", 1234, "InputFilled"], call.Arguments);
    }

    [Fact]
    public async Task Passo_ClickTarget_esconde_o_Proximo_e_avanca_no_clique_do_alvo()
    {
        var tour = new TourDefinition("t", 1,
        [
            new TourStep("a", "Clique", "x", Advance: AdvanceMode.ClickTarget),
            new TourStep("b", "Depois", "y"),
        ]);
        ArmAction();
        var cut = RenderWithTour(tour);
        Assert.Empty(cut.FindAll(".tutor-btn--primary"));
        Assert.Equal(true, _js.Invocations["focus"].Single().Arguments[1]);
        Assert.Equal("false", cut.Find("[role=dialog]").GetAttribute("aria-modal"));

        await cut.InvokeAsync(() => cut.Instance.OnKey("ArrowRight"));
        Assert.Equal(0, Tutor.Engine.Index);

        await cut.InvokeAsync(() => cut.Instance.OnTargetAction());
        cut.WaitForAssertion(() => Assert.Equal("Depois", cut.Find(".tutor-title").TextContent));
        Assert.NotEmpty(cut.FindAll(".tutor-btn--primary"));
    }

    [Fact]
    public void Passo_InputFilled_foca_o_campo_do_alvo()
    {
        var tour = new TourDefinition("t", 1, [new TourStep("a", "Campo", "x", Advance: AdvanceMode.InputFilled)]);
        ArmAction();
        RenderWithTour(tour);
        Assert.Equal(true, _js.Invocations["focus"].Single().Arguments[1]);
    }

    [Fact]
    public async Task Acao_que_nao_pode_acontecer_mostra_o_Proximo_e_e_ignorada()
    {
        // Botão desabilitado, campo já preenchido ou sem campo: o JS devolve ActionArmed = false.
        var tour = new TourDefinition("t", 1, [new TourStep("a", "Clique", "x", Advance: AdvanceMode.ClickTarget)]);
        var cut = RenderWithTour(tour);

        Assert.NotEmpty(cut.FindAll(".tutor-btn--primary"));
        Assert.Equal(false, _js.Invocations["focus"].Single().Arguments[1]);
        await cut.InvokeAsync(() => cut.Instance.OnTargetAction());
        Assert.Equal(TutorState.Showing, Tutor.Engine.State);
    }

    [Fact]
    public async Task Passo_de_acao_sem_alvo_volta_a_mostrar_o_Proximo_e_ignora_a_acao()
    {
        _js.Setup<LayoutChange>("locate", _ => true).SetResult(_layout with { Found = false });
        var tour = new TourDefinition("t", 1, [new TourStep("a", "Clique", "x", Advance: AdvanceMode.ClickTarget)]);
        var cut = RenderWithTour(tour);

        Assert.NotEmpty(cut.FindAll(".tutor-btn--primary"));
        await cut.InvokeAsync(() => cut.Instance.OnTargetAction());
        Assert.Equal(TutorState.Showing, Tutor.Engine.State);
    }

    [Fact]
    public async Task Acao_do_alvo_fora_de_tour_e_ignorada()
    {
        var cut = Render<TutorHost>();
        await cut.InvokeAsync(() => cut.Instance.OnTargetAction());
        Assert.Equal(TutorState.Idle, Tutor.Engine.State);
    }

    [Fact]
    public void Passo_em_outra_pagina_navega_e_so_entao_procura_o_alvo()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        var tour = new TourDefinition("t", 1,
        [
            new TourStep("a", "Aqui", "x"),
            new TourStep("b", "Lá", "y", Route: "/obras?aba=1"),
        ]);
        var cut = RenderWithTour(tour);
        Assert.Single(_js.Invocations["locate"]);

        cut.Find(".tutor-btn--primary").Click();
        cut.WaitForAssertion(() => Assert.Equal("Lá", cut.Find(".tutor-title").TextContent));
        Assert.EndsWith("/obras?aba=1", nav.Uri);
        Assert.Equal(2, _js.Invocations["locate"].Count);
        Assert.Single(_js.Invocations["arm"]);
    }

    [Fact]
    public void Endereco_fora_da_base_do_app_conta_como_outra_pagina()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("https://outro.exemplo/fora");
        var cut = RenderWithTour(new TourDefinition("t", 1, [new TourStep("a", "Aqui", "x", Route: "/obras")]));
        Assert.EndsWith("/obras", nav.Uri);
        Assert.Equal("Aqui", cut.Find(".tutor-title").TextContent);
    }

    [Fact]
    public void Passo_na_pagina_atual_nao_navega()
    {
        var nav = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/Obras/");
        var history = nav.History.Count;
        RenderWithTour(new TourDefinition("t", 1, [new TourStep("a", "Aqui", "x", Route: "obras")]));
        Assert.Equal(history, nav.History.Count);
    }

    [Fact]
    public void Navegacao_que_nao_e_do_tour_nao_relocaliza()
    {
        var cut = RenderWithTour(TestData.Tour());
        Services.GetRequiredService<NavigationManager>().NavigateTo("/outra");
        Assert.Single(_js.Invocations["locate"]);
        Assert.NotEmpty(cut.FindAll("[role=dialog]"));
    }

    [Fact]
    public async Task Tela_estreita_vira_faixa_inferior_sem_seta()
    {
        _js.Setup<LayoutChange>("locate", _ => true).SetResult(_layout with { ViewportWidth = 375 });
        var cut = Render<TutorHost>();
        await cut.InvokeAsync(() => Tutor.StartAsync(TestData.Tour()));
        cut.WaitForAssertion(() => Assert.Contains("tutor-popover--sheet", cut.Find("[role=dialog]").ClassName));
        cut.WaitForAssertion(() => Assert.Equal(true, _js.Invocations["focus"].Single().Arguments[2]));
        Assert.Empty(cut.FindAll(".tutor-arrow"));
        Assert.Equal("", cut.Find("[role=dialog]").GetAttribute("style") ?? "");
        Assert.NotEmpty(cut.FindAll(".tutor-hole"));
    }

    [Fact]
    public async Task Dispose_desativa_o_js_e_se_desliga_do_servico()
    {
        var cut = RenderWithTour(TestData.Tour());
        await cut.Instance.DisposeAsync();
        Assert.NotEmpty(_js.Invocations["deactivate"]);
    }

    [Fact]
    public async Task Dispose_com_circuito_caido_nao_lanca()
    {
        _js.SetupVoid("deactivate", _ => true).SetException(new Microsoft.JSInterop.JSDisconnectedException("caiu"));
        var cut = Render<TutorHost>();
        cut.WaitForAssertion(() => Assert.Single(JSInterop.Invocations["import"]));
        var error = await Record.ExceptionAsync(() => cut.Instance.DisposeAsync().AsTask());
        Assert.Null(error);
        Assert.Single(_js.Invocations["deactivate"]);
    }

    // "-" = speak(null), o áudio parou.
    private string[] Spoken => [.. _js.Invocations["speak"].Select(c => (string?)c.Arguments[0] ?? "-")];

    private AngleSharp.Dom.IElement Tool(IRenderedComponent<TutorHost> cut, string text) =>
        cut.FindAll(".tutor-tools button").Single(b => b.TextContent.Trim() == text);

    [Fact]
    public void Sem_apresentador_nem_audio_nao_ha_ferramentas_nem_som()
    {
        var cut = RenderWithTour(TestData.Tour());
        Assert.Empty(cut.FindAll(".tutor-tools"));
        Assert.Empty(cut.FindAll(".tutor-presenter"));
        Assert.Empty(_js.Invocations["speak"]);
    }

    [Fact]
    public void Apresentador_aparece_no_canto_com_a_pose_do_passo_e_depois_a_padrao()
    {
        var cut = RenderWithTour(TestData.Narrated(TestData.Aly));

        var img = cut.Find(".tutor-presenter");
        Assert.Equal("/aly-explica.png", img.GetAttribute("src"));
        Assert.Equal("", img.GetAttribute("alt"));
        Assert.Equal("top:420px;left:1072px;width:200px;height:300px", img.GetAttribute("style"));

        cut.Find(".tutor-btn--primary").Click();
        cut.WaitForAssertion(() => Assert.Equal("/aly.png", cut.Find(".tutor-presenter").GetAttribute("src")));
    }

    [Fact]
    public void Pose_animada_so_para_quem_nao_pediu_menos_movimento_e_sem_herdar_a_padrao()
    {
        var aly = TestData.Aly with { ImageAnimated = "/aly.webp" };
        var tour = new TourDefinition("animado", 1,
        [
            new TourStep("alvo-1", "Passo 1", "Texto 1", Pose: "/aly-acena.png", PoseAnimated: "/aly-acena.webp"),
            new TourStep("alvo-2", "Passo 2", "Texto 2"),
            new TourStep("alvo-3", "Passo 3", "Texto 3", Pose: "/aly-explica.png"),
        ])
        { Presenter = aly };
        var cut = RenderWithTour(tour);

        var source = cut.Find("picture source");
        Assert.Equal("/aly-acena.webp", source.GetAttribute("srcset"));
        Assert.Equal("(prefers-reduced-motion: no-preference)", source.GetAttribute("media"));
        Assert.Equal("/aly-acena.png", cut.Find("picture img.tutor-presenter").GetAttribute("src"));
        Assert.Equal("true", cut.Find("picture").GetAttribute("aria-hidden"));

        cut.Find(".tutor-btn--primary").Click();
        cut.WaitForAssertion(() => Assert.Equal("/aly.png", cut.Find(".tutor-presenter").GetAttribute("src")));
        Assert.Equal("/aly.webp", cut.Find("picture source").GetAttribute("srcset"));

        // Pose própria sem animada: fica parada, não pega a animação da imagem padrão.
        cut.Find(".tutor-btn--primary").Click();
        cut.WaitForAssertion(() => Assert.Equal("/aly-explica.png", cut.Find(".tutor-presenter").GetAttribute("src")));
        Assert.Empty(cut.FindAll("picture source"));
    }

    [Fact]
    public void Apresentador_fica_com_a_pose_anterior_enquanto_o_passo_seguinte_se_prepara()
    {
        var cut = RenderWithTour(TestData.Narrated(TestData.Aly));
        _js.Setup<LayoutChange>("locate", i => (string?)i.Arguments[0] == "alvo-2");

        cut.Find(".tutor-btn--primary").Click();

        // #49: sem balão (Preparing), o apresentador não sai do DOM; só troca de pose quando o passo aparece.
        cut.WaitForAssertion(() => Assert.Equal(TutorState.Preparing, Tutor.Engine.State));
        Assert.Empty(cut.FindAll("[role=dialog] .tutor-title"));
        Assert.Equal("/aly-explica.png", cut.Find(".tutor-presenter").GetAttribute("src"));
    }

    [Fact]
    public async Task Outro_tour_por_cima_nao_mostra_o_apresentador_do_anterior()
    {
        var cut = RenderWithTour(TestData.Narrated(TestData.Aly));
        Assert.Single(cut.FindAll(".tutor-presenter"));
        _js.Setup<LayoutChange>("locate", i => (string?)i.Arguments[0] == "outro-1");

        var outro = new TourDefinition("outro", 1, [new TourStep("outro-1", "Outro", "Texto")]) { Presenter = new("Guia", "/guia.png") };
        await cut.InvokeAsync(() => Tutor.StartAsync(outro, force: true));

        cut.WaitForAssertion(() => Assert.Equal(TutorState.Preparing, Tutor.Engine.State));
        Assert.Empty(cut.FindAll(".tutor-presenter"));
    }

    [Fact]
    public void Ocultar_tira_a_imagem_e_a_voz_sem_tirar_o_foco_do_botao()
    {
        var cut = RenderWithTour(TestData.Narrated(TestData.Aly));
        Assert.Equal(["/p1.mp3"], Spoken);
        Assert.Equal(["Ocultar Aly", "Silenciar"], cut.FindAll(".tutor-tools button").Select(b => b.TextContent.Trim()));

        Tool(cut, "Ocultar Aly").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, _js.Invocations["measure"].Count));
        Assert.Empty(cut.FindAll(".tutor-presenter"));
        Assert.Equal(["Mostrar Aly"], cut.FindAll(".tutor-tools button").Select(b => b.TextContent.Trim()));
        Assert.Equal(["/p1.mp3", "-"], Spoken);
        Assert.Single(_js.Invocations["focus"]);

        Tool(cut, "Mostrar Aly").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".tutor-presenter")));
        Assert.Equal(["/p1.mp3", "-", "/p1.mp3"], Spoken);
    }

    [Fact]
    public void Silenciar_para_o_audio_e_reativar_toca_o_passo_de_novo()
    {
        var cut = RenderWithTour(TestData.Narrated());
        Tool(cut, "Silenciar").Click();
        cut.WaitForAssertion(() => Assert.Equal(["/p1.mp3", "-"], Spoken));

        Tool(cut, "Ativar som").Click();
        cut.WaitForAssertion(() => Assert.Equal(["/p1.mp3", "-", "/p1.mp3"], Spoken));
    }

    [Fact]
    public void Trocar_de_passo_para_o_audio_e_toca_o_do_passo_novo()
    {
        var cut = RenderWithTour(TestData.Narrated());
        cut.Find(".tutor-btn--primary").Click();
        cut.WaitForAssertion(() => Assert.Equal("Passo 2", cut.Find(".tutor-title").TextContent));
        Assert.Empty(cut.FindAll(".tutor-tools")); // sem apresentador e passo sem áudio

        cut.Find(".tutor-btn--primary").Click();
        cut.WaitForAssertion(() => Assert.Equal(["/p1.mp3", "-", "/p3.mp3"], Spoken));
    }

    [Fact]
    public void Apresentador_some_na_tela_estreita()
    {
        var cut = RenderWithTour(TestData.Narrated(TestData.Aly));
        cut.InvokeAsync(() => cut.Instance.OnLayoutChanged(_layout with { ViewportWidth = 400 }));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".tutor-presenter")));
    }

    [Fact]
    public void Botoes_do_apresentador_em_ingles()
    {
        Services.GetRequiredService<TutorOptions>().Language = "en";
        var cut = RenderWithTour(TestData.Narrated(TestData.Aly));
        Assert.Equal(["Hide Aly", "Mute"], cut.FindAll(".tutor-tools button").Select(b => b.TextContent.Trim()));
    }
}
