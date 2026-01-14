using EyeExamParser.DTO;

public interface IScheduleParser
{
        IEnumerable<ScheduleDTO> Parse(IEnumerable<RawScheduleDTO> raw);
}