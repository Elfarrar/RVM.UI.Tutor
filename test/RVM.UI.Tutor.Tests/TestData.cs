namespace RVM.UI.Tutor.Tests;

internal static class TestData
{
    public static TourDefinition Tour(int steps = 3, int version = 1, string id = "tour") =>
        new(id, version, [.. Enumerable.Range(1, steps).Select(i => new TourStep($"alvo-{i}", $"Passo {i}", $"Texto {i}"))]);

    public static readonly TourPresenter Aly = new("Aly", "/aly.png");

    /// <summary>Passo 1 com pose e áudio, passo 2 sem nada, passo 3 só com áudio.</summary>
    public static TourDefinition Narrated(TourPresenter? presenter = null) =>
        new("narrado", 1,
        [
            new TourStep("alvo-1", "Passo 1", "Texto 1", Pose: "/aly-explica.png", Audio: "/p1.mp3"),
            new TourStep("alvo-2", "Passo 2", "Texto 2"),
            new TourStep("alvo-3", "Passo 3", "Texto 3", Audio: "/p3.mp3"),
        ])
        { Presenter = presenter };
}

/// <summary>Store em memória: guarda o que foi salvo, para o teste conferir.</summary>
internal sealed class FakeProgressStore : ITutorProgressStore
{
    public Dictionary<(string Tour, string User), TutorProgress> Saved { get; } = [];
    public int Saves { get; private set; }

    public Task<TutorProgress?> GetAsync(string tourId, string user) =>
        Task.FromResult(Saved.TryGetValue((tourId, user), out var p) ? p : null);

    public Task SaveAsync(TutorProgress progress)
    {
        Saves++;
        Saved[(progress.TourId, progress.User)] = progress;
        return Task.CompletedTask;
    }

    public void Seed(string tourId, string user, int version, TourStatus status, int lastStep) =>
        Saved[(tourId, user)] = new TutorProgress(tourId, user, version, status, lastStep, DateTimeOffset.UtcNow);
}
