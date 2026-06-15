using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Identity;

namespace FengDeskAI.Application.Features.CustomerCare.DTOs
{

    public class ReviewResponse
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
        public Guid ProductId { get; set; }
        public virtual Product Product { get; set; } = null!;
    }


    public class CreateReviewRespond
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid ProductId { get; set; }

    }

    public class UpdateReviewRespond
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public DateTime UpdateAt { get; set; } = DateTime.UtcNow;
    }


}
