using System.Text.Json;
using Microsoft.JSInterop;

namespace RVM.UI.Tutor;

/// <summary>
/// Progresso no <c>localStorage</c> do navegador: vale por aparelho. Os apps do ecossistema trocam
/// por uma implementação no banco, para o progresso seguir o usuário (ADR-003).
/// </summary>
public sealed class LocalStorageTutorProgressStore(IJSRuntime js) : ITutorProgressStore
{
    internal const string KeyPrefix = "rvm-tutor:";

    /// <inheritdoc />
    public async Task<TutorProgress?> GetAsync(string tourId, string user)
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", Key(tourId, user));
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<TutorProgress>(json);
        }
        catch (JsonException)
        {
            // Valor de outra versão ou editado à mão: tratar como nunca visto, em vez de quebrar a tela.
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(TutorProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        await js.InvokeVoidAsync("localStorage.setItem", Key(progress.TourId, progress.User),
            JsonSerializer.Serialize(progress));
    }

    internal static string Key(string tourId, string user) => $"{KeyPrefix}{tourId}:{user}";
}
