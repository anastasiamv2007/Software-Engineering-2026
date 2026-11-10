using System;

namespace SoftwareEngineering.DAL.Entities
{
    public class SubtaskEntity
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
    }
}
