using Microsoft.EntityFrameworkCore;
using RealtimeChat.Context;
using RealtimeChat.Interfaces;
using RealtimeChat.Models;

namespace RealtimeChat.Services
{
    public class MessageRepository : IMessageRepository
    {
        private readonly ChatDbContext _dbContext;
        public MessageRepository(ChatDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<Messages?> UpdateStatusAsync(int id, string status)
        {
            var message = await _dbContext.Messages.FindAsync(id);
            if (message == null) return null;

            message.Status = status;
            await _dbContext.SaveChangesAsync();

            return message;
        }

    }
}
