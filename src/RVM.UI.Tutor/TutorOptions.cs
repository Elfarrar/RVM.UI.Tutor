namespace RVM.UI.Tutor;

/// <summary>Opções globais do Tutor, definidas em <c>AddRvmTutor</c>.</summary>
public sealed class TutorOptions
{
    /// <summary>Idioma dos textos da biblioteca (<c>pt-BR</c>, <c>en</c>). <c>null</c> = cultura de UI corrente.</summary>
    public string? Language { get; set; }

    /// <summary>Mostra "Pular" e deixa o ESC pular o tour. Sem isso, o ESC só fecha.</summary>
    public bool AllowSkip { get; set; } = true;

    /// <summary>Clicar no fundo escurecido fecha o tour.</summary>
    public bool CloseOnOverlayClick { get; set; }

    /// <summary>Mostra o progresso ("2 de 7").</summary>
    public bool ShowProgress { get; set; } = true;

    /// <summary>Folga, em px, entre o alvo e a borda do recorte.</summary>
    public double Padding { get; set; } = 8;

    /// <summary>Raio, em px, dos cantos do recorte.</summary>
    public double Radius { get; set; } = 8;

    /// <summary>Apresentador de todos os tours; o tour troca com <see cref="TourDefinition.Presenter"/>. <c>null</c> = sem apresentador.</summary>
    public TourPresenter? Presenter { get; set; }

    /// <summary>O apresentador começa visível. O usuário muda no balão, e a escolha fica no progresso do tour.</summary>
    public bool ShowPresenter { get; set; } = true;
}
