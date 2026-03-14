using System;

namespace DINBoard.Models;

public class LicenseInfo
{
    public bool IsTrial { get; set; } = true;
    public int TrialProjectsRemaining { get; set; } = 3;
    public string? LicenseKey { get; set; }
    public string? RegisteredTo { get; set; }
    public DateTime? ActivationDate { get; set; }
}
