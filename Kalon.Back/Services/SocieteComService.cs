using System.Net.Http.Json;
using Kalon.Back.Configuration;
using Kalon.Back.DTOs;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Services;

public interface ISocieteComService
{
    Task<List<CompanySearchResultDto>> SearchAsync(string? dep, string? naf, int nbrep = 20, CancellationToken cancellationToken = default);
    Task<CompanyBilanDto> GetBilanAsync(string siren, CancellationToken cancellationToken = default);
    Task<CompanyContactDto> GetContactAsync(string siren, CancellationToken cancellationToken = default);
}


public class SocieteComService : ISocieteComService
{
    private readonly HttpClient _http;
    private readonly SocieteComOptions _options;

    public SocieteComService(HttpClient http, IOptions<SocieteComOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    private string ApiKey => _options.ApiKey;
    private string BaseUrl => _options.BaseUrl.TrimEnd('/');

    public async Task<List<CompanySearchResultDto>> SearchAsync(
        string? dep,
        string? naf,
        int nbrep = 20,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/entreprise/search?token={ApiKey}&nbrep={nbrep}";
        if (!string.IsNullOrEmpty(dep)) url += $"&dep={Uri.EscapeDataString(dep)}";
        if (!string.IsNullOrEmpty(naf)) url += $"&ape={Uri.EscapeDataString(naf)}";

        var response = await _http.GetFromJsonAsync<SocieteComSearchResponse>(url, cancellationToken);
        return response?.Data?.Results?
            .Select(r => new CompanySearchResultDto
            {
                Siren = r.Siren,
                NomCommercial = r.NomCommercial,
                NafCode = r.NafCode,
                NafLib = r.NafLib,
                CpVille = r.CpVille,
                Dep = r.Dep,
                Status = r.Status
            }).ToList() ?? [];
    }

    public async Task<CompanyBilanDto> GetBilanAsync(string siren, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/entreprise/{Uri.EscapeDataString(siren)}/bilans?token={ApiKey}";
        var response = await _http.GetFromJsonAsync<SocieteComBilanResponse>(url, cancellationToken);
        var latest = response?.Data?.FirstOrDefault();

        return new CompanyBilanDto
        {
            Siren = siren,
            Annee = latest?.Annee,
            ChiffreAffaires = latest?.Ca,
            ResultatNet = latest?.ResultatNet
        };
    }

    public async Task<CompanyContactDto> GetContactAsync(string siren, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/entreprise/{Uri.EscapeDataString(siren)}/dirigeants?token={ApiKey}";
        var response = await _http.GetFromJsonAsync<SocieteComContactResponse>(url, cancellationToken);
        var dirigeant = response?.Data?.FirstOrDefault();

        return new CompanyContactDto
        {
            Siren = siren,
            NomDirigeant = dirigeant?.Nom,
            PrenomDirigeant = dirigeant?.Prenom,
            Telephone = dirigeant?.Telephone,
            Email = dirigeant?.Email
        };
    }
}
