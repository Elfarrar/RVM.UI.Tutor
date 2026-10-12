using Microsoft.Extensions.Logging;

namespace RVM.UI.Tutor.Internal;

internal sealed record HighlightRequest(string Target, string Text);

/// <summary>
/// Implementação do <see cref="ITutor"/>: liga o motor ao progresso e avisa o <see cref="TutorHost"/>.
/// O host mede o alvo e devolve com <see cref="ReportTargetAsync"/>.
/// </summary>
internal sealed class TutorService(ITutorProgressStore store, TutorOptions options, ILogger<TutorService> logger) : ITutor
{
    private CancellationTokenSource? _highlightTimer;
    private string? _user;
    private bool _presenterOn;

    public TutorEngine Engine { get; } = new();
    public TutorOptions Options => options;
    public HighlightRequest? Highlight { get; private set; }

    /// <summary>Estado mudou; o host re-renderiza.</summary>
    public event Action? Changed;

    public event Action<TutorEvent>? EventRaised;

    public bool IsActive => Engine.IsActive;
    public TourStep? CurrentStep => Engine.CurrentStep;

    /// <summary>Apresentador do tour aberto (o do tour, senão o global), mesmo oculto pelo usuário.</summary>
    public TourPresenter? Presenter => Engine.IsActive ? Engine.Tour!.Presenter ?? options.Presenter : null;

    public bool PresenterShown => Presenter is not null && _presenterOn;

    /// <summary>Som desligado pelo usuário; vale até o fim do circuito (Server) ou da aba (WASM).</summary>
    public bool Muted { get; private set; }

    /// <summary>A voz é do apresentador: oculto, ele não fala. Tour sem apresentador fala sozinho.</summary>
    public bool CanSpeak => Presenter is null || _presenterOn;

    /// <summary>Áudio que deve estar tocando agora, ou <c>null</c>.</summary>
    public string? StepAudio =>
        Engine.State == TutorState.Showing && CanSpeak && !Muted ? Engine.CurrentStep!.Audio : null;

    public async Task StartAsync(TourDefinition tour, string? user = null, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(tour);
        // Lido mesmo com force: o "do começo" recomeça os passos, não a escolha do apresentador.
        TutorProgress? saved = null;
        if (user is not null)
            (_, saved) = await LoadAsync(tour.Id, user);
        var start = !force && saved is { Status: TourStatus.InProgress } && saved.Version == tour.Version
            ? saved.LastStep
            : 0;
        await BeginAsync(tour, user, start, saved);
    }

    public async Task<bool> StartIfNeverSeenAsync(TourDefinition tour, string user)
    {
        ArgumentNullException.ThrowIfNull(tour);
        ArgumentNullException.ThrowIfNull(user);
        // Qualquer registro desta versão conta como visto, inclusive o tour fechado no meio: senão ele
        // reabriria a cada recarga de página (achado no ObraEmDia, TUTOR-004). Retomar é no StartAsync.
        // Leitura que FALHOU não é "nunca visto": abrir sozinho a cada falha da API reabriria o tour a
        // cada carga de página (review do ObraEmDia, TUTOR-004). Na dúvida, não abre.
        var (read, saved) = await LoadAsync(tour.Id, user);
        if (!read || (saved is not null && (saved.Version >= tour.Version || !tour.ShowAgainOnNewVersion)))
            return false;
        await BeginAsync(tour, user, 0, saved);
        return true;
    }

    /// <summary>O usuário ligou/desligou o apresentador no balão. Fica no progresso, para valer ao retomar.</summary>
    public async Task TogglePresenterAsync()
    {
        if (Presenter is null || Engine.State != TutorState.Showing)
            return;
        _presenterOn = !_presenterOn;
        Changed?.Invoke();
        await SaveAsync(TourStatus.InProgress, Engine.Index);
    }

    public void ToggleMute()
    {
        Muted = !Muted;
        Changed?.Invoke();
    }

    public Task NextAsync() => ApplyAsync(Engine.Next());
    public Task BackAsync() => ApplyAsync(Engine.Back());
    public Task SkipAsync() => ApplyAsync(Engine.Skip());
    public Task CloseAsync() => ApplyAsync(Engine.Close());

