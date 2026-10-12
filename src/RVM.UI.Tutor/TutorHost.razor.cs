using System.ComponentModel;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using RVM.UI.Tutor.Internal;

namespace RVM.UI.Tutor;

/// <summary>
/// Desenha o tour e o destaque: overlay SVG com recorte no alvo e o balão. Vai UMA vez no layout,
/// em modo interativo, para sobreviver à troca de página.
/// </summary>
public partial class TutorHost : IAsyncDisposable
{
    private const string ModulePath = "./_content/RVM.UI.Tutor/tutor.js";
    private const double Gap = 12;
    // Espera curta (alvo que já está na página) não pisca o aviso de "procurando".
    private static readonly TimeSpan WaitingDelay = TimeSpan.FromMilliseconds(400);

    private enum HostMode { None, Tour, Highlight }

    private readonly string _id = "tutor-" + Guid.NewGuid().ToString("N")[..8];
    private IJSObjectReference? _module;
    private IJSObjectReference? _js;
    private DotNetObjectReference<TutorHost>? _self;
    private ElementReference _popover, _waitingBox;
    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    private HostMode _mode;
    private object? _shown;
    private bool _locatePending, _measurePending, _focusPending, _deactivatePending, _awaitingRoute, _actionArmed;
    private bool _focusAfterMeasure = true;
    private bool _waiting, _waitingFocusPending, _disposed;
    private int _waitingEpoch; // muda a cada passo: timer de um passo anterior não mostra o aviso
    private object? _spoken;
    private Box? _target, _presenterBox;
    private (string Still, string? Animated)? _presenterPose;
    private string? _presenterTour; // tour da pose guardada: outro tour por cima não mostra a do anterior
    private double _viewportWidth, _viewportHeight, _popoverWidth, _popoverHeight;
    private PopoverPosition? _position;

