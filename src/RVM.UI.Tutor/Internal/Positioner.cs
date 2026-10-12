namespace RVM.UI.Tutor.Internal;

internal readonly record struct Box(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public Box Inflate(double by) => new(X - by, Y - by, Width + 2 * by, Height + 2 * by);
    public bool Intersects(Box other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}

/// <summary>
/// Onde o balão fica. <see cref="Centered"/> = sem seta, no meio da tela. <see cref="Sheet"/> = faixa
/// inferior da tela estreita (o CSS posiciona; topo e esquerda não se aplicam).
/// </summary>
internal readonly record struct PopoverPosition(bool Centered, Placement Side, double Top, double Left, double ArrowOffset, bool Sheet = false);

/// <summary>
/// Escolhe o lado do balão (06-motor-do-tour.md § Posicionamento): o preferido se couber; senão
/// baixo → cima → direita → esquerda; se nada couber, centralizado (o recorte continua).
/// Coordenadas da viewport, as mesmas do <c>getBoundingClientRect</c>.
/// </summary>
internal static class Positioner
{
    public const double Margin = 8;

    /// <summary>Abaixo desta largura o balão vira faixa inferior (RNF06). O tutor.js usa o mesmo valor ao rolar.</summary>
    public const double SheetBreakpoint = 480;
    public const double ArrowInset = 16;
    private static readonly Placement[] FallbackOrder = [Placement.Bottom, Placement.Top, Placement.Right, Placement.Left];

    public static PopoverPosition Compute(Box? target, double popWidth, double popHeight,
        double viewportWidth, double viewportHeight, Placement preferred, double gap)
    {
        if (viewportWidth < SheetBreakpoint)
            return new(false, Placement.Bottom, 0, 0, 0, Sheet: true);
        if (target is not { } t)
            return Center(popWidth, popHeight, viewportWidth, viewportHeight);

        IEnumerable<Placement> order = preferred == Placement.Auto ? FallbackOrder : [preferred, .. FallbackOrder];
        foreach (var side in order)
        {
            if (Fits(side, t, popWidth, popHeight, viewportWidth, viewportHeight, gap))
                return Place(side, t, popWidth, popHeight, viewportWidth, viewportHeight, gap);
        }
        return Center(popWidth, popHeight, viewportWidth, viewportHeight);
    }

    private static bool Fits(Placement side, Box t, double w, double h, double vw, double vh, double gap) => side switch
    {
        Placement.Bottom => t.Bottom + gap + h <= vh - Margin && w <= vw - 2 * Margin,
        Placement.Top => t.Y - gap - h >= Margin && w <= vw - 2 * Margin,
        Placement.Right => t.Right + gap + w <= vw - Margin && h <= vh - 2 * Margin,
        Placement.Left => t.X - gap - w >= Margin && h <= vh - 2 * Margin,
        _ => false,
    };

    private static PopoverPosition Place(Placement side, Box t, double w, double h, double vw, double vh, double gap)
    {
        var centerX = t.X + t.Width / 2;
        var centerY = t.Y + t.Height / 2;
        if (side is Placement.Bottom or Placement.Top)
        {
            var left = Clamp(centerX - w / 2, Margin, vw - w - Margin);
            var top = side == Placement.Bottom ? t.Bottom + gap : t.Y - gap - h;
            return new(false, side, top, left, Clamp(centerX - left, ArrowInset, w - ArrowInset));
        }
        else
        {
            var top = Clamp(centerY - h / 2, Margin, vh - h - Margin);
            var left = side == Placement.Right ? t.Right + gap : t.X - gap - w;
            return new(false, side, top, left, Clamp(centerY - top, ArrowInset, h - ArrowInset));
        }
    }

    /// <summary>Altura máxima padrão do apresentador (<see cref="TutorOptions.PresenterMaxHeight"/>).</summary>
    public const double PresenterMaxHeight = 300;

    /// <summary>Fração padrão da altura da janela que o apresentador pode ocupar (<see cref="TutorOptions.PresenterViewportRatio"/>).</summary>
    public const double PresenterViewportRatio = 0.45;

    /// <summary>Abaixo disto o apresentador fica pequeno demais para ajudar: some.</summary>
    public const double PresenterMinHeight = 140;

    // Degrau do encolhimento quando o tamanho cheio não cabe em canto nenhum.
    private const double PresenterShrink = 0.9;

    /// <summary>
    /// Onde fica o apresentador (retrato 2:3, apoiado no rodapé): canto inferior direito, senão o esquerdo,
    /// o primeiro que não cobre o recorte nem o balão. Sem canto livre no tamanho cheio, encolhe até caber
    /// (apresentador grande, #60). <c>null</c> = nenhum cabe nem no mínimo, ou faixa inferior.
    /// </summary>
    public static Box? Presenter(Box? hole, PopoverPosition popover, double popWidth, double popHeight,
        double viewportWidth, double viewportHeight,
        double maxHeight = PresenterMaxHeight, double viewportRatio = PresenterViewportRatio)
    {
        if (popover.Sheet)
            return null;
        var balloon = new Box(popover.Left, popover.Top, popWidth, popHeight);
        for (var height = Math.Min(maxHeight, viewportHeight * viewportRatio); height >= PresenterMinHeight; height *= PresenterShrink)
        {
            var width = height * 2 / 3;
            Box[] corners =
            [
                new(viewportWidth - width - Margin, viewportHeight - height, width, height),
                new(Margin, viewportHeight - height, width, height),
            ];
            foreach (var corner in corners)
            {
                if (!corner.Intersects(balloon) && !(hole is { } h && corner.Intersects(h)))
                    return corner;
            }
        }
        return null;
    }

    private static PopoverPosition Center(double w, double h, double vw, double vh) =>
        new(true, Placement.Auto, Math.Max(Margin, (vh - h) / 2), Math.Max(Margin, (vw - w) / 2), 0);

    // Math.Clamp lança quando min > max (balão maior que o espaço); aí vale o mínimo.
    private static double Clamp(double value, double min, double max) => max < min ? min : Math.Clamp(value, min, max);
}
