using Microsoft.AspNetCore.Components;

namespace RVM.UI.Tutor;

/// <summary>Um tour: id estável, versão e passos. Escrito em C# pelo app (ADR-004).</summary>
/// <param name="Id">Identificador estável; é a chave do progresso.</param>
/// <param name="Version">Versão do roteiro. Subir a versão faz o tour reaparecer (ver <see cref="ShowAgainOnNewVersion"/>).</param>
/// <param name="Steps">Passos, na ordem. Pelo menos um.</param>
public sealed record TourDefinition(string Id, int Version, IReadOnlyList<TourStep> Steps)
{
    /// <summary>Quem viu uma versão anterior vê de novo quando a versão sobe. Padrão: <c>true</c>.</summary>
    public bool ShowAgainOnNewVersion { get; init; } = true;

    /// <summary>Apresentador deste tour; <c>null</c> = o de <see cref="TutorOptions.Presenter"/>.</summary>
    public TourPresenter? Presenter { get; init; }

    /// <summary>Se o apresentador começa visível neste tour; <c>null</c> = <see cref="TutorOptions.ShowPresenter"/>.</summary>
    public bool? ShowPresenter { get; init; }
}

/// <summary>Um passo do tour.</summary>
/// <param name="Target">Valor do atributo <c>data-tour</c> do elemento em foco. <c>null</c> = balão centralizado, sem recorte.</param>
/// <param name="Title">Título do balão.</param>
/// <param name="Text">Texto do balão.</param>
/// <param name="Placement">Lado preferido do balão; se não couber, o host tenta os outros.</param>
/// <param name="WhenTargetMissing">O que fazer se o alvo não estiver na tela.</param>
/// <param name="Content">Conteúdo extra, abaixo do texto.</param>
/// <param name="Route">Página do passo (ex. <c>/lancamentos</c>). Se o usuário estiver em outra, o tour navega e continua lá.</param>
/// <param name="Advance">Como o passo avança: botão "Próximo", clique no alvo ou campo preenchido.</param>
/// <param name="WaitForTargetMs">Quanto esperar o alvo aparecer (tela que carrega depois) antes de aplicar <paramref name="WhenTargetMissing"/>.</param>
/// <param name="Pose">Imagem do apresentador neste passo; <c>null</c> = a padrão (<see cref="TourPresenter.Image"/>).</param>
/// <param name="Audio">URL do áudio narrado no passo (mp3), no idioma do texto. Toca só depois de um gesto do usuário na página.</param>
/// <param name="PoseAnimated">Versão animada de <paramref name="Pose"/> (WebM com alfa, ou imagem animada), opcional.
/// Aparece só enquanto a voz do passo toca; quem pede menos movimento vê a parada.
/// Passo com <paramref name="Pose"/> e sem esta fica parado (não herda a animação da imagem padrão).</param>
public sealed record TourStep(
    string? Target,
    string Title,
    string Text,
    Placement Placement = Placement.Auto,
    MissingTargetPolicy WhenTargetMissing = MissingTargetPolicy.Center,
    RenderFragment? Content = null,
    string? Route = null,
    AdvanceMode Advance = AdvanceMode.Button,
    int WaitForTargetMs = 3000,
    string? Pose = null,
    string? Audio = null,
    string? PoseAnimated = null);

/// <summary>Lado do balão em relação ao alvo.</summary>
public enum Placement { Auto, Top, Bottom, Left, Right }

/// <summary>Como um passo avança.</summary>
public enum AdvanceMode
{
    /// <summary>Botão "Próximo".</summary>
    Button,
    /// <summary>Clique no alvo; o clique segue normal para o app. O "Próximo" some.</summary>
    ClickTarget,
    /// <summary>Campo do alvo preenchido (ao sair do campo ou Enter). O "Próximo" some.</summary>
    InputFilled,
}

/// <summary>Política para alvo ausente.</summary>
public enum MissingTargetPolicy
{
    /// <summary>Mostra o balão centralizado, sem recorte.</summary>
    Center,
    /// <summary>Pula o passo.</summary>
    Skip,
    /// <summary>Fecha o tour (o progresso guarda o passo, para retomar).</summary>
    End,
}
