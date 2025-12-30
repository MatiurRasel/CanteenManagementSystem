namespace CanteenManagementSystem.Models
{
    public class OperatorSession
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime? LastActivity { get; set; }
    }
}
