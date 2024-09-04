using System.Text.Json.Serialization;

namespace PostService.Models
{
    public class User
    {
        public int ID { get; set; }
        public required string Name { get; set; }

        [JsonIgnore]
        public ICollection<Post> Posts { get; set; } = new List<Post>();
    }
}
