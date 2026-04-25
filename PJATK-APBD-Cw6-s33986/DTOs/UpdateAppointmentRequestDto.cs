namespace PJATK_APBD_Cw6_s33986.DTOs;

public class UpdateAppointmentRequestDto
{
    public int IdPatient { get; set; }
    public int IdDoctor { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; }
    public string Reason { get; set; }
    public string IntrnalNotes { get; set; }
}