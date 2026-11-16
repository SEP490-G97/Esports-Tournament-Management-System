namespace ETOMS_API.Models;

public partial class OrganizationDocument
{
    public int OrganizationDocumentId { get; set; }

    public int OrganizationId { get; set; }

    public string DocumentType { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public string OriginalFileName { get; set; } = null!;

    public DateTime UploadedAt { get; set; }

    public virtual Organization Organization { get; set; } = null!;
}
