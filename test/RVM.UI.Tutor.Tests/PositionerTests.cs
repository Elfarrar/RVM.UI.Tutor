using RVM.UI.Tutor.Internal;

namespace RVM.UI.Tutor.Tests;

public class PositionerTests
{
    private const double W = 1280, H = 720, PopW = 300, PopH = 150, Gap = 12;

    private static PopoverPosition At(Box? target, Placement preferred = Placement.Auto, double vw = W, double vh = H) =>
        Positioner.Compute(target, PopW, PopH, vw, vh, preferred, Gap);

    [Fact]
    public void Sem_alvo_centraliza()
    {
        var p = At(null);
        Assert.True(p.Centered);
        Assert.Equal((W - PopW) / 2, p.Left);
        Assert.Equal((H - PopH) / 2, p.Top);
    }

    [Fact]
    public void Auto_prefere_baixo_e_centraliza_no_alvo()
    {
        var p = At(new Box(500, 100, 100, 40));
        Assert.False(p.Centered);
        Assert.Equal(Placement.Bottom, p.Side);
        Assert.Equal(152, p.Top);
        Assert.Equal(400, p.Left);
        Assert.Equal(150, p.ArrowOffset);
    }

    [Fact]
    public void Sem_espaco_embaixo_vai_para_cima()
    {
        var p = At(new Box(500, 600, 100, 40));
        Assert.Equal(Placement.Top, p.Side);
        Assert.Equal(600 - Gap - PopH, p.Top);
    }

    [Fact]
    public void Alvo_alto_demais_vai_para_a_direita_e_depois_esquerda()
    {
        var tall = At(new Box(100, 10, 100, 700));
        Assert.Equal(Placement.Right, tall.Side);
        Assert.Equal(212, tall.Left);

        var tallAtRight = At(new Box(1000, 10, 200, 700));
        Assert.Equal(Placement.Left, tallAtRight.Side);
        Assert.Equal(1000 - Gap - PopW, tallAtRight.Left);
    }

    [Theory]
    [InlineData(Placement.Top)]
    [InlineData(Placement.Left)]
    [InlineData(Placement.Right)]
    [InlineData(Placement.Bottom)]
    public void Preferido_que_cabe_vence(Placement preferred)
    {
        var p = At(new Box(500, 300, 100, 40), preferred);
        Assert.Equal(preferred, p.Side);
    }

    [Fact]
    public void Preferido_que_nao_cabe_cai_na_ordem_padrao()
    {
        var p = At(new Box(10, 300, 100, 40), Placement.Left);
        Assert.Equal(Placement.Bottom, p.Side);
    }

    [Fact]
    public void Nada_cabe_centraliza()
    {
        var p = At(new Box(0, 0, 1280, 720));
        Assert.True(p.Centered);
    }

    [Fact]
    public void Balao_fica_dentro_da_tela_e_a_seta_presa_no_balao()
    {
        var p = At(new Box(0, 100, 20, 20));
        Assert.Equal(Positioner.Margin, p.Left);
        Assert.Equal(Positioner.ArrowInset, p.ArrowOffset);

        var side = At(new Box(100, 0, 100, 20), Placement.Right);
        Assert.Equal(Positioner.Margin, side.Top);
    }

    [Fact]
    public void Balao_maior_que_a_tela_nao_lanca()
    {
        var p = Positioner.Compute(new Box(10, 10, 10, 10), 2000, 2000, 600, 300, Placement.Auto, Gap);
        Assert.True(p.Centered);
        Assert.Equal(Positioner.Margin, p.Left);
        Assert.Equal(Positioner.Margin, p.Top);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tela_estreita_vira_faixa_inferior_com_ou_sem_alvo(bool withTarget)
    {
        var p = At(withTarget ? new Box(10, 10, 50, 20) : null, vw: 375, vh: 667);
        Assert.True(p.Sheet);
        Assert.False(p.Centered);
    }

    [Fact]
    public void A_partir_de_480_volta_ao_balao_ancorado()
    {
        Assert.False(At(new Box(10, 10, 50, 20), vw: Positioner.SheetBreakpoint).Sheet);
    }

    [Fact]
    public void Box_inflate_cresce_para_todos_os_lados()
    {
        var b = new Box(10, 20, 30, 40).Inflate(5);
        Assert.Equal(new Box(5, 15, 40, 50), b);
        Assert.Equal(45, b.Right);
        Assert.Equal(65, b.Bottom);
    }

    // Balão abaixo de um alvo no alto, à esquerda: os dois cantos de baixo ficam livres.
    private static readonly PopoverPosition TopLeftBalloon = new(false, Placement.Bottom, 152, 10, 150);

    private static Box? PresenterAt(Box? hole, PopoverPosition balloon, double vw = W, double vh = H) =>
        Positioner.Presenter(hole, balloon, PopW, PopH, vw, vh);

    [Fact]
    public void Apresentador_fica_no_canto_inferior_direito_apoiado_no_rodape()
    {
        var box = PresenterAt(new Box(92, 92, 136, 56), TopLeftBalloon);
        Assert.Equal(new Box(W - 200 - Positioner.Margin, H - 300, 200, 300), box);
    }

    [Fact]
    public void Apresentador_vai_para_a_esquerda_quando_o_alvo_esta_no_canto_direito()
    {
        var box = PresenterAt(new Box(1100, 600, 100, 40), new PopoverPosition(false, Placement.Top, 438, 900, 150));
        Assert.Equal(Positioner.Margin, box!.Value.X);
    }

    [Fact]
    public void Apresentador_some_quando_os_dois_cantos_estao_ocupados()
    {
        var box = PresenterAt(new Box(1100, 600, 100, 40), new PopoverPosition(false, Placement.Top, 560, 20, 150));
        Assert.Null(box);
    }

    [Fact]
    public void Apresentador_some_na_faixa_inferior_e_em_janela_baixa()
    {
        Assert.Null(PresenterAt(null, new PopoverPosition(false, Placement.Bottom, 0, 0, 0, Sheet: true), vw: 375, vh: 667));
        Assert.Null(PresenterAt(null, TopLeftBalloon, vh: 300));
    }

    [Fact]
    public void Apresentador_encolhe_com_a_janela()
    {
        var box = PresenterAt(null, TopLeftBalloon, vh: 400);
        Assert.Equal(180, box!.Value.Height);
        Assert.Equal(120, box.Value.Width);
    }

    [Fact]
    public void Apresentador_grande_usa_a_altura_e_a_fracao_configuradas()
    {
        var box = Positioner.Presenter(null, TopLeftBalloon, PopW, PopH, W, H, maxHeight: 750, viewportRatio: 0.8);
        Assert.Equal(H * 0.8, box!.Value.Height);
        Assert.Equal(H - H * 0.8, box.Value.Y);
    }

    // Balão centralizado (passo sem alvo) cobre os dois cantos no tamanho cheio: encolhe até um caber, não some (#60).
    [Fact]
    public void Apresentador_grande_encolhe_ate_caber_ao_lado_do_balao()
    {
        const double vh = 953, popW = 440, popH = 200;
        var centered = new PopoverPosition(true, Placement.Auto, (vh - popH) / 2, (W - popW) / 2, 0);
        var box = Positioner.Presenter(null, centered, popW, popH, W, vh, maxHeight: 750, viewportRatio: 0.8);
        Assert.NotNull(box);
        Assert.True(box!.Value.Height < 750);
        Assert.True(box.Value.Height >= Positioner.PresenterMinHeight);
        Assert.False(box.Value.Intersects(new Box(centered.Left, centered.Top, popW, popH)));
    }
}
