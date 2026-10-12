namespace RVM.UI.Tutor;

/// <summary>Controla tours e destaques. Precisa de um <see cref="TutorHost"/> no layout para aparecer.</summary>
public interface ITutor
{
    /// <summary>Há um tour aberto.</summary>
    bool IsActive { get; }

    /// <summary>Passo atual, ou <c>null</c> sem tour aberto.</summary>
    TourStep? CurrentStep { get; }

    /// <summary>Iniciado, passo mostrado, concluído, pulado, fechado, alvo ausente.</summary>
    event Action<TutorEvent>? EventRaised;

    /// <summary>
    /// Abre o tour. Com <paramref name="user"/>, retoma do último passo se ele ficou em andamento
    /// nesta versão, a menos que <paramref name="force"/> (que começa do primeiro).
    /// </summary>
    Task StartAsync(TourDefinition tour, string? user = null, bool force = false);

    /// <summary>
    /// Abre do começo só se o usuário nunca viu esta versão (atalho para o "1º login"). Tour fechado no
    /// meio conta como visto: não reabre sozinho — o usuário retoma pelo <see cref="StartAsync"/>
    /// (ex. menu "Tutoriais"). Se o progresso não puder ser lido (store fora do ar), não abre: na
    /// dúvida, não interrompe o usuário. Devolve <c>true</c> se abriu.
    /// </summary>
    Task<bool> StartIfNeverSeenAsync(TourDefinition tour, string user);

    /// <summary>Próximo passo; no último, conclui.</summary>
    Task NextAsync();

    /// <summary>Passo anterior.</summary>
    Task BackAsync();

    /// <summary>Pula o tour (não volta a abrir sozinho).</summary>
    Task SkipAsync();

    /// <summary>Fecha o tour guardando o passo, para retomar.</summary>
    Task CloseAsync();

    /// <summary>Destaca um elemento com um texto curto, sem tour. Ignorado se houver tour aberto.</summary>
    /// <param name="target">Valor do <c>data-tour</c>.</param>
    /// <param name="text">Texto do balão.</param>
    /// <param name="duration">Some sozinho depois disso; <c>null</c> = só no clique.</param>
    Task HighlightAsync(string target, string text, TimeSpan? duration = null);
}
