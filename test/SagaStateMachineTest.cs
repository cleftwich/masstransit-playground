using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace masstransit_playground.test;

public class SagaStateMachineTest
{
    private IServiceCollection _serviceCollection;

    [Test]
    public async Task CompleteExampleSagaStateMachineTest()
    {
        _serviceCollection = new ServiceCollection();

        // Add our services, if we had any.
        // _serviceCollection.AddSingleton<>();

        _serviceCollection.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddSagaStateMachine<OrderStateMachine, OrderState>()
                .InMemoryRepository();
        });

        await using var provider = _serviceCollection.BuildServiceProvider(true);

        // Fetch the MassTransit Test Harness
        var testHarness = provider.GetRequiredService<ITestHarness>();
        testHarness.TestTimeout = TimeSpan.FromSeconds(5);

        try
        {
            // Start the test harness
            await testHarness.Start();

            var sagaId = Guid.NewGuid();

            await testHarness.Bus.Publish(new SubmitOrder
            {
                OrderId = sagaId
            });

            Assert.That(await testHarness.Consumed.Any<SubmitOrder>(), "The submit order request was not recieved by the harness");

            var sagaHarness = testHarness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();
            Assert.That(await sagaHarness.Consumed.Any<SubmitOrder>(), "The submit order request was not received by the saga harness.");

            // Check that the saga was created
            Assert.That(await sagaHarness.Created.Any(x => x.CorrelationId == sagaId), "A new item was not created with the given sagaId.");

            // Check for a saga in the Submitted state with the given sagaId. Seems like a better way to check the last assert?
            var instance = sagaHarness.Created.ContainsInState(sagaId, sagaHarness.StateMachine, sagaHarness.StateMachine.Submitted);

            Assert.IsNotNull(instance, "Saga instance not found");

            // Move the saga to Accept. Set the date to Christmas in 2012. **********************************************************************
            var christmas_2012 = new DateTime(2012, 12, 25);
            await testHarness.Bus.Publish(new AcceptOrder
            {
                OrderId = sagaId,
                DateAccepted = christmas_2012
            });

            // Check that the harness and saga harness got the accept request
            Assert.That(await testHarness.Consumed.Any<AcceptOrder>(), "The accept order request was not recieved by the harness");
            Assert.That(await sagaHarness.Consumed.Any<AcceptOrder>(), "The accept order request was not received by the saga harness.");

            // Check the state of the saga
            instance = sagaHarness.Created.ContainsInState(sagaId, sagaHarness.StateMachine, sagaHarness.StateMachine.Submitted);
            Assert.IsNull(instance, "Saga instance should not be in the submitted state!");
            instance = sagaHarness.Created.ContainsInState(sagaId, sagaHarness.StateMachine, sagaHarness.StateMachine.Accepted);
            Assert.IsNotNull(instance, "Saga instance not found");

            Assert.That(instance.DateAccepted, Is.EqualTo(christmas_2012));

            // Finalize the saga on new years eve **********************************************************************
            var new_years_eve_2012 = new DateTime(2012, 12, 31);
            await testHarness.Bus.Publish(new FinalizeOrder
            {
                OrderId = sagaId,
                DateAccepted = christmas_2012,
                DateFinalized = new_years_eve_2012
            });

            // Check that the harness and saga harness got the finalize request
            Assert.That(await testHarness.Consumed.Any<FinalizeOrder>(), "The finalize order request was not recieved by the harness");
            Assert.That(await sagaHarness.Consumed.Any<FinalizeOrder>(), "The finalize order request was not received by the saga harness.");

            // Check the state of the saga
            instance = sagaHarness.Created.ContainsInState(sagaId, sagaHarness.StateMachine, sagaHarness.StateMachine.Submitted);
            Assert.IsNull(instance, "Saga instance should not be in the submitted state!");
            instance = sagaHarness.Created.ContainsInState(sagaId, sagaHarness.StateMachine, sagaHarness.StateMachine.Accepted);
            Assert.IsNull(instance, "Saga instance should not be in the accepted state!");
            instance = sagaHarness.Created.ContainsInState(sagaId, sagaHarness.StateMachine, sagaHarness.StateMachine.Final);
            Assert.IsNotNull(instance, "Saga instance should be finalized!");

            Assert.That(instance.DateAccepted, Is.EqualTo(christmas_2012));
            Assert.That(instance.DateFinalized, Is.EqualTo(new_years_eve_2012));
        }
        finally
        {
            await testHarness.OutputTimeline(Console.Out, x => x.Now());
        }
    }
}