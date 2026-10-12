namespace RVM.UI.Tutor;

/// <summary>O que aconteceu no tour; o app usa para registrar uso ou reagir (a biblioteca não envia nada a lugar nenhum).</summary>
/// <param name="Kind">Tipo do evento.</param>
/// <param name="TourId">Id do tour.</param>
/// <param name="StepIndex">Índice (base 0) do passo envolvido.</param>
public sealed record TutorEvent(TutorEventKind Kind, string TourId, int StepIndex);

/// <summary>Tipos de <see cref="TutorEvent"/>.</summary>
public enum TutorEventKind { Started, StepShown, Completed, Skipped, Closed, TargetMissing }
