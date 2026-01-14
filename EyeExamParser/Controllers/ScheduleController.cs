using EyeExamParser.DTO;
using Microsoft.AspNetCore.Mvc;

namespace EyeExamParser.Controllers
{
    [ApiController]
    [Route("api/schedules")]
    public class SchedulesController : ControllerBase
    {
        private readonly IScheduleServices _scheduleServices;

        public SchedulesController(IScheduleServices scheduleServices)
            => _scheduleServices = scheduleServices;

        [HttpGet]
        public async Task<IActionResult> Get()
        { 
            return Ok(await _scheduleServices.GetSchedulesAsync());
        }

        [HttpGet("AreResultsTheSame")]
        public async Task<IActionResult> Verify()
        {
            var result = await _scheduleServices.VerifyAgainstExternalResultsAsync();
            return Ok(result);
        }
    }
}
