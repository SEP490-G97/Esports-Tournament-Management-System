using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

public partial class PlayerCredential
{
    [Key]
    public int PlayerCredentialID { get; set; }

    public int AccountID { get; set; }

    public int? GameID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string CredentialType { get; set; } = null!;

    [StringLength(150)]
    public string? Value { get; set; }

    [StringLength(500)]
    public string? FileUrl { get; set; }

    [Precision(0)]
    public DateTime? VerifiedAt { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("AccountID")]
    [InverseProperty("PlayerCredential")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("GameID")]
    [InverseProperty("PlayerCredential")]
    public virtual Game? Game { get; set; }

    [InverseProperty("PlayerCredential")]
    public virtual ICollection<RegistrationLineup> RegistrationLineup { get; set; } = new List<RegistrationLineup>();
}
