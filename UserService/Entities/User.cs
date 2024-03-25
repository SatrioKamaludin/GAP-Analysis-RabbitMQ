namespace UserService.Entities
{
    public class User
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Mail { get; set; }
        public required string OtherData { get; set; }
    }
}
