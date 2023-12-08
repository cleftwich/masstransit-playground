using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace masstransit_playground;

/// <summary>
/// From my understanding, a state (implements SagaStateMachineInstance) can be thought of as a single
/// record in a database.
/// </summary>
public class OrderState : SagaStateMachineInstance
{
    /// <summary>
    /// Required property. Used to correlate instances to the Sage State Machine. Essentially the GUID and
    /// primary key of the instance.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// The current state. This can be represented as a <see cref="State"/> object, a string, or an integer.
    /// Each have their drawbacks. I'm following an example, but for more information: https://masstransit.io/documentation/patterns/saga/state-machine#instance
    /// </summary>
    public string CurrentState { get; set; }

    /// <summary>
    /// An example of another field. Intended to mark when the instance is accepted.
    /// </summary>
    public DateTime? DateAccepted { get; set; }

    /// <summary>
    /// An example of another field. Intended to mark when the instance is finalized.
    /// </summary>
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

    public State Finalized { get; private set; } = null!;
    
    public State Accepted { get; private set; } = null!;

    public Event<SubmitOrder> OnSubmitted { get; }

    public Event<AcceptOrder> OnAccepted { get; }

    public Event<FinalizeOrder> OnFinalized { get; }

    public OrderStateMachine()
    {
        // How we define the valid states if we were using an int for CurrentState
        // InstanceState(x => x.CurrentState, Submitted, Accepted);

        // Setting instance state for a CurrentState whose value is a string
        InstanceState(x => x.CurrentState);

        // Declaring OnSubmit as an event
        Event(() => OnSubmitted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OnAccepted, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OnFinalized, x => x.CorrelateById(context => context.Message.OrderId));

        // Tell the saga where to store the current state
        Initially(
            When(OnSubmitted)
                .TransitionTo(Submitted)
        );

        During(Submitted,
            Ignore(OnSubmitted),
            When(OnAccepted)
                .Then(context =>
                {
                    // Set the Accepted date if it hasn't been already
                    if (!context.Saga.DateAccepted.HasValue)
                    {
                        context.Saga.DateAccepted = context.Message.DateAccepted;
                    }
                })
                .TransitionTo(Accepted),
            Ignore(OnFinalized)
        );

        During(Accepted,
            // We cannot submit when it has already been accepted.
            Ignore(OnSubmitted),
            Ignore(OnAccepted),
            When(OnFinalized)
                .Then((context) =>
                {
                    TestContext.WriteLine("Finalizing...");
                    context.Saga.DateFinalized = context.Message.DateFinalized;
                })
                // .TransitionTo(Finalized)
                .Finalize()
        );

        SetCompletedWhenFinalized();
    }

}