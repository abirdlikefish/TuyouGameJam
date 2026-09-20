using System;

namespace Game.Contracts
{
    public interface IEventBus
    {
        SubscriptionToken Subscribe<T>(Action<T> handler);
        void Publish<T>(T message);
        void Unsubscribe(SubscriptionToken token);
    }
}
