using Microsoft.Extensions.Logging.Abstractions;
using RVM.UI.Tutor.Internal;

namespace RVM.UI.Tutor.Tests;

public class TutorServiceTests
{
    private readonly FakeProgressStore _store = new();
    private readonly TutorService _tutor;
    private readonly List<TutorEvent> _events = [];
    private int _changes;

    public TutorServiceTests()
    {
        _tutor = new TutorService(_store, new TutorOptions(), NullLogger<TutorService>.Instance);
        _tutor.EventRaised += _events.Add;
        _tutor.Changed += () => _changes++;
    }

    private async Task ShowAsync() => await _tutor.ReportTargetAsync(true);

    [Fact]
    public async Task Start_sem_usuario_nao_toca_no_progresso()
    {
        await _tutor.StartAsync(TestData.Tour());
        await ShowAsync();
        await _tutor.NextAsync();

        Assert.Equal(0, _store.Saves);
        Assert.True(_tutor.IsActive);
        Assert.Equal("Passo 2", _tutor.CurrentStep!.Title);
        Assert.True(_changes > 0);
    }

    [Fact]
    public async Task Tour_concluido_salva_Completed()
    {
        await _tutor.StartAsync(TestData.Tour(2), "u");
        await ShowAsync();
        await _tutor.NextAsync();
        await ShowAsync();
        await _tutor.NextAsync();

        var saved = _store.Saved[("tour", "u")];
        Assert.Equal(TourStatus.Completed, saved.Status);
        Assert.Equal(1, saved.LastStep);
        Assert.Equal(1, saved.Version);
        Assert.Equal(TutorEventKind.Completed, _events[^1].Kind);
        Assert.False(_tutor.IsActive);
    }

    [Fact]
    public async Task Pular_no_passo_2_salva_Skipped()
    {
        await _tutor.StartAsync(TestData.Tour(), "u");
        await ShowAsync();
        await _tutor.NextAsync();
        await ShowAsync();
        await _tutor.SkipAsync();

        var saved = _store.Saved[("tour", "u")];
        Assert.Equal(TourStatus.Skipped, saved.Status);
        Assert.Equal(1, saved.LastStep);
    }

    [Fact]
    public async Task Fechar_no_passo_2_e_reabrir_retoma_no_passo_2()
    {
        await _tutor.StartAsync(TestData.Tour(), "u");
        await ShowAsync();
        await _tutor.NextAsync();
        await ShowAsync();
        await _tutor.CloseAsync();
        Assert.Equal(TourStatus.InProgress, _store.Saved[("tour", "u")].Status);

        await _tutor.StartAsync(TestData.Tour(), "u");
        Assert.Equal("Passo 2", _tutor.CurrentStep!.Title);
    }

    [Fact]
    public async Task Forcar_comeca_do_primeiro()
    {
        _store.Seed("tour", "u", 1, TourStatus.InProgress, 2);
        await _tutor.StartAsync(TestData.Tour(), "u", force: true);
        Assert.Equal("Passo 1", _tutor.CurrentStep!.Title);
    }

    [Fact]
    public async Task Progresso_de_outra_versao_nao_retoma()
    {
        _store.Seed("tour", "u", 1, TourStatus.InProgress, 2);
        await _tutor.StartAsync(TestData.Tour(version: 2), "u");
        Assert.Equal("Passo 1", _tutor.CurrentStep!.Title);
    }

    [Fact]
    public async Task StartIfNeverSeen_nunca_visto_abre()
    {
        Assert.True(await _tutor.StartIfNeverSeenAsync(TestData.Tour(), "u"));
        Assert.True(_tutor.IsActive);
    }

    [Theory]
    [InlineData(TourStatus.Completed)]
    [InlineData(TourStatus.Skipped)]
    public async Task StartIfNeverSeen_ja_visto_nesta_versao_nao_abre(TourStatus status)
    {
        _store.Seed("tour", "u", 1, status, 0);
        Assert.False(await _tutor.StartIfNeverSeenAsync(TestData.Tour(), "u"));
        Assert.False(_tutor.IsActive);
    }

