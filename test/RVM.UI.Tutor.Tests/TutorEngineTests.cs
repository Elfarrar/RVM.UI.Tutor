using RVM.UI.Tutor.Internal;

namespace RVM.UI.Tutor.Tests;

// 06-motor-do-tour.md: todo par (estado, evento) tem teste. Estados: Idle, Preparing, Showing,
// Completed, Skipped. Eventos: Start, TargetResolved(achou/não achou x política), Next, Back, Skip, Close.
public class TutorEngineTests
{
    private static TutorEngine InState(TutorState state, int steps = 3, int index = 0)
    {
        var engine = new TutorEngine();
        if (state == TutorState.Idle)
            return engine;
        engine.Start(TestData.Tour(steps), index);
        switch (state)
        {
            case TutorState.Showing:
                engine.TargetResolved(true);
                break;
            case TutorState.Completed:
                engine.Start(TestData.Tour(steps), steps - 1);
                engine.TargetResolved(true);
                engine.Next();
                break;
            case TutorState.Skipped:
                engine.Skip();
                break;
        }
        Assert.Equal(state, engine.State);
        return engine;
    }

    private static IEnumerable<TutorEventKind> Kinds(IReadOnlyList<TutorEvent> events) => events.Select(e => e.Kind);

    // ---- Start ----

    [Theory]
    [InlineData(TutorState.Idle)]
    [InlineData(TutorState.Preparing)]
    [InlineData(TutorState.Showing)]
    [InlineData(TutorState.Completed)]
    [InlineData(TutorState.Skipped)]
    public void Start_em_qualquer_estado_prepara_o_passo_pedido(object fromState)
    {
        var from = (TutorState)fromState;
        var engine = InState(from);
        var events = engine.Start(TestData.Tour(id: "outro"), startIndex: 1);

        Assert.Equal(TutorState.Preparing, engine.State);
        Assert.Equal(1, engine.Index);
        Assert.Equal([TutorEventKind.Started], Kinds(events));
        Assert.Equal("outro", events[0].TourId);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(99, 2)]
    public void Start_prende_o_indice_dentro_do_tour(int asked, int expected)
    {
        var engine = new TutorEngine();
        engine.Start(TestData.Tour(3), asked);
        Assert.Equal(expected, engine.Index);
    }

    [Fact]
    public void Start_sem_passos_e_erro()
    {
        var engine = new TutorEngine();
        Assert.Throws<ArgumentException>(() => engine.Start(new TourDefinition("vazio", 1, [])));
        Assert.Throws<ArgumentNullException>(() => engine.Start(null!));
    }

    // ---- TargetResolved ----

    [Fact]
    public void Preparing_alvo_achado_mostra_com_recorte()
    {
        var engine = InState(TutorState.Preparing);
        var events = engine.TargetResolved(true);

        Assert.Equal(TutorState.Showing, engine.State);
        Assert.False(engine.Centered);
        Assert.Equal([TutorEventKind.StepShown], Kinds(events));
    }

    [Fact]
    public void Preparing_passo_sem_alvo_mostra_centralizado_sem_evento_de_ausencia()
    {
        var engine = new TutorEngine();
        engine.Start(new TourDefinition("t", 1, [new TourStep(null, "Boas-vindas", "Oi")]));
        var events = engine.TargetResolved(false);

        Assert.Equal(TutorState.Showing, engine.State);
        Assert.True(engine.Centered);
        Assert.Equal([TutorEventKind.StepShown], Kinds(events));
    }

    [Fact]
    public void Preparing_alvo_ausente_politica_Center_mostra_centralizado()
    {
        var engine = InState(TutorState.Preparing);
        var events = engine.TargetResolved(false);

        Assert.Equal(TutorState.Showing, engine.State);
        Assert.True(engine.Centered);
        Assert.Equal([TutorEventKind.TargetMissing, TutorEventKind.StepShown], Kinds(events));
    }

    [Fact]
    public void Preparing_alvo_ausente_politica_Skip_vai_para_o_proximo_passo()
    {
        var engine = new TutorEngine();
        engine.Start(new TourDefinition("t", 1,
        [
            new TourStep("some", "A", "a", WhenTargetMissing: MissingTargetPolicy.Skip),
            new TourStep("b", "B", "b"),
        ]));
        var events = engine.TargetResolved(false);

        Assert.Equal(TutorState.Preparing, engine.State);
        Assert.Equal(1, engine.Index);
        Assert.Equal([TutorEventKind.TargetMissing], Kinds(events));
    }

    [Fact]
    public void Preparing_alvo_ausente_politica_Skip_no_ultimo_passo_conclui()
    {
        var engine = new TutorEngine();
        engine.Start(new TourDefinition("t", 1, [new TourStep("some", "A", "a", WhenTargetMissing: MissingTargetPolicy.Skip)]));
        var events = engine.TargetResolved(false);

        Assert.Equal(TutorState.Completed, engine.State);
        Assert.Equal([TutorEventKind.TargetMissing, TutorEventKind.Completed], Kinds(events));
    }

    [Fact]
    public void Preparing_alvo_ausente_politica_End_fecha()
    {
        var engine = new TutorEngine();
        engine.Start(new TourDefinition("t", 1, [new TourStep("some", "A", "a", WhenTargetMissing: MissingTargetPolicy.End)]));
        var events = engine.TargetResolved(false);

        Assert.Equal(TutorState.Idle, engine.State);
        Assert.Equal([TutorEventKind.TargetMissing, TutorEventKind.Closed], Kinds(events));
    }

