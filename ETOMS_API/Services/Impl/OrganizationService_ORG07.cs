using ETOMS_API.DTOs.Organization;
using ETOMS_API.HungModels;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class OrganizationService
{
        public async Task<Result<OrganizationReputationResponse>> GetReputationAsync(
            int organizationId,
            CancellationToken cancellationToken)
        {
            var organizationExists = await _unitOfWork.Repository<Organization>().Query()
                .AnyAsync(item => item.OrganizationID == organizationId && item.Status == "ACTIVE", cancellationToken);
            if (!organizationExists)
            {
                return Result.Failure<OrganizationReputationResponse>("Không tìm thấy đơn vị đang hoạt động.", StatusCodes.Status404NotFound);
            }

            return Result.Success(await CalculateReputationAsync(organizationId, cancellationToken));
        }

        private async Task<OrganizationReputationResponse> CalculateReputationAsync(
            int organizationId,
            CancellationToken cancellationToken)
        {
            var tournaments = _unitOfWork.Repository<Tournament>().Query()
                .Where(item => item.OrganizationID == organizationId);
            var totalTournaments = await tournaments.CountAsync(cancellationToken);
            var completedTournaments = await tournaments.CountAsync(item => item.Status == "COMPLETED", cancellationToken);
            var totalMatches = await _unitOfWork.Repository<Match>().Query()
                .CountAsync(item => item.Tournament.OrganizationID == organizationId, cancellationToken);
            var disputedMatches = await _unitOfWork.Repository<Dispute>().Query()
                .Where(item => item.Match.Tournament.OrganizationID == organizationId)
                .Select(item => item.MatchID)
                .Distinct()
                .CountAsync(cancellationToken);
            var resolvedDisputes = await _unitOfWork.Repository<Dispute>().Query()
                .Where(item => item.Match.Tournament.OrganizationID == organizationId && item.ResolvedAt != null)
                .Select(item => new { item.OpenedAt, item.ResolvedAt })
                .ToListAsync(cancellationToken);
            var refundCount = await _unitOfWork.Repository<PaymentRecord>().Query()
                .CountAsync(item => item.RecordKind == "REFUND"
                    && item.Registration.Tournament.OrganizationID == organizationId,
                    cancellationToken);

            var completionRate = totalTournaments == 0
                ? 0
                : decimal.Round(completedTournaments * 100m / totalTournaments, 2);
            var disputeRate = totalMatches == 0
                ? 0
                : decimal.Round(disputedMatches * 100m / totalMatches, 2);
            double? averageResolutionHours = resolvedDisputes.Count == 0
                ? null
                : resolvedDisputes.Average(item => (item.ResolvedAt!.Value - item.OpenedAt).TotalHours);

            return new OrganizationReputationResponse(
                organizationId,
                completedTournaments,
                totalTournaments,
                completionRate,
                disputedMatches,
                totalMatches,
                disputeRate,
                averageResolutionHours,
                refundCount);
        }
}
