using Microsoft.AspNetCore.Mvc;
using PJATK_APBD_Cw6_s33986.Exceptions;
using PJATK_APBD_Cw6_s33986.Services;

namespace PJATK_APBD_Cw6_s33986.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController(IAppointmentService service) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> GetAppointments([FromQuery] string? status, [FromQuery] string? patientLastName,
        CancellationToken cancellationToken)
    {
        return Ok(await service.GetAppointmentsAsync(status, patientLastName, cancellationToken));
    }

    [HttpGet("{appointmentId:int}")]
    public async Task<IActionResult> GetAppointment([FromRoute] int appointmentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await service.GetAppointmentDetailsAsync(appointmentId, cancellationToken));
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
    
}