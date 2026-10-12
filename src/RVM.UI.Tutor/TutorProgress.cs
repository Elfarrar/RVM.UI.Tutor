namespace RVM.UI.Tutor;

/// <summary>Progresso de um usuário num tour. Quem persiste é o app (ADR-003).</summary>
/// <param name="TourId">Id do tour.</param>
/// <param name="User">Quem viu; o app decide o que é (normalmente o id do usuário).</param>
/// <param name="Version">Versão do tour vista.</param>
/// <param name="Status">Situação.</param>
/// <param name="LastStep">Índice (base 0) do último passo mostrado, para retomar.</param>
/// <param name="UpdatedAt">Quando mudou, em UTC.</param>
public sealed record TutorProgress(
    string TourId,
    string User,
    int Version,
    TourStatus Status,
    int LastStep,
    DateTimeOffset UpdatedAt);

/// <summary>Situação do tour para um usuário.</summary>
public enum TourStatus { InProgress, Completed, Skipped }

/// <summary>Onde o progresso mora. Padrão: <see cref="LocalStorageTutorProgressStore"/>.</summary>
public interface ITutorProgressStore
{
    /// <summary>Progresso salvo, ou <c>null</c> se o usuário nunca viu o tour.</summary>
    Task<TutorProgress?> GetAsync(string tourId, string user);

    /// <summary>Grava (substitui) o progresso.</summary>
    Task SaveAsync(TutorProgress progress);
}