    [Inject] private TutorService Tutor { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private string MaskId => _id + "-mask";
    private string TitleId => _id + "-title";
    private string TextId => _id + "-text";
    private string WaitingId => _id + "-waiting";

    private bool ShowPopover => _mode switch
    {
        HostMode.Tour => Tutor.Engine.State == TutorState.Showing,
        HostMode.Highlight => !_locatePending,
        _ => false,
    };

    private Box? Hole => _target is { } t && !(_mode == HostMode.Tour && Tutor.Engine.Centered)
        ? t.Inflate(Tutor.Options.Padding)
        : null;

    private string PopoverClass => _position switch
    {
        null => "tutor-popover",
        { Sheet: true } => "tutor-popover tutor-popover--sheet",
        { Centered: true } => "tutor-popover tutor-popover--centered",
        { } p => "tutor-popover tutor-popover--" + p.Side.ToString().ToLowerInvariant(),
    };

    // Antes de medir, o balão é desenhado invisível para o JS saber o tamanho dele.
    private string PopoverStyle => _position switch
    {
        null => "top:0;left:0;visibility:hidden",
        { Sheet: true } => "",
        { } p => $"top:{Px(p.Top)}px;left:{Px(p.Left)}px",
    };

    private bool ShowWaiting => _waiting && _mode == HostMode.Tour && Tutor.Engine.State == TutorState.Preparing;

    // Tela estreita: a mesma faixa inferior do balão, para o aviso não pular para o meio da tela.
    private string WaitingClass => _viewportWidth is > 0 and < Positioner.SheetBreakpoint
        ? "tutor-popover tutor-popover--sheet tutor-waiting"
        : "tutor-popover tutor-waiting tutor-waiting--center";

    private bool ShowArrow => _position is { Centered: false, Sheet: false };

    // Entre um passo e outro (Preparing) o apresentador fica com a pose e o lugar do passo anterior:
    // sair do DOM e voltar refaria a animação de entrada a cada passo (#49).
    private bool ShowPresenter => _mode == HostMode.Tour && Tutor.PresenterShown &&
        Tutor.Engine.State is TutorState.Showing or TutorState.Preparing;

    private bool ShowMute => Tutor.CurrentStep?.Audio is not null && Tutor.CanSpeak;

    // Passo que espera clique/campo: sem "Próximo". Volta se o alvo sumiu (centralizado) ou se a ação
    // não pode acontecer (botão desabilitado, campo já preenchido): o usuário nunca fica preso.
    private bool WaitsForAction => Tutor.WaitsForAction && _actionArmed;
    private bool ShowNext => !WaitsForAction;

    // Fora do recorte o fundo bloqueia o clique; dentro, o clique chega ao alvo (passo ClickTarget).
    private IEnumerable<Box> Blockers
    {
        get
        {
            const double Far = 100_000;
            if (Hole is not { } h)
            {
                yield return new Box(0, 0, Far, Far);
                yield break;
            }
            yield return new Box(0, 0, Far, Math.Max(0, h.Y));
            yield return new Box(0, h.Bottom, Far, Far);
            yield return new Box(0, h.Y, Math.Max(0, h.X), h.Height);
            yield return new Box(h.Right, h.Y, Far, h.Height);
        }
    }

    private string ArrowStyle => _position is { Side: Placement.Top or Placement.Bottom } p
        ? $"left:{Px(p.ArrowOffset)}px"
        : $"top:{Px(_position?.ArrowOffset ?? 0)}px";

    protected override void OnInitialized()
    {
        _culture = TutorTexts.CultureFor(Tutor.Options);
        Tutor.Changed += OnTutorChanged;
        Navigation.LocationChanged += OnLocationChanged;
        Sync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _self = DotNetObjectReference.Create(this);
            _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _js = await _module.InvokeAsync<IJSObjectReference>("createHost", _self);
        }
        if (_js is null)
            return;

        if (_deactivatePending)
        {
            // Sempre, não só depois do foco: o locate já ligou observers (fim por política End,
            // Skip/Close em Preparing, destaque dispensado antes de focar).
            _deactivatePending = false;
            await _js.InvokeVoidAsync("deactivate");
        }

        await SyncAudioAsync();

        if (_locatePending)
        {
            _locatePending = false;
            var step = _mode == HostMode.Tour ? Tutor.CurrentStep : null;
            if (step?.Route is { } route && !IsAt(route))
            {
                // Passo em outra página: navega e continua no LocationChanged (o host vive no layout).
                // Teclado ligado antes (ESC funciona durante a espera) e prazo, se a navegação não vier.
                _awaitingRoute = true;
                _ = ShowWaitingLaterAsync(_waitingEpoch);
                await _js.InvokeVoidAsync("arm");
                _ = GiveUpOnRouteAsync(_shown, step.WaitForTargetMs);
                Navigation.NavigateTo(route.TrimStart('/'));
                return;
            }

            var shown = _shown;
            var name = step is not null ? step.Target : Tutor.Highlight?.Target;
            if (step is { Target: not null, WaitForTargetMs: > 0 })
                _ = ShowWaitingLaterAsync(_waitingEpoch);
            var layout = await _js.InvokeAsync<LayoutChange>("locate", name,
                step?.WaitForTargetMs ?? 0, (step?.Advance ?? AdvanceMode.Button).ToString());
            if (!Equals(shown, _shown))
                return; // o usuário saiu desse passo enquanto o alvo era esperado
            ApplyLayout(layout);
            _actionArmed = layout.ActionArmed;
            if (_mode == HostMode.Tour)
            {
                // O motor decide (mostrar, centralizar, pular ou encerrar); o Changed traz o próximo render.
                await Tutor.ReportTargetAsync(_target is not null);
            }
            else
            {
                _measurePending = true;
                StateHasChanged();
            }
            return;
        }

        if (_waitingFocusPending && ShowWaiting)
        {
            _waitingFocusPending = false;
            await _js.InvokeVoidAsync("focus", _waitingBox, false, false);
            return;
        }

        if (_measurePending && ShowPopover)
        {
            _measurePending = false;
            var size = await _js.InvokeAsync<SizeInfo>("measure", _popover);
            _popoverWidth = size.Width;
            _popoverHeight = size.Height;
            Reposition();
            // Remedido por um botão do próprio balão: o foco fica nele, não pula para o "Próximo".
            _focusPending = _focusAfterMeasure;
            _focusAfterMeasure = true;
            StateHasChanged();
            return;
        }

        if (_focusPending && ShowPopover)
        {
            _focusPending = false;
            await _js.InvokeVoidAsync("focus", _popover, WaitsForAction, _position is { Sheet: true });
        }
    }

