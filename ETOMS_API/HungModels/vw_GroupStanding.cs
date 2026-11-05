using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Keyless]
public partial class vw_GroupStanding
{
    public int TournamentID { get; set; }

    [StringLength(10)]
    public string? GroupLabel { get; set; }

    public int? TeamID { get; set; }

    public int? Played { get; set; }

    public int? Won { get; set; }

    public int? Lost { get; set; }

    public int? ScoreDiff { get; set; }

    public int? Point { get; set; }
}
