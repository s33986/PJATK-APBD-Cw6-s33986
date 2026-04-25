namespace PJATK_APBD_Cw6_s33986.DTOs;

public class AppointmentDetailsDto
{
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string Name { get; set; } //nazwa specjalizacji
    public string InternalNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}