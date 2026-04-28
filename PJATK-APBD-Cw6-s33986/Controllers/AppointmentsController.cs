using Microsoft.AspNetCore.Mvc;
using PJATK_APBD_Cw6_s33986.DTOs;
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

    [HttpPost]
    public async Task<IActionResult> CreateAppointmentRequestAsync([FromBody] CreateAppointmentRequestDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var newAppointment = await service.CreateAppointmentAsync(dto, cancellationToken);
            return Created("api/Appointments", newAppointment);
        }
        catch (DoctorInavailableException e)
        {
            return Conflict(e.Message);
        } catch (IncorrectDateException e)
        {
            return BadRequest(e.Message);
        } catch(NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }

    [HttpPut("{appointmentId:int}")]
    public async Task<IActionResult> UpdateAppointmentRequestAsync([FromRoute] int appointmentId,
        [FromBody] UpdateAppointmentRequestDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await service.UpdateAppointmentAsync(appointmentId, dto, cancellationToken);
            return Ok(appointment);
        }
        catch (DoctorInavailableException e)
        {
            return Conflict(e.Message);
        }
        catch (IncorrectDateException e)
        {
            return BadRequest(e.Message);
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (InvalidStatusException e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpDelete("{appointmentId:int}")]
    public async Task<IActionResult> DeleteAppointmentRequestAsync([FromRoute] int appointmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.DeleteAppointmentAsync(appointmentId, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (InvalidStatusException e)
        {
            return Conflict(e.Message);
        }
    }
    
}