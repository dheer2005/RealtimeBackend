using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RealtimeChat.Models;
using System.Reflection.Emit;

namespace RealtimeChat.Context
{
    public class ChatDbContext : IdentityDbContext<AppUser>
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options): base(options)
        {
            
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<SeenUpdate>().HasNoKey();

            builder.Entity<Messages>()
            .HasOne(m => m.ReplyToMessage)
            .WithMany() // no collection navigation
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(builder);
        }

        public DbSet<Messages> Messages { get; set; }
        public DbSet<Group_chats> GroupChats { get; set; }
        public DbSet<FriendRequest> FriendRequests { get; set; }

        //public DbSet<ChatGroup> ChatGroups { get; set; }
        //public DbSet<GroupMember> GroupMembers { get; set; }
        //public DbSet<GroupMessage> GroupMessages { get; set; }
    }

    
}