    [Fact]
    public async Task StartIfNeverSeen_fechado_no_meio_conta_como_visto_e_StartAsync_retoma()
    {
        _store.Seed("tour", "u", 1, TourStatus.InProgress, 2);
        Assert.False(await _tutor.StartIfNeverSeenAsync(TestData.Tour(), "u"));
        Assert.False(_tutor.IsActive);

        await _tutor.StartAsync(TestData.Tour(), "u");
        Assert.Equal("Passo 3", _tutor.CurrentStep!.Title);
    }

    [Fact]
    public async Task Tour_v1_visto_roteiro_vira_v2_reaparece()
    {
        _store.Seed("tour", "u", 1, TourStatus.Completed, 2);
        Assert.True(await _tutor.StartIfNeverSeenAsync(TestData.Tour(version: 2), "u"));
        Assert.Equal("Passo 1", _tutor.CurrentStep!.Title);
    }

    [Fact]
    public async Task Versao_nova_sem_ShowAgain_nao_reaparece()
    {
        _store.Seed("tour", "u", 1, TourStatus.Completed, 2);
        var tour = TestData.Tour(version: 2) with { ShowAgainOnNewVersion = false };
        Assert.False(await _tutor.StartIfNeverSeenAsync(tour, "u"));
    }

    [Fact]
    public async Task Progresso_de_versao_mais_nova_nao_abre_roteiro_antigo()
    {
        _store.Seed("tour", "u", 3, TourStatus.Completed, 0);
        Assert.False(await _tutor.StartIfNeverSeenAsync(TestData.Tour(version: 2), "u"));
    }

    [Fact]
    public async Task Eventos_chegam_ao_app_na_ordem()
    {
        await _tutor.StartAsync(TestData.Tour(1));
        await ShowAsync();
        await _tutor.NextAsync();

        Assert.Equal([TutorEventKind.Started, TutorEventKind.StepShown, TutorEventKind.Completed], _events.Select(e => e.Kind));
    }

    [Fact]
    public async Task Back_volta_um_passo()
    {
        await _tutor.StartAsync(TestData.Tour(), null);
        await ShowAsync();
        await _tutor.NextAsync();
        await ShowAsync();
        await _tutor.BackAsync();
        Assert.Equal("Passo 1", _tutor.CurrentStep!.Title);
    }

    [Fact]
    public async Task Argumentos_nulos_sao_erro()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _tutor.StartAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _tutor.StartIfNeverSeenAsync(null!, "u"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _tutor.StartIfNeverSeenAsync(TestData.Tour(), null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _tutor.HighlightAsync(" ", "x"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _tutor.HighlightAsync("a", null!));
    }

    // ---- Passo que espera ação ----

    [Fact]
    public async Task Acao_do_alvo_avanca_so_passo_que_espera_acao()
    {
        var tour = new TourDefinition("t", 1,
        [
            new TourStep("a", "A", "a"),
            new TourStep("b", "B", "b", Advance: AdvanceMode.ClickTarget),
            new TourStep("c", "C", "c"),
        ]);
        await _tutor.StartAsync(tour);
        await ShowAsync();
        Assert.False(_tutor.WaitsForAction);
        await _tutor.TargetActionAsync();
        Assert.Equal("A", _tutor.CurrentStep!.Title);

        await _tutor.NextAsync();
        Assert.False(_tutor.WaitsForAction); // ainda preparando
        await _tutor.TargetActionAsync();
        Assert.Equal("B", _tutor.CurrentStep!.Title);

        await ShowAsync();
        Assert.True(_tutor.WaitsForAction);
        await _tutor.TargetActionAsync();
        Assert.Equal("C", _tutor.CurrentStep!.Title);
    }

    [Fact]
    public async Task Passo_de_acao_centralizado_nao_espera_acao()
    {
        await _tutor.StartAsync(new TourDefinition("t", 1, [new TourStep("a", "A", "a", Advance: AdvanceMode.InputFilled)]));
        await _tutor.ReportTargetAsync(false);
        Assert.False(_tutor.WaitsForAction);
    }

    // ---- Falhas que não podem travar o tour ----

    [Fact]
    public async Task Store_que_falha_ao_ler_nao_abre_sozinho_mas_StartAsync_abre_do_comeco()
    {
        var tutor = new TutorService(new BrokenStore(), new TutorOptions(), NullLogger<TutorService>.Instance);
        Assert.False(await tutor.StartIfNeverSeenAsync(TestData.Tour(), "u"));
        Assert.False(tutor.IsActive);
        await tutor.StartAsync(TestData.Tour(), "u");
        Assert.Equal("Passo 1", tutor.CurrentStep!.Title);
    }

    [Fact]
    public async Task Store_que_falha_ao_salvar_nao_trava_o_tour()
    {
        var tutor = new TutorService(new BrokenStore(), new TutorOptions(), NullLogger<TutorService>.Instance);
        var changes = 0;
        tutor.Changed += () => changes++;
        await tutor.StartAsync(TestData.Tour(2), "u");
        await tutor.ReportTargetAsync(true);
        await tutor.NextAsync();
        await tutor.ReportTargetAsync(true);
        await tutor.NextAsync();

        Assert.Equal(TutorState.Completed, tutor.Engine.State);
        Assert.Equal(5, changes);
    }

    [Fact]
    public async Task Handler_do_app_que_lanca_ainda_deixa_o_host_renderizar()
    {
        await _tutor.StartAsync(TestData.Tour());
        _tutor.EventRaised += _ => throw new InvalidOperationException("bug do app");
        var before = _changes;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _tutor.ReportTargetAsync(true));
        Assert.Equal(before + 1, _changes);
        Assert.Equal(TutorState.Showing, _tutor.Engine.State);
    }

