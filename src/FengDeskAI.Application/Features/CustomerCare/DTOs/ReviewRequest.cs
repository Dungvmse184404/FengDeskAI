using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace FengDeskAI.Application.Features.CustomerCare.DTOs
{

    public class CreateReviewRequest
    {
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public Guid UserId { get; set; }

        public Guid ProductId { get; set; }
    }

    public class UpdateReviewRequest
    {
        public string Content { get; set; } = string.Empty;

        public int Rating { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class DeleteReviewRequest
    {
        public Guid ReviewId { get; set; }
    }

}

