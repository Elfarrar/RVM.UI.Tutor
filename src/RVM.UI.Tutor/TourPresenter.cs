namespace RVM.UI.Tutor;

/// <summary>
/// Personagem que apresenta o tour: uma imagem no canto da tela, ao lado do balão, e a voz dos passos
/// que têm <see cref="TourStep.Audio"/>. O usuário liga e desliga no balão. A personagem vem do app
/// (ou da pele do RVM.UI); a biblioteca não traz nenhuma.
/// </summary>
/// <param name="Name">Nome mostrado nos botões ("Ocultar Aly").</param>
/// <param name="Image">URL da imagem padrão: retrato 2:3, fundo transparente. O passo troca com <see cref="TourStep.Pose"/>.</param>
/// <param name="ImageAnimated">Versão animada da imagem padrão (WebM com alfa, ou WebP/GIF/SVG animado), opcional.
/// Aparece só enquanto a voz do passo toca; quem pede menos movimento no sistema (<c>prefers-reduced-motion</c>)
/// vê <paramref name="Image"/>.</param>
public sealed record TourPresenter(string Name, string Image, string? ImageAnimated = null);
