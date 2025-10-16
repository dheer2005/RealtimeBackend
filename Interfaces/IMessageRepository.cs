using RealtimeChat.Models;

namespace RealtimeChat.Interfaces
{
    public interface IMessageRepository
    {
        Task<Messages?> UpdateStatusAsync(int id, string status);

    }
}
