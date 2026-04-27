using PJATK_APBD_Cw6_s33986.DTOs;

namespace PJATK_APBD_Cw6_s33986.Services;

public interface IAppointmentService
{
    Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName, CancellationToken cancellationToken);
    Task<AppointmentDetailsDto> GetAppointmentDetailsAsync(int appointmentId, CancellationToken cancellationToken);
}