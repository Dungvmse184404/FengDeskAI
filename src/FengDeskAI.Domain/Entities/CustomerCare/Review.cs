using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FengDeskAI.Domain.Entities.CustomerCare
{
    public class Review : BaseEntity
    {
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
        public Guid ProductId { get; set; }
        public virtual Product Product { get; set; } = null!;

    }
}
