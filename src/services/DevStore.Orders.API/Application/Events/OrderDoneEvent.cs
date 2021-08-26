using System;
using DevStore.Core.Messages;

namespace DevStore.Orders.API.Application.Events
{
    public class OrderDoneEvent : Event
    {
        public Guid OrderId { get; private set; }
        public Guid ClientId { get; private set; }

        public OrderDoneEvent(Guid orderId, Guid clientId)
        {
            OrderId = orderId;
            ClientId = clientId;
        }
    }
}