using System.Text.Json.Serialization;

namespace Kalon.Back.DTOs;

public class CompanySearchResultDto
{
    public string Siren { get; set; } = string.Empty;
    public string NomCommercial { get; set; } = string.Empty;
    public string NafCode { get; set; } = string.Empty;
    public string NafLib { get; set; } = string.Empty;
    public string CpVille { get; set; } = string.Empty;
    public string Dep { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class CompanyBilanDto
{
    public string Siren { get; set; } = string.Empty;
    public int? Annee { get; set; }
    public decimal? ChiffreAffaires { get; set; }
    public decimal? ResultatNet { get; set; }
}

public class CompanyContactDto
{
    public string Siren { get; set; } = string.Empty;
    public string? NomDirigeant { get; set; }
    public string? PrenomDirigeant { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
}

public class SocieteComSearchResponse
{
    [JsonPropertyName("data")]
    public SocieteComSearchData? Data { get; set; }
}

public class SocieteComSearchData
{
    [JsonPropertyName("results")]
    public List<SocieteComSearchItem>? Results { get; set; }
}

public class SocieteComSearchItem
{
    [JsonPropertyName("siren")]
    public string Siren { get; set; } = string.Empty;

    [JsonPropertyName("nom_commercial")]
    public string NomCommercial { get; set; } = string.Empty;

    [JsonPropertyName("naf_code")]
    public string NafCode { get; set; } = string.Empty;

    [JsonPropertyName("naf_lib")]
    public string NafLib { get; set; } = string.Empty;

    [JsonPropertyName("cp_ville")]
    public string CpVille { get; set; } = string.Empty;

    [JsonPropertyName("dep")]
    public string Dep { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class SocieteComBilanResponse
{
    [JsonPropertyName("data")]
    public List<SocieteComBilanItem>? Data { get; set; }
}

public class SocieteComBilanItem
{
    [JsonPropertyName("annee")]
    public int? Annee { get; set; }

    [JsonPropertyName("ca")]
    public decimal? Ca { get; set; }

    [JsonPropertyName("resultat_net")]
    public decimal? ResultatNet { get; set; }
}

public class SocieteComContactResponse
{
    [JsonPropertyName("data")]
    public List<SocieteComContactItem>? Data { get; set; }
}

public class SocieteComContactItem
{
    [JsonPropertyName("nom")]
    public string? Nom { get; set; }

    [JsonPropertyName("prenom")]
    public string? Prenom { get; set; }

    [JsonPropertyName("telephone")]
    public string? Telephone { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}
