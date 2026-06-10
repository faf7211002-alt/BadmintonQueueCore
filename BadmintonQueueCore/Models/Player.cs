using System.ComponentModel.DataAnnotations;

namespace BadmintonQueueCore.Models
{
    public class Player
    {
        [Key]
        public int Id { get; set; }

        public string PlayerName { get; set; } = "";

        public int QueueNo { get; set; }

        public string Status { get; set; } = "Waiting";

        public int CourtNo { get; set; }

        public int GroupNo { get; set; }
    }
}