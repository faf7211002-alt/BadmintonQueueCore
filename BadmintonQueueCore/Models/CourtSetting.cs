namespace BadmintonQueueCore.Models
{
    public class CourtSetting
    {
        public int Id { get; set; }

        public string CourtA { get; set; } = "A場";

        public string CourtB { get; set; } = "B場";

        public string CourtC { get; set; } = "C場";
    }
}