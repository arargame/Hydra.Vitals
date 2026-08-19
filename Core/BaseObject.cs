using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Hydra.Vitals.Core
{
    public interface IHasId
    {
        Guid Id { get; set; }
    }

    public interface IHasAuditFields
    {
        DateTime AddedDate { get; set; }
        DateTime? ModifiedDate { get; set; }
        byte[]? RowVersion { get; set; }
    }

    public interface IBaseObject<T> : IHasId, IHasAuditFields where T : IBaseObject<T>
    {
        string? Name { get; set; }
        string? Description { get; set; }
        bool IsActive { get; set; }
    }

    public abstract class BaseObject<T> : IBaseObject<T> where T : BaseObject<T>
    {
        public Guid Id { get; set; }

        public string? Name { get; set; } = null;

        public string? Description { get; set; } = null;

        public DateTime AddedDate { get; set; }

        public DateTime? ModifiedDate { get; set; } = null;

        public bool IsActive { get; set; } = true;

        [Timestamp]
        [JsonIgnore]
        public byte[]? RowVersion { get; set; } = null;

        [NotMapped]
        [JsonIgnore]
        public bool IsPersistent { get; set; } = true;

        protected BaseObject()
        {
            Initialize();
        }

        public virtual void Initialize()
        {
            Id = Guid.CreateVersion7();
            AddedDate = DateTime.UtcNow;
            ModifiedDate = DateTime.UtcNow;
        }

        public virtual T SetName(string? name)
        {
            Name = name;
            ModifiedDate = DateTime.UtcNow;
            return (T)this;
        }

        public virtual T SetDescription(string? description)
        {
            Description = description;
            ModifiedDate = DateTime.UtcNow;
            return (T)this;
        }

        public virtual string UniqueProperty => Id.ToString();

        public override string ToString()
        {
            return $"{Id}/{Name}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            var other = (BaseObject<T>)obj;
            return UniqueProperty == other.UniqueProperty;
        }

        public override int GetHashCode()
        {
            return UniqueProperty?.GetHashCode() ?? 0;
        }
    }
}
