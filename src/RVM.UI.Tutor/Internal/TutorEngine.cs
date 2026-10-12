namespace RVM.UI.Tutor.Internal;

internal enum TutorState { Idle, Preparing, Showing, Completed, Skipped }

/// <summary>
/// Máquina de estados do tour (06-motor-do-tour.md), C# puro: sem JS, sem render, sem progresso.
/// Cada operação devolve os eventos que causou; operação fora de hora não muda nada e devolve vazio.
/// <c>Preparing</c> = esperando o host dizer se o alvo do passo está na tela.
/// </summary>
internal sealed class TutorEngine
{
    public TutorState State { get; private set; } = TutorState.Idle;
    public TourDefinition? Tour { get; private set; }
    public int Index { get; private set; }

    /// <summary>Passo mostrado sem recorte (sem alvo, ou alvo ausente com política Center).</summary>
    public bool Centered { get; private set; }

    public bool IsActive => State is TutorState.Preparing or TutorState.Showing;
    public TourStep? CurrentStep => IsActive ? Tour!.Steps[Index] : null;
    public bool IsLastStep => Tour is not null && Index == Tour.Steps.Count - 1;

    public IReadOnlyList<TutorEvent> Start(TourDefinition tour, int startIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(tour);
        if (tour.Steps.Count == 0)
            throw new ArgumentException("O tour precisa de pelo menos um passo.", nameof(tour));

        Tour = tour;
        Index = Math.Clamp(startIndex, 0, tour.Steps.Count - 1);
        Prepare();
        return [Event(TutorEventKind.Started)];
    }

    public IReadOnlyList<TutorEvent> TargetResolved(bool found)
    {
        if (State != TutorState.Preparing)
            return [];

        var step = CurrentStep!;
        if (found || step.Target is null)
            return [Show(centered: step.Target is null)];

        var missing = Event(TutorEventKind.TargetMissing);
        return step.WhenTargetMissing switch
        {
            MissingTargetPolicy.Skip => [missing, .. Advance()],
            MissingTargetPolicy.End => [missing, .. Close()],
            _ => [missing, Show(centered: true)],
        };
    }

    public IReadOnlyList<TutorEvent> Next() => State == TutorState.Showing ? Advance() : [];

    public IReadOnlyList<TutorEvent> Back()
    {
        if (State != TutorState.Showing || Index == 0)
            return [];
        Index--;
        Prepare();
        return [];
    }

    public IReadOnlyList<TutorEvent> Skip()
    {
        if (!IsActive)
            return [];
        State = TutorState.Skipped;
        return [Event(TutorEventKind.Skipped)];
    }

    public IReadOnlyList<TutorEvent> Close()
    {
        if (!IsActive)
            return [];
        State = TutorState.Idle;
        return [Event(TutorEventKind.Closed)];
    }

    private IReadOnlyList<TutorEvent> Advance()
    {
        if (IsLastStep)
        {
            State = TutorState.Completed;
            return [Event(TutorEventKind.Completed)];
        }
        Index++;
        Prepare();
        return [];
    }

    private void Prepare()
    {
        State = TutorState.Preparing;
        Centered = false;
    }

    private TutorEvent Show(bool centered)
    {
        State = TutorState.Showing;
        Centered = centered;
        return Event(TutorEventKind.StepShown);
    }

    private TutorEvent Event(TutorEventKind kind) => new(kind, Tour!.Id, Index);
}