    [Theory]
    [InlineData(TutorState.Idle)]
    [InlineData(TutorState.Showing)]
    [InlineData(TutorState.Completed)]
    [InlineData(TutorState.Skipped)]
    public void TargetResolved_fora_de_Preparing_nao_faz_nada(object fromState)
    {
        var from = (TutorState)fromState;
        var engine = InState(from);
        Assert.Empty(engine.TargetResolved(true));
        Assert.Empty(engine.TargetResolved(false));
        Assert.Equal(from, engine.State);
    }

    // ---- Next ----

    [Fact]
    public void Showing_Next_no_meio_prepara_o_proximo()
    {
        var engine = InState(TutorState.Showing);
        Assert.Empty(engine.Next());
        Assert.Equal(TutorState.Preparing, engine.State);
        Assert.Equal(1, engine.Index);
    }

    [Fact]
    public void Showing_Next_no_ultimo_conclui()
    {
        var engine = InState(TutorState.Showing, steps: 2, index: 1);
        var events = engine.Next();
        Assert.Equal(TutorState.Completed, engine.State);
        Assert.Equal([TutorEventKind.Completed], Kinds(events));
        Assert.Null(engine.CurrentStep);
    }

    [Theory]
    [InlineData(TutorState.Idle)]
    [InlineData(TutorState.Preparing)]
    [InlineData(TutorState.Completed)]
    [InlineData(TutorState.Skipped)]
    public void Next_fora_de_Showing_nao_faz_nada(object fromState)
    {
        var from = (TutorState)fromState;
        var engine = InState(from);
        Assert.Empty(engine.Next());
        Assert.Equal(from, engine.State);
    }

    // ---- Back ----

    [Fact]
    public void Showing_Back_volta_um_passo()
    {
        var engine = InState(TutorState.Showing, index: 2);
        Assert.Empty(engine.Back());
        Assert.Equal(TutorState.Preparing, engine.State);
        Assert.Equal(1, engine.Index);
    }

    [Fact]
    public void Showing_Back_no_primeiro_nao_faz_nada()
    {
        var engine = InState(TutorState.Showing);
        Assert.Empty(engine.Back());
        Assert.Equal(TutorState.Showing, engine.State);
        Assert.Equal(0, engine.Index);
    }

    [Theory]
    [InlineData(TutorState.Idle)]
    [InlineData(TutorState.Preparing)]
    [InlineData(TutorState.Completed)]
    [InlineData(TutorState.Skipped)]
    public void Back_fora_de_Showing_nao_faz_nada(object fromState)
    {
        var from = (TutorState)fromState;
        var engine = InState(from, index: 0);
        Assert.Empty(engine.Back());
        Assert.Equal(from, engine.State);
    }

    // ---- Skip / Close ----

    [Theory]
    [InlineData(TutorState.Preparing)]
    [InlineData(TutorState.Showing)]
    public void Skip_com_tour_aberto_pula(object fromState)
    {
        var from = (TutorState)fromState;
        var engine = InState(from, index: 1);
        var events = engine.Skip();
        Assert.Equal(TutorState.Skipped, engine.State);
        Assert.Equal([TutorEventKind.Skipped], Kinds(events));
        Assert.Equal(1, events[0].StepIndex);
    }

    [Theory]
    [InlineData(TutorState.Idle)]
    [InlineData(TutorState.Completed)]
    [InlineData(TutorState.Skipped)]
    public void Skip_sem_tour_aberto_nao_faz_nada(object fromState)
    {
        var from = (TutorState)fromState;
        var engine = InState(from);
        Assert.Empty(engine.Skip());
        Assert.Equal(from, engine.State);
    }

    [Theory]
    [InlineData(TutorState.Preparing)]
    [InlineData(TutorState.Showing)]
    public void Close_com_tour_aberto_volta_a_Idle_guardando_o_indice(object fromState)
    {
        var from = (TutorState)fromState;
        var engine = InState(from, index: 1);
        var events = engine.Close();
        Assert.Equal(TutorState.Idle, engine.State);
        Assert.Equal(1, engine.Index);
        Assert.Equal([TutorEventKind.Closed], Kinds(events));
        Assert.False(engine.IsActive);
    }

    [Theory]
    [InlineData(TutorState.Idle)]
    [InlineData(TutorState.Completed)]
    [InlineData(TutorState.Skipped)]
    public void Close_sem_tour_aberto_nao_faz_nada(object fromState)
    {
        var from = (TutorState)fromState;
        var engine = InState(from);
        Assert.Empty(engine.Close());
        Assert.Equal(from, engine.State);
    }

    [Fact]
    public void Tour_de_3_passos_Next_x3_conclui()
    {
        var engine = new TutorEngine();
        var all = new List<TutorEvent>(engine.Start(TestData.Tour(3)));
        for (var i = 0; i < 3; i++)
        {
            all.AddRange(engine.TargetResolved(true));
            all.AddRange(engine.Next());
        }
        Assert.Equal(TutorState.Completed, engine.State);
        Assert.Equal(
            [TutorEventKind.Started, TutorEventKind.StepShown, TutorEventKind.StepShown, TutorEventKind.StepShown, TutorEventKind.Completed],
            Kinds(all));
    }
}
