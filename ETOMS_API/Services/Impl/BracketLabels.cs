namespace ETOMS_API.Services.Impl;

// Tên vòng knockout dùng chung cho trang xếp lịch (BRK-12) và trang nhánh đấu (BRK-10, BRK-11).
public static class BracketLabels
{
    public static string KnockoutRound(int finalRound, int? roundNo) => (finalRound - (roundNo ?? 0)) switch
    {
        0 => "Chung kết",
        1 => "Bán kết",
        2 => "Tứ kết",
        _ => $"Vòng {roundNo}"
    };
}
