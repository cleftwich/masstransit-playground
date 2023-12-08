using MassTransit;

namespace masstransit_playground;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; }

    public DateTime? DateAccepted { get; set; }

    public DateTime? DateFinalized { get; set; }
}

public record SubmitOrder
{
    public Guid OrderId { get; init; }
}

public record AcceptOrder
{
    public Guid OrderId { get; init; }

    public DateTime DateAccepted { get; init; }
}

public record FinalizeOrder
{
    public Guid OrderId { get; init; }

    public DateTime DateAccepted { get; init; }

    public DateTime DateFinalized { get; init; }

}

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State Submitted { get; private set; } = null!;

    public State Accepted { get; private set; } = null!;

    public Event<SubmitOrder> OnSubmitted { get; }

    public Event<AcceptOrder> OnAccepted { get; }

    public Event<FinalizeOrder> OnFinalized { get; }

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OnSubmitted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OnAccepted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OnFinalized, x => x.CorrelateById(context => context.Message.OrderId));

        Initially(
            When(OnSubmitted)
                .TransitionTo(Submitted)
        );

        During(Submitted,
            When(OnAccepted)
                .Then(context =>
                {
                    if (!context.Saga.DateAccepted.HasValue)
                    {
                        context.Saga.DateAccepted = context.Message.DateAccepted;
                    }
                })
                .TransitionTo(Accepted)
        );

        During(Accepted,
            When(OnFinalized)
                .Then((context) =>
                {
                    context.Saga.DateFinalized = context.Message.DateFinalized;
                })
                .Finalize()
        );
    }

}