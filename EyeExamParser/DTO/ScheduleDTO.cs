using Microsoft.Extensions.Caching.Memory;

namespace EyeExamParser.DTO
{
    public class ScheduleDTO
    {
        public int EntryNumber { get; set; }
        public DateTime? EntryDate { get; set; }
        public string RegistrationDateAndPlanRef { get; set; }
        public string PropertyDescription { get; set; }
        public string DateOfLeaseAndTerm { get; set; }
        public string LesseesTitle { get; set; }
        public List<string>? Notes { get; set; }
    }
}
