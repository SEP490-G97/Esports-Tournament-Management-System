using ETOMS_API.DTOs.Organization;
using ETOMS_API.HungModels;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class OrganizationService
{
        public async Task<Result<PublicOrganizationResponse>> GetPublicProfileAsync(
            int organizationId,
            CancellationToken cancellationToken)
        {
            var organization = await _unitOfWork.Repository<Organization>().Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.OrganizationID == organizationId && item.Status == "ACTIVE", cancellationToken);
            if (organization is null)
            {
                return Result.Failure<PublicOrganizationResponse>("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
            }

            return Result.Success(await ToPublicResponseAsync(organization, cancellationToken));
        }

        public async Task<Result<IReadOnlyList<PublicOrganizationResponse>>> GetPublicOrganizationsAsync(
            CancellationToken cancellationToken)
        {
            var organizations = await _unitOfWork.Repository<Organization>().Query()
                .AsNoTracking()
                .Where(item => item.Status == "ACTIVE")
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

            var responses = new List<PublicOrganizationResponse>(organizations.Count);
            foreach (var organization in organizations)
            {
                responses.Add(await ToPublicResponseAsync(organization, cancellationToken));
            }

            return Result.Success<IReadOnlyList<PublicOrganizationResponse>>(responses);
        }

        private async Task<PublicOrganizationResponse> ToPublicResponseAsync(
            Organization organization,
            CancellationToken cancellationToken)
        {
            var tournaments = await _unitOfWork.Repository<Tournament>().Query()
                .AsNoTracking()
                .Where(item => item.OrganizationID == organization.OrganizationID && item.Status != "DRAFT")
                .OrderByDescending(item => item.StartAt)
                .Select(item => new OrganizationTournamentResponse(
                    item.TournamentID,
                    item.Name,
                    item.Status,
                    TournamentStatusLabel(item.Status),
                    item.StartAt))
                .ToListAsync(cancellationToken);
            var reputation = await CalculateReputationAsync(organization.OrganizationID, cancellationToken);

            return new PublicOrganizationResponse(
                organization.OrganizationID,
                organization.Name,
                organization.OrganizationType,
                OrganizationTypeLabel(organization.OrganizationType),
                organization.ParentOrganization,
                organization.ContactEmail,
                organization.ContactPhone,
                organization.WebsiteUrl,
                organization.LogoUrl,
                organization.Description,
                tournaments,
                reputation);
        }
}