    private sealed class BrokenStore : ITutorProgressStore
    {
        public Task<TutorProgress?> GetAsync(string tourId, string user) => throw new InvalidOperationException("storage bloqueado");
        public Task SaveAsync(TutorProgress progress) => throw new InvalidOperationException("cota estourada");
    }

    // ---- Destaque ----

    [Fact]
    public async Task Destaque_aparece_e_some_no_dismiss()
    {
        await _tutor.HighlightAsync("salvo", "Salvo aqui");
        Assert.Equal(new HighlightRequest("salvo", "Salvo aqui"), _tutor.Highlight);

        _tutor.DismissHighlight();
        Assert.Null(_tutor.Highlight);

        var before = _changes;
        _tutor.DismissHighlight();
        Assert.Equal(before, _changes);
    }

    [Fact]
    public async Task Destaque_com_duracao_some_sozinho()
    {
        var gone = new TaskCompletionSource();
        _tutor.Changed += () => { if (_tutor.Highlight is null) gone.TrySetResult(); };

        await _tutor.HighlightAsync("salvo", "Salvo", TimeSpan.FromMilliseconds(20));
        await gone.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(_tutor.Highlight);
    }

    [Fact]
    public async Task Novo_destaque_cancela_o_timer_do_anterior()
    {
        await _tutor.HighlightAsync("a", "A", TimeSpan.FromMilliseconds(30));
        await _tutor.HighlightAsync("b", "B");
        await Task.Delay(120);
        Assert.Equal("b", _tutor.Highlight!.Target);
    }

    [Fact]
    public async Task Destaque_e_ignorado_com_tour_aberto_e_tour_limpa_destaque()
    {
        await _tutor.HighlightAsync("a", "A");
        await _tutor.StartAsync(TestData.Tour());
        Assert.Null(_tutor.Highlight);

        await _tutor.HighlightAsync("b", "B");
        Assert.Null(_tutor.Highlight);
    }
}

public class TutorServicePresenterTests
{
    private readonly FakeProgressStore _store = new();
    private readonly TutorOptions _options = new();
    private readonly TutorService _tutor;

    public TutorServicePresenterTests() =>
        _tutor = new TutorService(_store, _options, NullLogger<TutorService>.Instance);

    private async Task StartAsync(TourDefinition tour, string? user = "u", bool force = false)
    {
        await _tutor.StartAsync(tour, user, force);
        await _tutor.ReportTargetAsync(true);
    }

    [Fact]
    public async Task Sem_apresentador_o_audio_toca_sozinho_e_o_progresso_nao_guarda_escolha()
    {
        await StartAsync(TestData.Narrated());

        Assert.Null(_tutor.Presenter);
        Assert.False(_tutor.PresenterShown);
        Assert.True(_tutor.CanSpeak);
        Assert.Equal("/p1.mp3", _tutor.StepAudio);
        Assert.Null(_store.Saved[("narrado", "u")].ShowPresenter);
    }

