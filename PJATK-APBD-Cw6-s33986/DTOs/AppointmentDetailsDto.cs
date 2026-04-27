namespace PJATK_APBD_Cw6_s33986.DTOs;

public class AppointmentDetailsDto
{
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string SpetializationName { get; set; } = string.Empty; //nazwa specjalizacji
    public string InternalNotes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}