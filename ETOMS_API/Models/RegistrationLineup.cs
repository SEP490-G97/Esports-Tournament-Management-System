using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

[Index("RegistrationID", "AccountID", Name = "UQ_RegistrationLineup_User", IsUnique = true)]
public partial class RegistrationLineup
{
    [Key]
    public int RegistrationLineupID { get; set; }

    public int RegistrationID { get; set; }

    public int AccountID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Slot { get; set; } = null!;

    [StringLength(150)]
    public string? InGameNameSnapshot { get; set; }

    public int? PlayerCredentialID { get; set; }

    [ForeignKey("AccountID")]
    [InverseProperty("RegistrationLineup")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("PlayerCredentialID")]
    [InverseProperty("RegistrationLineup")]
    public virtual PlayerCredential? PlayerCredential { get; set; }

    [ForeignKey("RegistrationID")]
    [InverseProperty("RegistrationLineup")]
    public virtual Registration Registration { get; set; } = null!;
}
