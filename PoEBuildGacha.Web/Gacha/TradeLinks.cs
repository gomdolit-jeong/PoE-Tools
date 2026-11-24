using System.Text.Json;

namespace PoEBuildGacha.Web.Gacha;

/// <summary>거래소 한 곳 (공식 거래 검색 주소의 앞부분)</summary>
public sealed record TradeSite(string Label, string SearchUrl);

/// <summary>
/// 군단 주얼 시드를 거래소에서 찾는 검색 주소. PoB TreeTab.lua의 "Search for Trade"와 같은 조건입니다:
/// 정복자별 시드 능력치(explicit.pseudo_timeless_jewel_&lt;정복자&gt;)를 시드 값으로 고정하고, 그중 하나라도 맞으면(count ≥ 1) 보여 줍니다.
/// 주소 뒤 ?q=&lt;검색 JSON&gt;으로 검색 조건을 넘깁니다.
/// </summary>
public static class TradeLinks
{
    public static readonly TradeSite Kakao = new("카카오 거래소", "https://poe.kakaogames.com/trade/search/");
    public static readonly TradeSite Global = new("국제 거래소", "https://www.pathofexile.com/trade/search/");

    /// <summary>PoB처럼 한 번에 넣는 시드 수 (거래소가 한 검색의 조건 수를 제한합니다)</summary>
    public const int MaxFilters = 10;

    /// <summary>거래소의 정복자 시드 능력치 id. 3.30에서 Ahuana가 Mazcotl로 바뀌었지만 거래소 id는 PoB처럼 ahuana를 씁니다.</summary>
    public static string StatId(Conqueror conqueror) =>
        "explicit.pseudo_timeless_jewel_" + (conqueror.Name == "Mazcotl" ? "ahuana" : conqueror.Name.ToLowerInvariant());

    /// <summary>이번 리그 이름으로 고를 수 있는 리그 목록 (거래소 리그 이름)</summary>
    public static string[] Leagues(string? currentLeague) =>
        currentLeague is { Length: > 0 } league
            ? [league, $"Hardcore {league}", "Standard", "Hardcore"]
            : ["Standard", "Hardcore"];

    /// <summary>시드들 중 하나라도 맞는 주얼을 찾는 검색 주소. conquerors가 여럿이면 정복자도 상관없이 찾습니다.</summary>
    public static string Url(TradeSite site, string league, IEnumerable<Conqueror> conquerors, IEnumerable<int> seeds)
    {
        var filters = seeds.SelectMany(seed => conquerors.Select(c => new
        {
            id = StatId(c),
            value = new { min = seed, max = seed },
        })).ToArray();
        var query = new
        {
            query = new
            {
                status = new { option = "online" },
                stats = new[] { new { type = "count", value = new { min = 1 }, filters } },
            },
            sort = new { price = "asc" },
        };
        return site.SearchUrl + Uri.EscapeDataString(league) + "?q=" + Uri.EscapeDataString(JsonSerializer.Serialize(query));
    }
}
