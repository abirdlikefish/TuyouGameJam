namespace Game.Contracts
{
    public readonly struct SubscriptionToken
    {
        private readonly object owner;
        private readonly long subscriptionId;

        internal SubscriptionToken(object owner, long subscriptionId)
        {
            this.owner = owner;
            this.subscriptionId = subscriptionId;
        }

        public bool IsValid => owner != null && subscriptionId > 0;

        internal bool BelongsTo(object candidate)
        {
            return ReferenceEquals(owner, candidate);
        }

        internal long SubscriptionId => subscriptionId;
    }
}
