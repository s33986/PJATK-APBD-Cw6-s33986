using System.Text;
using Microsoft.Data.SqlClient;
using PJATK_APBD_Cw6_s33986.DTOs;
using PJATK_APBD_Cw6_s33986.Exceptions;

namespace PJATK_APBD_Cw6_s33986.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IConfiguration _configuration;

    public AppointmentService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName, CancellationToken cancellationToken)
    {
        var result = new List<AppointmentListDto>();

        var sqlCommand = new StringBuilder("""
                                           SELECT 
                                               a.IdAppointment, 
                                               a.AppointmentDate, 
                                               a.Status, 
                                               a.Reason, 
                                               p.FirstName + N' ' + p.LastName AS PatientFullName, 
                                               p.Email AS PatientEmail
                                           FROM dbo.Appointments a
                                           JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                                           """);
        var conditions = new List<string>();
        var parameters = new List<SqlParameter>();

        if (status is not null)
        {
            conditions.Add("a.status = @Status");
            parameters.Add(new SqlParameter("@Status", status));
        }

        if (patientLastName is not null)
        {
            conditions.Add("p.lastname = @PatientLastName");
            parameters.Add(new SqlParameter("@PatientLastName", patientLastName));
        }

        if (parameters.Count > 0)
        {
            sqlCommand.Append(" WHERE ");
            sqlCommand.Append(string.Join(" AND ", conditions));
        }

        sqlCommand.Append(" ORDER BY a.AppointmentDate");

        await using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        command.Connection = connection;
        command.CommandText = sqlCommand.ToString();
        if (parameters.Count > 0)
        {
            command.Parameters.AddRange(parameters.ToArray());
        }

        await connection.OpenAsync(cancellationToken);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = await reader.IsDBNullAsync(3, cancellationToken) ? string.Empty : reader.GetString(3),
                PatientFullName = reader.GetString(4),
                PatientEmail = reader.GetString(5)
            });
        }

        return result;
    }
    
    public async Task<AppointmentDetailsDto> GetAppointmentDetailsAsync(int appointmentId, CancellationToken cancellationToken)
    {
        AppointmentDetailsDto? result = null;

        var sqlCommand = new StringBuilder("""
                                           select p.Email, p.PhoneNumber, s.Name, a.InternalNotes, a.CreatedAt from Appointments a
                                           join Patients p on a.IdPatient = p.IdPatient
                                           join Doctors d on a.IdDoctor = d.IdDoctor
                                           join Specializations s on d.IdSpecialization = s.IdSpecialization
                                           where a.IdAppointment = @IdAppointment
                                           """);
        
        
        await using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        command.Connection = connection;
        command.CommandText = sqlCommand.ToString();
        command.Parameters.AddWithValue("@IdAppointment", appointmentId);
        
        
        await connection.OpenAsync(cancellationToken);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result ??= new AppointmentDetailsDto
            {
                Email = reader.GetString(0),
                PhoneNumber = reader.GetString(1),
                SpetializationName = reader.GetString(2),
                InternalNotes =  await reader.IsDBNullAsync(3, cancellationToken) ? string.Empty : reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
            };
        }

        if (result is null)
        {
            throw new NotFoundException($"Appointment {appointmentId} not found");
        }

        return result;
    }
}