    public Task ReportTargetAsync(bool found) => ApplyAsync(Engine.TargetResolved(found));

    /// <summary>O usuário clicou no alvo ou preencheu o campo (vem do JS). Só avança passo que espera ação.</summary>
    public Task TargetActionAsync() => WaitsForAction ? NextAsync() : Task.CompletedTask;

    /// <summary>Passo mostrado com recorte e avanço por ação: o "Próximo" fica oculto.</summary>
    public bool WaitsForAction =>
        Engine.State == TutorState.Showing && !Engine.Centered && Engine.CurrentStep!.Advance != AdvanceMode.Button;

    public Task HighlightAsync(string target, string text, TimeSpan? duration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(text);
        if (Engine.IsActive)
            return Task.CompletedTask;

        CancelHighlightTimer();
        Highlight = new HighlightRequest(target, text);
        Changed?.Invoke();
        if (duration is { } after)
        {
            var timer = _highlightTimer = new CancellationTokenSource();
            _ = DismissLaterAsync(after, timer.Token);
        }
        return Task.CompletedTask;
    }

    public void DismissHighlight()
    {
        CancelHighlightTimer();
        if (Highlight is null)
            return;
        Highlight = null;
        Changed?.Invoke();
    }

    private async Task DismissLaterAsync(TimeSpan after, CancellationToken token)
    {
        try
        {
            await Task.Delay(after, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        DismissHighlight();
    }

    private void CancelHighlightTimer()
    {
        _highlightTimer?.Cancel();
        _highlightTimer?.Dispose();
        _highlightTimer = null;
    }

    private Task BeginAsync(TourDefinition tour, string? user, int start, TutorProgress? saved)
    {
        CancelHighlightTimer();
        Highlight = null;
        _user = user;
        _presenterOn = saved?.ShowPresenter ?? tour.ShowPresenter ?? options.ShowPresenter;
        return ApplyAsync(Engine.Start(tour, start));
    }

    // A tela primeiro, o progresso depois: no Server cada gravação no localStorage é uma ida ao
    // navegador, e o passo não deve esperar por ela. O finally garante o render mesmo se um handler
    // do app lançar; sem ele o motor já mudou e o host fica com o fundo escuro e sem balão.
    private async Task ApplyAsync(IReadOnlyList<TutorEvent> events)
    {
        try
        {
            foreach (var e in events)
                EventRaised?.Invoke(e);
        }
        finally
        {
            Changed?.Invoke();
        }
        foreach (var e in events)
            await SaveProgressAsync(e);
    }

    // Progresso é conveniência: localStorage bloqueado (aba privada, cota) ou banco fora do ar não
    // pode travar o tour. Falha vira log e Read = false — quem decide o que fazer é quem chamou.
    private async Task<(bool Read, TutorProgress? Progress)> LoadAsync(string tourId, string user)
    {
        try
        {
            return (true, await store.GetAsync(tourId, user));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Tutor: não foi possível ler o progresso do tour {TourId}.", tourId);
            return (false, null);
        }
    }

    private async Task SaveProgressAsync(TutorEvent e)
    {
        if (_user is null)
            return;
        TourStatus? status = e.Kind switch
        {
            TutorEventKind.StepShown => TourStatus.InProgress,
            TutorEventKind.Completed => TourStatus.Completed,
            TutorEventKind.Skipped => TourStatus.Skipped,
            _ => null,
        };
        if (status is { } s)
            await SaveAsync(s, e.StepIndex);
    }

    private async Task SaveAsync(TourStatus status, int step)
    {
        if (_user is null)
            return;
        var tour = Engine.Tour!;
        bool? presenter = (tour.Presenter ?? options.Presenter) is null ? null : _presenterOn;
        try
        {
            await store.SaveAsync(new TutorProgress(tour.Id, _user, tour.Version, status, step, DateTimeOffset.UtcNow, presenter));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Tutor: não foi possível salvar o progresso do tour {TourId}.", tour.Id);
        }
    }
}
