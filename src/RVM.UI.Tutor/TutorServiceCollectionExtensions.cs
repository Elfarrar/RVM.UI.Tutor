using Microsoft.Extensions.DependencyInjection.Extensions;
using RVM.UI.Tutor;
using RVM.UI.Tutor.Internal;

// No namespace do DI, como os AddXxx do ASP.NET: o Program.cs do app não precisa de using.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registro do Tutor no DI.</summary>
public static class TutorServiceCollectionExtensions
{
    /// <summary>
    /// Registra o <see cref="ITutor"/> (por circuito/usuário) e, se o app não registrou outro, o
    /// <see cref="LocalStorageTutorProgressStore"/>.
    /// </summary>
    public static IServiceCollection AddRvmTutor(this IServiceCollection services, Action<TutorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new TutorOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.TryAddScoped<ITutorProgressStore, LocalStorageTutorProgressStore>();
        services.TryAddScoped<TutorService>();
        services.TryAddScoped<ITutor>(sp => sp.GetRequiredService<TutorService>());
        return services;
    }
}
