using System;
using System.Collections.Generic;
using Game.Contracts;

namespace Game.Foundation
{
    public sealed class EventBus : IEventBus
    {
        private readonly Action<Exception> reportException;
        private readonly Dictionary<Type, ISubscriptionSet> subscriptionsByType =
            new Dictionary<Type, ISubscriptionSet>();
        private readonly Dictionary<long, ISubscriptionSet> subscriptionsById =
            new Dictionary<long, ISubscriptionSet>();

        private long nextSubscriptionId;

        public EventBus(Action<Exception> reportException)
        {
            this.reportException = reportException ?? throw new ArgumentNullException(nameof(reportException));
        }

        public SubscriptionToken Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var messageType = typeof(T);
            if (!subscriptionsByType.TryGetValue(messageType, out var untypedSet))
            {
                untypedSet = new SubscriptionSet<T>();
                subscriptionsByType.Add(messageType, untypedSet);
            }

            var subscriptionId = AllocateSubscriptionId();
            var subscriptionSet = (SubscriptionSet<T>)untypedSet;
            subscriptionSet.Add(subscriptionId, handler);
            subscriptionsById.Add(subscriptionId, subscriptionSet);

            return new SubscriptionToken(this, subscriptionId);
        }

        public void Publish<T>(T message)
        {
            if (!subscriptionsByType.TryGetValue(typeof(T), out var untypedSet))
            {
                return;
            }

            ((SubscriptionSet<T>)untypedSet).Publish(message, ReportHandlerException);
        }

        public void Unsubscribe(SubscriptionToken token)
        {
            if (!token.IsValid || !token.BelongsTo(this))
            {
                return;
            }

            var subscriptionId = token.SubscriptionId;
            if (!subscriptionsById.TryGetValue(subscriptionId, out var subscriptionSet))
            {
                return;
            }

            if (!subscriptionSet.Remove(subscriptionId))
            {
                return;
            }

            subscriptionsById.Remove(subscriptionId);
            if (subscriptionSet.IsEmpty)
            {
                subscriptionsByType.Remove(subscriptionSet.MessageType);
            }
        }

        private long AllocateSubscriptionId()
        {
            if (nextSubscriptionId == long.MaxValue)
            {
                throw new InvalidOperationException("EventBus subscription ID space is exhausted.");
            }

            nextSubscriptionId++;
            return nextSubscriptionId;
        }

        private void ReportHandlerException(Exception exception)
        {
            try
            {
                reportException(exception);
            }
            catch
            {
                // 诊断路径不能阻断当前发布快照中的后续处理器。
            }
        }

        private interface ISubscriptionSet
        {
            Type MessageType { get; }
            bool IsEmpty { get; }
            bool Remove(long subscriptionId);
        }

        private sealed class SubscriptionSet<T> : ISubscriptionSet
        {
            private readonly List<Subscription> subscriptions = new List<Subscription>();

            public Type MessageType => typeof(T);
            public bool IsEmpty => subscriptions.Count == 0;

            public void Add(long subscriptionId, Action<T> handler)
            {
                subscriptions.Add(new Subscription(subscriptionId, handler));
            }

            public bool Remove(long subscriptionId)
            {
                for (var index = 0; index < subscriptions.Count; index++)
                {
                    if (subscriptions[index].Id != subscriptionId)
                    {
                        continue;
                    }

                    subscriptions.RemoveAt(index);
                    return true;
                }

                return false;
            }

            public void Publish(T message, Action<Exception> reportHandlerException)
            {
                var snapshot = new Action<T>[subscriptions.Count];
                for (var index = 0; index < subscriptions.Count; index++)
                {
                    snapshot[index] = subscriptions[index].Handler;
                }

                for (var index = 0; index < snapshot.Length; index++)
                {
                    try
                    {
                        snapshot[index](message);
                    }
                    catch (Exception exception)
                    {
                        reportHandlerException(exception);
                    }
                }
            }

            private readonly struct Subscription
            {
                public Subscription(long id, Action<T> handler)
                {
                    Id = id;
                    Handler = handler;
                }

                public long Id { get; }
                public Action<T> Handler { get; }
            }
        }
    }
}
