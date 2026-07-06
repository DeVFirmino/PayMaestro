namespace PayMaestro.Domain.Entities

    // prevents invalid states setting a new guid here it generates every instance

{
    public abstract class EntityBase
    {
        public Guid Id { get; protected set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    }
}