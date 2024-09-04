using Microsoft.EntityFrameworkCore;
using UserService.Models;

namespace UserService.Data
{
    public class UserServiceContext : DbContext
    {
        public UserServiceContext(DbContextOptions<UserServiceContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
    }
}
