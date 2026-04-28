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

    public async Task<CreateAppointmentRequestDto> CreateAppointmentAsync(CreateAppointmentRequestDto dto, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        command.Connection = connection;
        command.Transaction = (SqlTransaction)transaction;

        try
        {
            command.CommandText = """select 1 from Patients where IdPatient = @IdPatient and IsActive = 1""";
            command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
            var patientExists = await command.ExecuteScalarAsync(cancellationToken);
            
            if (patientExists is null)
            {
                throw new NotFoundException($"Patient with id: {dto.IdPatient} not found");
            }
            
            command.Parameters.Clear();
            
            command.CommandText = """select 1 from Doctors where IdDoctor = @IdDoctor and IsActive = 1""";
            command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
            var doctorExists = await command.ExecuteScalarAsync(cancellationToken);

            if (doctorExists is null)
            {
                throw new NotFoundException($"Doctor with id: {dto.IdDoctor} not found");
            }
            
            command.Parameters.Clear();

            command.CommandText =
                """select 1 from Appointments where IdDoctor = @IdDoctor and AppointmentDate = @AppointmentDate""";
            command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
            command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
            var isDoctorAvailable = await command.ExecuteScalarAsync(cancellationToken);
            
            if (isDoctorAvailable is not null)
            {
                throw new DoctorInavailableException($"Doctor with id: {dto.IdDoctor} is not available in selected date: {dto.AppointmentDate}");
            }


            command.Parameters.Clear();

            if (dto.AppointmentDate < DateTime.Now)
            {
                throw new IncorrectDateException($"Date {dto.AppointmentDate} cannot be in the past");
            }

            command.CommandText =
                """insert into Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason) values (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason)""";
            
            command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
            command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
            command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
            command.Parameters.AddWithValue("@Reason", dto.Reason);
            
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return dto;
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        
        
    }

    public async Task<UpdateAppointmentRequestDto> UpdateAppointmentAsync(int appointmentId, UpdateAppointmentRequestDto dto, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        command.Connection = connection;
        command.Transaction = (SqlTransaction)transaction;

        try
        {
            command.CommandText = """select status, appointmentDate from Appointments where IdAppointment = @IdAppointment""";
            command.Parameters.AddWithValue("@IdAppointment", appointmentId);
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            string currentStatus = string.Empty;
            DateTime currentAppointmentDate = default;
            var exists = false;
            
            while (await reader.ReadAsync(cancellationToken))
            {
                exists = true;
                currentStatus = reader.GetString(0);
                currentAppointmentDate = reader.GetDateTime(1);
            }

            await reader.CloseAsync();

            if (!exists)
            {
                throw new NotFoundException($"Appointment with id: {appointmentId} not found");
            }
            
            command.Parameters.Clear();
            
            command.CommandText = """select 1 from Patients where IdPatient = @IdPatient and IsActive = 1""";
            command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
            var patientExists = await command.ExecuteScalarAsync(cancellationToken);
            
            if (patientExists is null)
            {
                throw new NotFoundException($"Patient with id: {dto.IdPatient} not found");
            }
            
            command.Parameters.Clear();
            
            command.CommandText = """select 1 from Doctors where IdDoctor = @IdDoctor and IsActive = 1""";
            command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
            var doctorExists = await command.ExecuteScalarAsync(cancellationToken);

            if (doctorExists is null)
            {
                throw new NotFoundException($"Doctor with id: {dto.IdDoctor} not found");
            }
            
            command.Parameters.Clear();

            command.CommandText =
                """select 1 from Appointments where IdDoctor = @IdDoctor and AppointmentDate = @AppointmentDate and IdAppointment != @IdAppointment""";
            command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
            command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
            command.Parameters.AddWithValue("@IdAppointment", appointmentId);
            var isDoctorAvailable = await command.ExecuteScalarAsync(cancellationToken);
            
            if (isDoctorAvailable is not null)
            {
                throw new DoctorInavailableException($"Doctor with id: {dto.IdDoctor} is not available in selected date: {dto.AppointmentDate}");
            }
            
            command.Parameters.Clear();

            if (!dto.Status.Equals("Scheduled") && !dto.Status.Equals("Completed") && !dto.Status.Equals("Cancelled"))
            {
                throw new InvalidStatusException($"Status {dto.Status} is not valid");
            }
            
            if (dto.AppointmentDate < DateTime.Now)
            {
                throw new IncorrectDateException($"Date {dto.AppointmentDate} cannot be in the past");
            }

            command.CommandText =
                """update Appointments set IdPatient = @IdPatient, IdDoctor = @IdDoctor, AppointmentDate = @AppointmentDate, Status = @Status, Reason = @Reason, InternalNotes = @InternalNotes where IdAppointment = @IdAppointment""";
            
            command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
            command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
            if (currentStatus.Equals("Completed"))
            {
                command.Parameters.AddWithValue("@AppointmentDate", currentAppointmentDate);
            }
            else
            {
                command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
            }
            command.Parameters.AddWithValue("@Status", dto.Status);
            command.Parameters.AddWithValue("@Reason", dto.Reason);
            command.Parameters.AddWithValue("@InternalNotes", dto.InternalNotes);
            command.Parameters.AddWithValue("@IdAppointment", appointmentId);
            
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return dto;
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        
    }

    public async Task DeleteAppointmentAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();

        command.Connection = connection;
        
        await connection.OpenAsync(cancellationToken);
        
        string status = string.Empty;
        var exists = false;
        
        command.CommandText = """select status from Appointments where IdAppointment = @IdAppointment""";
        command.Parameters.AddWithValue("@IdAppointment", appointmentId);
        var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            exists = true;
            status = reader.GetString(0);
        }
        
        await reader.CloseAsync();
        
        if (!exists)
        {
            throw new NotFoundException($"Appointment with id: {appointmentId} not found");
        }

        if (status.Equals("Completed"))
        {
            throw new InvalidStatusException($"Cannot delete {status} visits");
        }
        
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        command.Transaction = (SqlTransaction)transaction;

        try
        {
            command.CommandText = """delete from appointments where IdAppointment = @IdAppointment""";
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        } catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        
    }
}