    [Fact]
    public async Task Apresentador_global_aparece_e_o_progresso_guarda_que_estava_visivel()
    {
        _options.Presenter = TestData.Aly;
        await StartAsync(TestData.Narrated());

        Assert.Same(TestData.Aly, _tutor.Presenter);
        Assert.True(_tutor.PresenterShown);
        Assert.True(_store.Saved[("narrado", "u")].ShowPresenter);
    }

    [Fact]
    public async Task Apresentador_do_tour_vence_o_global()
    {
        _options.Presenter = new TourPresenter("Outro", "/outro.png");
        await StartAsync(TestData.Narrated(TestData.Aly));
        Assert.Same(TestData.Aly, _tutor.Presenter);
    }

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, null, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    public async Task Padrao_de_visibilidade_vem_do_tour_senao_das_opcoes(bool global, bool? perTour, bool expected)
    {
        _options.ShowPresenter = global;
        await StartAsync(TestData.Narrated(TestData.Aly) with { ShowPresenter = perTour });
        Assert.Equal(expected, _tutor.PresenterShown);
    }

    [Fact]
    public async Task Ocultar_cala_o_apresentador_e_salva_a_escolha_no_passo_atual()
    {
        await StartAsync(TestData.Narrated(TestData.Aly));
        await _tutor.TogglePresenterAsync();

        Assert.False(_tutor.PresenterShown);
        Assert.False(_tutor.CanSpeak);
        Assert.Null(_tutor.StepAudio);
        Assert.Equal(new { Status = TourStatus.InProgress, Step = 0, Show = (bool?)false },
            _store.Saved[("narrado", "u")] is var p ? new { p.Status, Step = p.LastStep, Show = p.ShowPresenter } : null);

        await _tutor.TogglePresenterAsync();
        Assert.True(_tutor.PresenterShown);
        Assert.Equal("/p1.mp3", _tutor.StepAudio);
    }

    [Fact]
    public async Task A_escolha_volta_ao_retomar_e_ao_recomecar_com_force()
    {
        await StartAsync(TestData.Narrated(TestData.Aly));
        await _tutor.TogglePresenterAsync();
        await _tutor.CloseAsync();

        await StartAsync(TestData.Narrated(TestData.Aly));
        Assert.False(_tutor.PresenterShown);

        await StartAsync(TestData.Narrated(TestData.Aly), force: true);
        Assert.Equal(0, _tutor.Engine.Index);
        Assert.False(_tutor.PresenterShown);
    }

    [Fact]
    public async Task Sem_usuario_a_escolha_vale_so_neste_tour()
    {
        await StartAsync(TestData.Narrated(TestData.Aly), user: null);
        await _tutor.TogglePresenterAsync();
        Assert.False(_tutor.PresenterShown);
        Assert.Equal(0, _store.Saves);
    }

    [Fact]
    public async Task Alternar_sem_apresentador_ou_fora_do_passo_mostrado_nao_faz_nada()
    {
        await _tutor.TogglePresenterAsync();
        await StartAsync(TestData.Narrated());
        await _tutor.TogglePresenterAsync();
        Assert.True(_tutor.CanSpeak);

        await _tutor.StartAsync(TestData.Narrated(TestData.Aly), "u");
        var saves = _store.Saves;
        await _tutor.TogglePresenterAsync(); // ainda em Preparing
        Assert.True(_tutor.PresenterShown);
        Assert.Equal(saves, _store.Saves);
    }

    [Fact]
    public async Task Mudo_cala_ate_ser_desfeito_e_passo_sem_audio_nao_toca()
    {
        await StartAsync(TestData.Narrated(TestData.Aly));
        var changes = 0;
        _tutor.Changed += () => changes++;

        _tutor.ToggleMute();
        Assert.True(_tutor.Muted);
        Assert.Null(_tutor.StepAudio);
        _tutor.ToggleMute();
        Assert.Equal("/p1.mp3", _tutor.StepAudio);
        Assert.Equal(2, changes);

        await _tutor.NextAsync();
        Assert.Null(_tutor.StepAudio); // Preparing
        await _tutor.ReportTargetAsync(true);
        Assert.Null(_tutor.StepAudio); // passo 2 não tem áudio
    }
}
