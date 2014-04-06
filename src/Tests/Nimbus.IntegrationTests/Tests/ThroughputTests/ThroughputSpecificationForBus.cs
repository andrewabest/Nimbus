﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Nimbus.Configuration;
using Nimbus.Infrastructure;
using Nimbus.IntegrationTests.Tests.ThroughputTests.EventHandlers;
using Nimbus.IntegrationTests.Tests.ThroughputTests.Infrastructure;
using Nimbus.Logger;
using NUnit.Framework;
using Shouldly;

namespace Nimbus.IntegrationTests.Tests.ThroughputTests
{
    [TestFixture]
    [Explicit("We pay $$ for messages when we're hitting the Azure Message Bus. Let's not run these on CI builds.")]
    [Timeout(60*1000)]
    public abstract class ThroughputSpecificationForBus : SpecificationFor<Bus>
    {
        private TimeSpan _timeout;
        private AssemblyScanningTypeProvider _typeProvider;

        private FakeHandlerFactory _handlerFactory;
        private FakeHandler _fakeHandler;
        private Stopwatch _stopwatch;
        private double _messagesPerSecond;
        private ILogger _logger;

        protected virtual int NumMessagesToSend
        {
            get { return 4000; }
        }

        protected abstract int ExpectedMessagesPerSecond { get; }

        public override async Task<Bus> Given()
        {
            _fakeHandler = new FakeHandler(NumMessagesToSend);
            _handlerFactory = new FakeHandlerFactory(_fakeHandler);
            _timeout = TimeSpan.FromSeconds(300); //FIXME set to 30 seconds
            _typeProvider = new TestHarnessTypeProvider(new[] {GetType().Assembly}, new[] {GetType().Namespace});
            //_logger = new ConsoleLogger();    // useful for debugging but it fills up the test runner with way too much output
            _logger = new NullLogger();

            var bus = new BusBuilder().Configure()
                                      .WithNames("ThroughputTestSuite", Environment.MachineName)
                                      .WithLogger(_logger)
                                      .WithConnectionString(CommonResources.ConnectionString)
                                      .WithTypesFrom(_typeProvider)
                                      .WithCommandHandlerFactory(_handlerFactory)
                                      .WithRequestHandlerFactory(_handlerFactory)
                                      .WithMulticastRequestHandlerFactory(_handlerFactory)
                                      .WithMulticastEventHandlerFactory(_handlerFactory)
                                      .WithCompetingEventHandlerFactory(_handlerFactory)
                                      .WithDebugOptions(
                                          dc =>
                                              dc.RemoveAllExistingNamespaceElementsOnStartup(
                                                  "I understand this will delete EVERYTHING in my namespace. I promise to only use this for test suites."))
                                      .Build();
            bus.Start();
            return bus;
        }

        public override async Task When()
        {
            Console.WriteLine("Starting to send messages...");
            _stopwatch = Stopwatch.StartNew();

            await Task.WhenAll(SendMessages(Subject));

            Console.WriteLine();
            Console.WriteLine("Finished sending messages. Waiting for them to all find their way back...");
            _fakeHandler.WaitUntilDone(_timeout);
            _stopwatch.Stop();

            Console.WriteLine("All done. Took {0} milliseconds to process {1} messages", _stopwatch.ElapsedMilliseconds, NumMessagesToSend);
            _messagesPerSecond = _fakeHandler.ActualNumMessagesReceived/_stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine("Average throughput: {0} messages/second", _messagesPerSecond);
        }

        public abstract IEnumerable<Task> SendMessages(IBus bus);

        [Test]
        public async Task TheCorrectNumberOfMessagesShouldHaveBeenObserved()
        {
            Subject = await Given();
            await When();

            _fakeHandler.ActualNumMessagesReceived.ShouldBe(_fakeHandler.ExpectedNumMessagesReceived);
        }

        [Test]
        public async Task WeShouldGetAcceptableThroughput()
        {
            Subject = await Given();
            await When();

            _messagesPerSecond.ShouldBeGreaterThan(ExpectedMessagesPerSecond);
        }

        [TearDown]
        public override void TearDown()
        {
            Subject.Stop();
            _handlerFactory = null;
        }
    }
}