using Microsoft.Identity.Client;

namespace RealtimeChat.Models
{
    public class Messages
    {
        public int Id { get; set; }
        public string FromUser { get; set; } = string.Empty;
        public string UserTo { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public string? Reactions { get; set; }
        public string Status { get; set; } = "sent";
        public bool IsImage { get; set; } = false;
        public string? MediaUrl { get; set; }

        public int? ReplyToMessageId { get; set; }
        public Messages? ReplyToMessage { get; set; }

    }
}
