﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nimbus.DependencyResolution;
using Nimbus.Exceptions;
using Nimbus.Extensions;
using Nimbus.Handlers;
using Nimbus.Interceptors.Inbound;
using Nimbus.Logger;

namespace Nimbus.Infrastructure.Events
{
    internal class MulticastEventMessageDispatcher : EventMessageDispatcher
    {
        public MulticastEventMessageDispatcher(IBrokeredMessageFactory brokeredMessageFactory,
                                               IClock clock,
                                               IDependencyResolver dependencyResolver,
                                               IInboundInterceptorFactory inboundInterceptorFactory,
                                               IReadOnlyDictionary<Type, Type[]> handlerMap)
            : base(brokeredMessageFactory, clock, dependencyResolver, handlerMap, inboundInterceptorFactory, new NullLogger())
        {
        }

        protected override object CreateHandlerFromScope<TBusEvent>(IDependencyResolverScope scope, TBusEvent busEvent, Type handlerType)
        {
            var handler = scope.Resolve<IHandleMulticastEvent<TBusEvent>>(handlerType.FullName);
            return handler;
        }

        protected override Task DispatchToHandleMethod<TBusEvent>(TBusEvent busEvent, object handler)
        {
            var genericHandler = (IHandleMulticastEvent<TBusEvent>) handler;
            var handlerTask = genericHandler.Handle(busEvent);
            return handlerTask;
        }
    }
}