    // Toca o áudio do passo uma vez por passo mostrado; para ao trocar de passo, ocultar o
    // apresentador, silenciar ou fechar. Religar/reativar o som toca o passo de novo.
    private async Task SyncAudioAsync()
    {
        var audio = ShowPopover && _mode == HostMode.Tour ? Tutor.StepAudio : null;
        object? spoken = audio is null ? null : (_shown, audio);
        if (Equals(spoken, _spoken))
            return;
        _spoken = spoken;
        await _js!.InvokeVoidAsync("speak", audio);
    }

    /// <summary>Chamado pelo <c>tutor.js</c> quando o alvo ou a janela mudou (no máximo uma vez por frame).</summary>
    [JSInvokable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void OnLayoutChanged(LayoutChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (_mode == HostMode.None)
            return;
        // Janela mudou de tamanho: o balão pode ter quebrado linha (max-width em vw), medir de novo.
        if (ShowPopover && (change.ViewportWidth != _viewportWidth || change.ViewportHeight != _viewportHeight))
            _measurePending = true;
        ApplyLayout(change);
        Reposition();
        StateHasChanged();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (!_awaitingRoute)
            return;
        // Qualquer chegada encerra a espera: se a rota redirecionou, a política de alvo ausente decide.
        _awaitingRoute = false;
        _locatePending = true;
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task ShowWaitingLaterAsync(int epoch)
    {
        await Task.Delay(WaitingDelay);
        if (_disposed)
            return;
        await InvokeAsync(() =>
        {
            if (_disposed || epoch != _waitingEpoch || _waiting || Tutor.Engine.State != TutorState.Preparing)
                return;
            _waiting = _waitingFocusPending = true;
            StateHasChanged();
        });
    }

    private async Task GiveUpOnRouteAsync(object? shown, int afterMs)
    {
        await Task.Delay(Math.Max(afterMs, 1000));
        if (_disposed)
            return;
        await InvokeAsync(() =>
        {
            if (_disposed || !_awaitingRoute || !Equals(shown, _shown))
                return;
            _awaitingRoute = false;
            _locatePending = true;
            StateHasChanged();
        });
    }

    private bool IsAt(string route)
    {
        static string PathOf(string value) => "/" + value.Split('?', '#')[0].Trim('/');
        try
        {
            return string.Equals(PathOf(Navigation.ToBaseRelativePath(Navigation.Uri)), PathOf(route), StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false; // URI fora do base path do app
        }
    }

    private void OnTutorChanged() => _ = InvokeAsync(() =>
    {
        Sync();
        StateHasChanged();
    });

    // Traduz o estado do serviço em trabalho para o próximo OnAfterRender: localizar, medir, focar.
    private void Sync()
    {
        var engine = Tutor.Engine;
        var mode = engine.IsActive ? HostMode.Tour : Tutor.Highlight is not null ? HostMode.Highlight : HostMode.None;
        object? shown = mode switch
        {
            HostMode.Tour => (engine.Tour!.Id, engine.Index, engine.State),
            HostMode.Highlight => Tutor.Highlight,
            _ => null,
        };
        if (mode == HostMode.None && _mode != HostMode.None)
            _deactivatePending = true;
        _mode = mode;
        if (Equals(shown, _shown))
            return;

        _shown = shown;
        _position = null;
        _popoverWidth = _popoverHeight = 0;
        _locatePending = mode == HostMode.Highlight || (mode == HostMode.Tour && engine.State == TutorState.Preparing);
        _measurePending = mode == HostMode.Tour && engine.State == TutorState.Showing;
        _focusPending = false;
        _focusAfterMeasure = true;
        if (mode != HostMode.Tour || engine.Tour!.Id != _presenterTour)
        {
            _presenterBox = null;
            _presenterPose = null;
        }
        _awaitingRoute = false;
        _waitingEpoch++;
        // Dois passos pulados seguidos: o aviso continua, sem sumir e voltar entre um e outro.
        _waiting = _waiting && mode == HostMode.Tour && engine.State == TutorState.Preparing;
        _waitingFocusPending = false;
        if (_locatePending)
            _actionArmed = false; // o próximo locate diz se a ação do passo novo pode acontecer
        // Passo novo: o recorte do anterior não pode ficar (nem deixar clicar nele) durante a espera.
        if (mode == HostMode.None || engine.State == TutorState.Preparing)
            _target = null;
    }

    private void ApplyLayout(LayoutChange layout)
    {
        _viewportWidth = layout.ViewportWidth;
        _viewportHeight = layout.ViewportHeight;
        _target = layout.Found ? new Box(layout.X, layout.Y, layout.Width, layout.Height) : null;
    }

    private void Reposition()
    {
        if (_popoverWidth <= 0)
            return;
        var preferred = _mode == HostMode.Tour ? Tutor.CurrentStep?.Placement ?? Placement.Auto : Placement.Auto;
        _position = Positioner.Compute(Hole, _popoverWidth, _popoverHeight, _viewportWidth, _viewportHeight, preferred, Gap);
        _presenterBox = _mode == HostMode.Tour && Tutor.PresenterShown
            ? Positioner.Presenter(Hole, _position.Value, _popoverWidth, _popoverHeight, _viewportWidth, _viewportHeight)
            : null;
        // Parada e animada vêm em par: o passo com pose própria não herda a animação da imagem padrão.
        _presenterTour = Tutor.Engine.Tour?.Id;
        _presenterPose = _presenterBox is null ? null
            : Tutor.CurrentStep!.Pose is { } pose ? (pose, Tutor.CurrentStep.PoseAnimated)
            : (Tutor.Presenter!.Image, Tutor.Presenter.ImageAnimated);
    }

    /// <summary>O usuário clicou no alvo ou preencheu o campo do passo (vem do <c>tutor.js</c>).</summary>
    [JSInvokable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task OnTargetAction() => WaitsForAction ? Tutor.TargetActionAsync() : Task.CompletedTask;

    /// <summary>Tecla do tour (ESC, setas), vinda do listener do <c>tutor.js</c> no documento.</summary>
    [JSInvokable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task OnKey(string key)
    {
        if (_mode == HostMode.Highlight)
        {
            if (key == "Escape")
                Tutor.DismissHighlight();
            return;
        }
        switch (key)
        {
            case "Escape":
                await (Tutor.Options.AllowSkip ? Tutor.SkipAsync() : Tutor.CloseAsync());
                break;
            case "ArrowRight" when ShowNext:
                await Tutor.NextAsync();
                break;
            case "ArrowLeft":
                await Tutor.BackAsync();
                break;
        }
    }

    // Os botões mudam de texto (e o "Silenciar" some com o apresentador): mede o balão de novo.
    private async Task OnTogglePresenter()
    {
        await Tutor.TogglePresenterAsync();
        RemeasureKeepingFocus();
    }

    private void OnToggleMute()
    {
        Tutor.ToggleMute();
        RemeasureKeepingFocus();
    }

    private void RemeasureKeepingFocus()
    {
        _measurePending = true;
        _focusAfterMeasure = false;
    }

    private async Task OnCloseClick()
    {
        if (_mode == HostMode.Highlight)
            Tutor.DismissHighlight();
        else
            await Tutor.CloseAsync();
    }

    private async Task OnOverlayClick()
    {
        if (_mode == HostMode.Highlight)
            Tutor.DismissHighlight();
        else if (Tutor.Options.CloseOnOverlayClick)
            await Tutor.CloseAsync();
    }

    private string Text(string key) => TutorTexts.Get(key, _culture);

    private static string Px(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        Tutor.Changed -= OnTutorChanged;
        Navigation.LocationChanged -= OnLocationChanged;
        try
        {
            if (_js is not null)
            {
                await _js.InvokeVoidAsync("deactivate");
                await _js.DisposeAsync();
            }
            if (_module is not null)
                await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuito já caiu (Server): não há navegador para limpar.
        }
        _self?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Retângulo do alvo e tamanho da janela, vindos do <c>tutor.js</c>. Uso interno.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed record LayoutChange(bool Found, double X, double Y, double Width, double Height, double ViewportWidth, double ViewportHeight, bool ActionArmed = false);

internal sealed record SizeInfo(double Width, double Height);
