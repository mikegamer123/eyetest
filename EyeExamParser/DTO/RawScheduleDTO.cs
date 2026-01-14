namespace EyeExamParser.DTO
{
    public class RawScheduleDTO
    {
        public string EntryNumber { get; set; }
        public string EntryDate { get; set; }
        public string EntryType { get; set; }
        public List<string> EntryText { get; set; }
    }
}
