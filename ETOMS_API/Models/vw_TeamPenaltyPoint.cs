using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

[Keyless]
public partial class vw_TeamPenaltyPoint
{
    public int TournamentID { get; set; }

    public int TeamID { get; set; }

    public int? ActivePoints { get; set; }

    public int? PenaltyThreshold { get; set; }
}
