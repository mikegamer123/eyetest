using EyeExamParser.DTO;

public interface IScheduleServices
{
    Task<IReadOnlyList<ScheduleDTO>> GetSchedulesAsync();
    Task<string> VerifyAgainstExternalResultsAsync();
}