using Kalon.Back.DTOs;

namespace Kalon.Back.Services;

public interface ISocieteComService
{
    Task<List<CompanySearchResultDto>> SearchAsync(string? dep, string? naf, int nbrep = 20, CancellationToken cancellationToken = default);
    Task<CompanyBilanDto> GetBilanAsync(string siren, CancellationToken cancellationToken = default);
    Task<CompanyContactDto> GetContactAsync(string siren, CancellationToken cancellationToken = default);
}
