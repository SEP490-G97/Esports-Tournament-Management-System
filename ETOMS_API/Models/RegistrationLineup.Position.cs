using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

// GRL-04. Cột GamePositionID chưa có trong bảng RegistrationLineup của database chung.
// Khi nhóm thêm cột và scaffold lại, RegistrationLineup.cs sẽ có sẵn hai property này: xóa file này
// và phần cấu hình RegistrationLineup trong Repositories/EtomsDbContext.PendingTables.cs.
public partial class RegistrationLineup
{
    public int? GamePositionId { get; set; }

    public virtual GamePosition? GamePosition { get; set; }
}
