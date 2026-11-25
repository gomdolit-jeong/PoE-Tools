using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PoETools.Web.Core;

// ───────── crafting.json (tools/crawl_crafting_data.py) ─────────

/// <summary>접두·접미 옵션 하나. W: 태그별 등장 가중치(앞에서부터 처음 맞는 태그), Gw: 태그별 가중치 배율(%)</summary>
public sealed class CraftMod
{
    [JsonPropertyName("s")] public string Side { get; set; } = "p";
    [JsonPropertyName("g")] public string[] Groups { get; set; } = [];
    [JsonPropertyName("t")] public string Type { get; set; } = "";
    [JsonPropertyName("l")] public int Level { get; set; }
    [JsonPropertyName("w")] public List<List<object>> W { get; set; } = [];
    [JsonPropertyName("gw")] public List<List<object>> Gw { get; set; } = [];
    [JsonPropertyName("tags")] public string[] Tags { get; set; } = [];
    [JsonPropertyName("en")] public string En { get; set; } = "";
    [JsonPropertyName("ko")] public string Ko { get; set; } = "";
    [JsonPropertyName("v")] public double[][] V { get; set; } = [];
    [JsonPropertyName("d")] public string Domain { get; set; } = "item";

    [JsonIgnore] public string Id { get; set; } = "";
    [JsonIgnore] public bool Prefix => Side == "p";
    [JsonIgnore] public string Text => string.IsNullOrEmpty(Ko) ? En : Ko;
    [JsonIgnore] public bool Crafted => Domain == "crafted";
}

public sealed record CraftBase(
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("k")] string Korean,
    [property: JsonPropertyName("c")] string Class,
    [property: JsonPropertyName("t")] string[] Tags,
    [property: JsonPropertyName("l")] int Level);

public sealed record CraftEssence(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("k")] string Korean,
    [property: JsonPropertyName("lv")] int Level,
    [property: JsonPropertyName("il")] int MaxItemLevel,
    [property: JsonPropertyName("m")] Dictionary<string, string> Mods);

public sealed record CraftBench(
    [property: JsonPropertyName("m")] string Mod,
    [property: JsonPropertyName("c")] string[] Classes,
    [property: JsonPropertyName("cost")] Dictionary<string, int> Cost);

/// <summary>화석: Pos 태그는 가중치 배율(%), Neg 태그는 막음, Forced는 확정으로 붙이는 옵션(공허 화석: 심연 홈), Allowed는 쓸 수 있는 베이스 태그</summary>
public sealed record CraftFossil(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("k")] string Korean,
    [property: JsonPropertyName("pos")] List<List<object>> Pos,
    [property: JsonPropertyName("neg")] string[] Neg,
    [property: JsonPropertyName("forced")] string[]? Forced = null,
    [property: JsonPropertyName("allowed")] string[]? Allowed = null)
{
    public bool UsableOn(CraftBase item) => Allowed is not { Length: > 0 } || Allowed.Any(item.Tags.Contains);
}

/// <summary>혼돈 공명기 (S: 화석 소켓 수)</summary>
public sealed record CraftResonator(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("k")] string Korean,
    [property: JsonPropertyName("s")] int Sockets);

public sealed class CraftData
{
    [JsonPropertyName("fossils")] public CraftFossil[] Fossils { get; set; } = [];
    [JsonPropertyName("resonators")] public CraftResonator[] Resonators { get; set; } = [];
    [JsonPropertyName("classes")] public Dictionary<string, string> Classes { get; set; } = [];
    [JsonPropertyName("influences")] public string[] Influences { get; set; } = [];
    [JsonPropertyName("bases")] public CraftBase[] Bases { get; set; } = [];
    [JsonPropertyName("mods")] public Dictionary<string, CraftMod> Mods { get; set; } = [];
    [JsonPropertyName("essences")] public CraftEssence[] Essences { get; set; } = [];
    [JsonPropertyName("bench")] public CraftBench[] Bench { get; set; } = [];
    [JsonPropertyName("currency")] public Dictionary<string, string[]> Currency { get; set; } = [];
}

/// <summary>crafting.json을 한 번만 받아 둡니다.</summary>
public sealed class CraftDataStore(HttpClient http)
{
    Task<CraftData>? loading;
    Task<CraftPrices?>? prices;

    public Task<CraftData> Load() => loading ??= Fetch();

    /// <summary>poe.ninja 시세 (없거나 못 받으면 null → 기본 가격을 씀)</summary>
    public Task<CraftPrices?> LoadPrices() => prices ??= FetchPrices();

    async Task<CraftPrices?> FetchPrices()
    {
        try { return await http.GetFromJsonAsync<CraftPrices>("data/prices.json"); }
        catch (Exception) { return null; }
    }

    async Task<CraftData> Fetch()
    {
        var data = await http.GetFromJsonAsync<CraftData>("data/crafting.json")
                   ?? throw new InvalidOperationException("crafting.json이 비어 있어요");
        foreach (var (id, mod) in data.Mods) mod.Id = id;
        return data;
    }
}

// ───────── 화폐 ─────────

/// <summary>poe.ninja 시세 (tools/crawl_prices.py): 영어 이름 → 카오스</summary>
public sealed class CraftPrices
{
    [JsonPropertyName("league")] public string League { get; set; } = "";
    [JsonPropertyName("updated")] public string Updated { get; set; } = "";
    [JsonPropertyName("prices")] public Dictionary<string, double> Prices { get; set; } = [];
}

public sealed record Currency(string Id, string Name, string Korean, double DefaultPrice);

public static class Currencies
{
    const string P = "Metadata/Items/Currency/";
    public const string Chaos = P + "CurrencyRerollRare";
    public const string Alteration = P + "CurrencyRerollMagic";
    public const string Augment = P + "CurrencyAddModToMagic";
    public const string Transmute = P + "CurrencyUpgradeToMagic";
    public const string Regal = P + "CurrencyUpgradeMagicToRare";
    public const string Alchemy = P + "CurrencyUpgradeToRare";
    public const string Scour = P + "CurrencyConvertToNormal";
    public const string Exalt = P + "CurrencyAddModToRare";
    public const string Divine = P + "CurrencyModValues";
    public const string Blessed = P + "CurrencyRerollImplicit";
    public const string Sacred = P + "CurrencyRerollDefences";

    static readonly string[] Jewellery = ["Amulet", "Ring", "Belt"];
    static readonly string[] Armour = ["Body Armour", "Helmet", "Gloves", "Boots", "Shield", "Quiver"];

    /// <summary>
    /// 작업대 "접두어/접미어 속성 변경 불가" 비용 (3.30.0 패치 노트): 무기 신성 4 + 신성화 1,
    /// 방어구·화살통 신성 4 + 축복 4, 장신구 신성 4 + 엑잘 8.
    /// </summary>
    public static Dictionary<string, int> LockCost(string itemClass) =>
        Jewellery.Contains(itemClass) ? new() { [Divine] = 4, [Exalt] = 8 }
        : Armour.Contains(itemClass) ? new() { [Divine] = 4, [Blessed] = 4 }
        : new() { [Divine] = 4, [Sacred] = 1 };

    /// <summary>카오스 기준 기본 가격 (리그마다 달라서 화면에서 고칠 수 있음)</summary>
    public static readonly Currency[] All =
    [
        new(Chaos, "Chaos Orb", "카오스 오브", 1),
        new(Alteration, "Orb of Alteration", "변화의 오브", 0.15),
        new(Augment, "Orb of Augmentation", "확장의 오브", 0.1),
        new(Transmute, "Orb of Transmutation", "변환의 오브", 0.05),
        new(Regal, "Regal Orb", "제왕의 오브", 0.5),
        new(Alchemy, "Orb of Alchemy", "연금술의 오브", 0.3),
        new(Scour, "Orb of Scouring", "정제의 오브", 0.5),
        new(Exalt, "Exalted Orb", "엑잘티드 오브", 15),
        new(Divine, "Divine Orb", "신성한 오브", 150),
        new(Blessed, "Blessed Orb", "축복의 오브", 0.5),
        new(Sacred, "Sacred Orb", "신성화의 오브", 10),
        new(P + "CurrencyCorrupt", "Vaal Orb", "바알 오브", 1),
        new(P + "CurrencyFlaskQuality", "Glassblower's Bauble", "유리공의 방울", 0.1),
        new(P + "CurrencyArmourQuality", "Armourer's Scrap", "방어구 장인의 고철", 0.02),
        new(P + "CurrencyGemQuality", "Gemcutter's Prism", "젬 세공사의 프리즘", 1),
        new(P + "CurrencyRerollSocketNumbers", "Jeweller's Orb", "보석공의 오브", 0.1),
        new(P + "CurrencyRerollSocketLinks", "Orb of Fusing", "결합의 오브", 0.2),
        new(P + "CurrencyRerollSocketColours", "Chromatic Orb", "색채의 오브", 0.05),
        new(P + "CurrencyUpgradeRandomly", "Orb of Chance", "기회의 오브", 0.05),
        new(P + "CurrencyInstillingOrb", "Instilling Orb", "주입의 오브", 1),
    ];

    /// <summary>화석 기본 가격 (리그마다 크게 다르니 화면에서 고쳐 주세요)</summary>
    public const double FossilPrice = 2;

    /// <summary>혼돈 공명기 기본 가격: 소켓 1·2·3·4개</summary>
    public static double ResonatorPrice(int sockets) => sockets switch { 1 => 0.5, 2 => 1, 3 => 3, _ => 6 };

    /// <summary>에센스 기본 가격: 단계(레벨)로 어림합니다.</summary>
    public static double EssencePrice(CraftEssence e) => e.Level switch
    {
        >= 8 => 15,       // 타락 전용 (광란·공포·히스테리·광기·계략)
        7 => 3,           // 귀청이 떨어질 듯한
        6 => 1,           // 날카로운
        5 => 0.5,
        0 => 10,          // 황폐 등 단계 없는 에센스
        _ => 0.2,
    };
}

// ───────── 목표 옵션 ─────────

/// <summary>원하는 옵션 하나: 옵션 종류(Type, 접두/접미)와 최소 티어. Accepted: 이 티어 이상인 일반 옵션 id</summary>
public sealed class CraftTarget
{
    public required string Type { get; init; }
    public required bool Prefix { get; init; }
    /// <summary>티어 1(가장 좋음)부터 정렬된 일반 옵션 (일반으로 안 나오는 에센스 전용이면 에센스 옵션)</summary>
    public required CraftMod[] Tiers { get; init; }
    public int Tier { get; set; } = 1;
    /// <summary>균열 옵션처럼 베이스에 처음부터 붙어 있고 지워지지 않는 옵션 (고른 티어로 붙어 있다고 봄)</summary>
    public bool Fixed { get; set; }
    /// <summary>베일드 옵션: 일반 크래프팅으로는 안 붙어서, 자리만 비워 두고 마지막에 따로 붙인다고 봄</summary>
    public bool Reserved { get; init; }

    public CraftMod Threshold => Tiers[Math.Clamp(Tier, 1, Tiers.Length) - 1];

    /// <summary>옵션이 이 목표를 채우는지: 일반 옵션은 티어로, 에센스·작업대 옵션은 첫 수치의 최솟값으로 비교</summary>
    public bool Matches(CraftMod mod)
    {
        if (mod.Type != Type || mod.Prefix != Prefix) return false;
        var index = Array.IndexOf(Tiers, mod);
        if (index >= 0) return index < Tier;
        var need = Threshold.V.Length > 0 ? Math.Abs(Threshold.V[0][0]) : 0;
        var have = mod.V.Length > 0 ? Math.Abs(mod.V[0][0]) : 0;
        return have >= need;
    }
}

// ───────── 옵션 풀과 아이템 ─────────

/// <summary>베이스·아이템 레벨·영향력에서 자연적으로 붙을 수 있는 옵션과 가중치</summary>
public sealed class ModPool
{
    public CraftBase Base { get; }
    public int ItemLevel { get; }
    public CraftMod[] Mods { get; }
    public int[] Weights { get; }
    readonly long[] cumulative;
    public long Total { get; }

    public ModPool(CraftData data, CraftBase item, int itemLevel, IReadOnlyCollection<string> influences)
    {
        Base = item;
        ItemLevel = itemLevel;
        var tags = new HashSet<string>(item.Tags);
        foreach (var t in item.Tags)
            foreach (var inf in influences)
                tags.Add($"{t}_{inf}");

        var mods = new List<CraftMod>();
        var weights = new List<int>();
        var veiled = new List<CraftMod>();
        foreach (var mod in data.Mods.Values)
        {
            if (mod.Level > itemLevel || mod.W.Count == 0) continue;
            var w = FirstMatch(mod.W, tags) ?? 0;
            if (w <= 0) continue;
            if (mod.Domain == "unveiled") { veiled.Add(mod); continue; }
            var g = FirstMatch(mod.Gw, tags);
            if (g is { } gm) w = w * gm / 100;
            if (w <= 0) continue;
            mods.Add(mod);
            weights.Add(w);
        }
        Mods = [.. mods];
        Weights = [.. weights];
        Veiled = [.. veiled];
        (cumulative, Total) = Sum(Weights);
    }

    /// <summary>이 베이스에 붙을 수 있는 베일드 옵션 (풀에는 넣지 않음)</summary>
    public CraftMod[] Veiled { get; private init; } = [];

    ModPool(CraftBase item, int itemLevel, CraftMod[] mods, int[] weights)
    {
        Base = item;
        ItemLevel = itemLevel;
        Mods = mods;
        Weights = weights;
        (cumulative, Total) = Sum(Weights);
    }

    static (long[], long) Sum(int[] weights)
    {
        var cumulative = new long[weights.Length];
        long sum = 0;
        for (var i = 0; i < weights.Length; i++) cumulative[i] = sum += weights[i];
        return (cumulative, sum);
    }

    /// <summary>
    /// 화석을 넣은 공명기로 굴릴 때의 풀: 옵션 태그가 화석의 막는 태그와 겹치면 0,
    /// 늘리는 태그와 겹치면 (가중치/100)배를 화석·태그마다 곱합니다 (Craft of Exile과 같은 어림).
    /// </summary>
    public ModPool WithFossils(IEnumerable<CraftFossil> fossils)
    {
        var list = fossils.ToArray();
        var weights = new int[Mods.Length];
        for (var i = 0; i < Mods.Length; i++)
        {
            double w = Weights[i];
            foreach (var f in list)
            {
                if (f.Neg.Any(t => Array.IndexOf(Mods[i].Tags, t) >= 0)) { w = 0; break; }
                foreach (var pos in f.Pos)
                    if (pos.Count == 2 && Array.IndexOf(Mods[i].Tags, pos[0].ToString()) >= 0)
                        w *= double.Parse(pos[1].ToString()!, System.Globalization.CultureInfo.InvariantCulture) / 100;
            }
            weights[i] = (int)Math.Min(int.MaxValue / 64, Math.Round(w));
        }
        return new ModPool(Base, ItemLevel, Mods, weights);
    }

    /// <summary>목표 하나를 채우는 옵션들의 가중치 합 (화석 조합 고르기에 씀)</summary>
    public long WeightOf(CraftTarget target)
    {
        long sum = 0;
        for (var i = 0; i < Mods.Length; i++) if (target.Matches(Mods[i])) sum += Weights[i];
        return sum;
    }

    static int? FirstMatch(List<List<object>> entries, HashSet<string> tags)
    {
        foreach (var e in entries)
            if (e.Count == 2 && tags.Contains(e[0].ToString()!))
                return int.Parse(e[1].ToString()!);
        return null;
    }

    /// <summary>아이템에 붙일 수 있는 옵션 하나를 가중치대로 뽑습니다 (같은 그룹·꽉 찬 쪽은 제외). 없으면 null</summary>
    public CraftMod? Pick(CraftItem item, Random rng)
    {
        if (Total == 0) return null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var r = rng.NextInt64(Total);
            var i = Array.BinarySearch(cumulative, r + 1);
            if (i < 0) i = ~i;
            var mod = Mods[i];
            if (item.CanAdd(mod)) return mod;
        }
        // 거의 다 막혔을 때: 가능한 것만 모아서 다시 뽑습니다.
        long sum = 0;
        for (var i = 0; i < Mods.Length; i++) if (item.CanAdd(Mods[i])) sum += Weights[i];
        if (sum == 0) return null;
        var pick = rng.NextInt64(sum);
        for (var i = 0; i < Mods.Length; i++)
        {
            if (!item.CanAdd(Mods[i])) continue;
            pick -= Weights[i];
            if (pick < 0) return Mods[i];
        }
        return null;
    }
}

/// <summary>시뮬레이션 중인 아이템 (매직이면 접두·접미 1개씩, 레어면 3개씩)</summary>
public sealed class CraftItem
{
    public readonly List<CraftMod> Mods = new(6);
    public bool Rare;

    public int MaxSide => Rare ? 3 : 1;
    public int Prefixes { get { var n = 0; foreach (var m in Mods) if (m.Prefix) n++; return n; } }
    public int Suffixes => Mods.Count - Prefixes;

    public bool HasRoom(bool prefix) => (prefix ? Prefixes : Suffixes) < MaxSide;

    public bool CanAdd(CraftMod mod)
    {
        if (!HasRoom(mod.Prefix)) return false;
        foreach (var m in Mods)
            foreach (var g in m.Groups)
                if (Array.IndexOf(mod.Groups, g) >= 0) return false;
        return true;
    }

    /// <summary>균열 옵션: 어떤 오브로도 지워지지 않고 늘 붙어 있는 옵션</summary>
    public CraftMod[] Fixed = [];

    public void Clear(bool rare) { Mods.Clear(); Mods.AddRange(Fixed); Rare = rare; }
}

// ───────── 방법별 시뮬레이션 ─────────

public sealed record CraftStep(string Text);

/// <summary>방법 하나의 결과. Uses: 성공 1번에 드는 평균 화폐 수, Cost: 카오스 기준 예상 비용</summary>
public sealed record CraftResult(
    string Name,
    string[] Steps,
    double SuccessPerTry,
    int Successes,
    long Tries,
    IReadOnlyDictionary<string, double> Uses,
    double Cost,
    string? Note,
    IReadOnlyList<SampleMod[]> Samples,
    IReadOnlyList<ExtraMod> Extras)
{
    public bool Found => Successes > 0;
}

/// <summary>성공한 아이템의 옵션 한 줄. Tier: 일반 옵션 티어(에센스·작업대 옵션은 0), Source: "에센스"·"작업대" 등</summary>
public sealed record SampleMod(string Text, bool Prefix, int Tier, string? Source, bool Target);

/// <summary>목표 말고 함께 붙은 옵션 종류와, 성공한 아이템 중 그 옵션이 붙은 비율</summary>
public sealed record ExtraMod(string Label, bool Prefix, double Share);

public sealed class CraftSimulator
{
    readonly CraftData data;
    readonly ModPool pool;
    readonly CraftTarget[] targets;
    readonly Func<string, double> price;
    readonly Random rng = new();
    /// <summary>목표마다 작업대로 붙일 수 있는 옵션 (없으면 null)</summary>
    readonly (CraftMod Mod, Dictionary<string, int> Cost)?[] bench;

    public CraftSimulator(CraftData data, ModPool pool, IReadOnlyList<CraftTarget> targets, Func<string, double> price)
    {
        this.data = data;
        this.pool = pool;
        this.targets = [.. targets];
        this.price = price;
        bench = this.targets.Select(t =>
        {
            foreach (var b in data.Bench)
            {
                if (Array.IndexOf(b.Classes, pool.Base.Class) < 0) continue;
                if (data.Mods.TryGetValue(b.Mod, out var m) && t.Matches(m))
                    return ((CraftMod, Dictionary<string, int>)?)(m, b.Cost);
            }
            return null;
        }).ToArray();
    }

    /// <summary>풀의 옵션 → 티어 (같은 종류 안에서 요구 레벨이 높은 순서로 1부터)</summary>
    Dictionary<CraftMod, int>? tiers;

    int TierOf(CraftMod mod)
    {
        tiers ??= pool.Mods.GroupBy(m => (m.Type, m.Prefix))
            .SelectMany(g => g.OrderByDescending(m => m.Level).Select((m, i) => (m, tier: i + 1)))
            .ToDictionary(x => x.m, x => x.tier);
        return tiers.GetValueOrDefault(mod);
    }

    SampleMod Sample(CraftMod mod, string? source) =>
        new(mod.Text, mod.Prefix, source is null ? TierOf(mod) : 0,
            source ?? (TierOf(mod) != 0 ? null : mod.Domain == "delve" ? "화석" : "에센스"), IsTarget(mod));

    public bool CanBench(int target) => bench[target] is not null;

    /// <summary>베일드 옵션 자리 수 (접두·접미)</summary>
    int ReservedPrefixes => targets.Count(t => t.Reserved && t.Prefix);
    int ReservedSuffixes => targets.Count(t => t.Reserved && !t.Prefix);

    /// <summary>균열 옵션 (목표 중 Fixed인 것을 고른 티어로)</summary>
    CraftMod[] FixedMods => targets.Where(t => t.Fixed).Select(t => t.Threshold).ToArray();

    CraftItem NewItem() => new() { Fixed = FixedMods };

    public string BenchText(int target) => bench[target] is { } b ? b.Mod.Text : "";

    /// <summary>
    /// 목표를 다 채웠는지. 하나가 모자라도 작업대로 붙일 수 있고 자리가 있으면 성공으로 보고 그 목표 번호를 돌려줍니다.
    /// 반환: (성공, 작업대로 채울 목표 번호 또는 -1)
    /// </summary>
    (bool Ok, int Bench) Check(CraftItem item, bool allowBench)
    {
        var missing = -1;
        Span<bool> used = stackalloc bool[6];
        for (var t = 0; t < targets.Length; t++)
        {
            if (targets[t].Reserved) continue;
            var hit = false;
            for (var i = 0; i < item.Mods.Count; i++)
            {
                if (used[i] || !targets[t].Matches(item.Mods[i])) continue;
                used[i] = hit = true;
                break;
            }
            if (hit) continue;
            if (missing >= 0 || !allowBench || bench[t] is not { } b) return (false, -1);
            missing = t;
        }
        // 베일드 옵션 자리 (작업대로 붙일 옵션이 있으면 그 자리까지 빼고)
        var freeP = 3 - item.Prefixes - ReservedPrefixes;
        var freeS = 3 - item.Suffixes - ReservedSuffixes;
        if (missing < 0) return freeP >= 0 && freeS >= 0 ? (true, -1) : (false, -1);
        var mod = bench[missing]!.Value.Mod;
        if (mod.Prefix) freeP--; else freeS--;
        return item.CanAdd(mod) && freeP >= 0 && freeS >= 0 ? (true, missing) : (false, -1);
    }

    /// <summary>모든 목표가 아이템에 이미 붙은 옵션과 겹치지 않는지 (매직 단계에서 쓰레기 옵션 판정)</summary>
    bool IsTarget(CraftMod mod) => targets.Any(t => t.Matches(mod));

    void RollRare(CraftItem item, CraftMod? forced)
    {
        item.Clear(rare: true);
        if (forced is not null && item.CanAdd(forced)) item.Mods.Add(forced);
        // 레어 옵션 개수: 4개 8/12, 5개 3/12, 6개 1/12 (커뮤니티 추정치)
        var r = rng.Next(12);
        var count = r < 8 ? 4 : r < 11 ? 5 : 6;
        while (item.Mods.Count < count && pool.Pick(item, rng) is { } m) item.Mods.Add(m);
    }

    void RollMagic(CraftItem item)
    {
        item.Clear(rare: false);
        var count = rng.Next(2) + 1;
        while (item.Mods.Count < count && pool.Pick(item, rng) is { } m) item.Mods.Add(m);
    }

    void Add(CraftItem item)
    {
        if (pool.Pick(item, rng) is { } m) item.Mods.Add(m);
    }

    /// <summary>
    /// 시간 예산 안에서 방법 하나를 여러 번 돌립니다. tryOnce는 한 번의 "시도"를 하고, 쓴 화폐를 uses에 더하고, 성공이면 작업대 목표 번호(없으면 -1), 실패면 null.
    /// </summary>
    async Task<CraftResult> Run(string name, string[] steps, CraftItem item, Func<Dictionary<string, double>, int?> tryOnce,
        int maxSuccesses, long maxTries, CancellationToken cancel, string? note = null)
    {
        var uses = new Dictionary<string, double>();
        var samples = new List<SampleMod[]>();
        var extras = new Dictionary<(string, bool), int>();
        long tries = 0;
        var successes = 0;
        var started = Environment.TickCount64;
        var yielded = started;
        while (tries < maxTries && successes < maxSuccesses)
        {
            tries++;
            if (tryOnce(uses) is { } benchTarget)
            {
                successes++;
                if (benchTarget >= 0)
                    foreach (var (cur, n) in bench[benchTarget]!.Value.Cost)
                        uses[cur] = uses.GetValueOrDefault(cur) + n;
                // 완성된 아이템: 예시 몇 개와, 목표 말고 함께 붙은 옵션 종류
                foreach (var m in item.Mods)
                    if (!IsTarget(m))
                    {
                        var key = (CraftHelpers.Template(m.Text), m.Prefix);
                        extras[key] = extras.GetValueOrDefault(key) + 1;
                    }
                if (samples.Count < 5)
                {
                    var mods = item.Mods.Select(m => Sample(m, Array.IndexOf(item.Fixed, m) >= 0 ? "균열" : null)).ToList();
                    if (benchTarget >= 0) mods.Add(Sample(bench[benchTarget]!.Value.Mod, "작업대"));
                    samples.Add([.. mods.OrderBy(m => m.Prefix ? 0 : 1)]);
                }
            }
            // 화면이 멈추지 않게 가끔 양보하고, 방법 하나에 2.5초가 넘으면 그만둡니다.
            var now = Environment.TickCount64;
            if (now - started > 2500) break;
            if (now - yielded > 60)
            {
                await Task.Delay(1, cancel);
                yielded = Environment.TickCount64;
            }
        }

        var per = successes > 0 ? (double)successes : 1;
        var perSuccess = uses.ToDictionary(kv => kv.Key, kv => kv.Value / per);
        var cost = successes > 0 ? perSuccess.Sum(kv => kv.Value * price(kv.Key)) : double.PositiveInfinity;
        var extraList = extras.OrderByDescending(kv => kv.Value).Take(10)
            .Select(kv => new ExtraMod(kv.Key.Item1, kv.Key.Item2, (double)kv.Value / per)).ToList();
        return new CraftResult(name, steps, (double)successes / Math.Max(1, tries), successes, tries, perSuccess, cost, note, samples, extraList);
    }

    static void Use(Dictionary<string, double> uses, string currency, double n = 1) =>
        uses[currency] = uses.GetValueOrDefault(currency) + n;

    string BenchStep(bool allowBench) =>
        allowBench && targets.Select((_, i) => i).Any(CanBench) ? "모자란 옵션 하나는 작업대로 붙이기 (자리가 있을 때)" : "";

    public Task<CraftResult> ChaosSpam(bool allowBench, CancellationToken cancel)
    {
        var item = NewItem();
        string[] steps = ["레어 아이템에 카오스 오브를 원하는 옵션이 나올 때까지 사용", BenchStep(allowBench)];
        return Run("카오스 스팸", steps.Where(s => s != "").ToArray(), item, uses =>
        {
            Use(uses, Currencies.Chaos);
            RollRare(item, null);
            var (ok, b) = Check(item, allowBench);
            return ok ? b : null;
        }, 400, 3_000_000, cancel);
    }

    public Task<CraftResult> EssenceSpam(CraftEssence essence, bool allowBench, CancellationToken cancel)
    {
        var forced = data.Mods[essence.Mods[pool.Base.Class]];
        var item = NewItem();
        var id = essence.Id;
        string[] steps = [$"{essence.Korean}을(를) 원하는 옵션이 나올 때까지 사용 (확정: {forced.Text})", BenchStep(allowBench)];
        return Run($"에센스 스팸 · {essence.Korean}", steps.Where(s => s != "").ToArray(), item, uses =>
        {
            Use(uses, id);
            RollRare(item, forced);
            var (ok, b) = Check(item, allowBench);
            return ok ? b : null;
        }, 400, 3_000_000, cancel, note: id);
    }

    /// <summary>화석을 넣은 혼돈 공명기를 레어 아이템에 반복 사용 (이 시뮬레이터의 풀은 WithFossils로 만든 것)</summary>
    public Task<CraftResult> FossilSpam(CraftResonator resonator, CraftFossil[] fossils, bool allowBench, CancellationToken cancel)
    {
        var item = NewItem();
        var names = string.Join(" + ", fossils.Select(f => f.Korean));
        string[] steps = [$"{resonator.Korean}에 {names}을(를) 넣고 원하는 옵션이 나올 때까지 사용", BenchStep(allowBench)];
        var forced = fossils.SelectMany(f => f.Forced ?? []).Select(id => data.Mods.GetValueOrDefault(id)).OfType<CraftMod>().FirstOrDefault();
        return Run($"화석 · {names}", steps.Where(s => s != "").ToArray(), item, uses =>
        {
            Use(uses, resonator.Id);
            foreach (var f in fossils) Use(uses, f.Id);
            RollRare(item, forced);
            var (ok, b) = Check(item, allowBench);
            return ok ? b : null;
        }, 400, 3_000_000, cancel);
    }

    /// <summary>
    /// 변화·확장으로 매직 단계에서 접두·접미 목표를 하나씩 맞춘 뒤 제왕 → (엑잘) → 작업대.
    /// 매직 단계는 붙은 옵션이 모두 목표이고, 접두·접미 목표가 있는 쪽마다 하나씩 붙었을 때 통과입니다.
    /// 실패하면 정제 + 변환으로 다시 매직부터 시작합니다.
    /// </summary>
    public Task<CraftResult> AltRegal(bool useExalts, bool allowBench, CancellationToken cancel)
    {
        var item = NewItem();
        var wantPrefix = targets.Any(t => t.Prefix && !t.Reserved);
        var wantSuffix = targets.Any(t => !t.Prefix && !t.Reserved);
        var magicNeed = (wantPrefix ? 1 : 0) + (wantSuffix ? 1 : 0);
        var failed = false;

        var steps = new List<string>
        {
            "일반 아이템에 변환의 오브 → 변화의 오브를 돌려 매직 옵션 맞추기",
            "옵션이 하나만 붙었고 그게 원하는 옵션이면 확장의 오브",
            "원하는 옵션만 붙은 매직이 되면 제왕의 오브",
        };
        if (useExalts) steps.Add("남은 옵션은 엑잘티드 오브로 하나씩 추가");
        var bs = BenchStep(allowBench);
        if (bs != "") steps.Add(bs);
        steps.Add("실패하면 정제의 오브 + 변환의 오브로 처음부터");

        // 실패한 아이템은 정제해서 다시 쓰고, 성공하면 새 베이스로 시작합니다.
        return Run(useExalts ? "변화 → 제왕 → 엑잘" : "변화 → 제왕", [.. steps], item, uses =>
        {
            if (failed) Use(uses, Currencies.Scour);
            var result = Attempt(uses);
            failed = result is null;
            return result;
        }, 400, 300_000, cancel);

        int? Attempt(Dictionary<string, double> uses)
        {
            Use(uses, Currencies.Transmute);
            item.Clear(rare: false);
            Add(item);
            // 매직 단계: 원하는 옵션만, 필요한 수만큼 붙을 때까지 변화 (한 시도에서 너무 오래 돌면 끊어서 셉니다)
            for (var spins = 0; ; spins++)
            {
                if (spins > 20_000) return null;
                var good = item.Mods.Count > 0 && item.Mods.All(IsTarget);
                if (good && item.Mods.Count < magicNeed)
                {
                    Use(uses, Currencies.Augment);
                    Add(item);
                    good = item.Mods.All(IsTarget);
                }
                if (good && item.Mods.Count >= magicNeed) break;
                Use(uses, Currencies.Alteration);
                RollMagic(item);
            }

            Use(uses, Currencies.Regal);
            item.Rare = true;
            Add(item);
            var (ok, b) = Check(item, allowBench);
            if (ok) return b;
            if (!useExalts) return null;
            // 쓰레기 옵션이 섞였어도 남은 자리로 아직 목표를 채울 수 있으면 엑잘을 계속합니다.
            while (item.Mods.Count < 6 && HasRoomForMissing(item))
            {
                Use(uses, Currencies.Exalt);
                Add(item);
                (ok, b) = Check(item, allowBench);
                if (ok) return b;
            }
            return null;
        }
    }

    /// <summary>한쪽(접두 또는 접미) 목표를 이 아이템이 다 채웠는지</summary>
    bool SideDone(CraftItem item, bool prefix)
    {
        Span<bool> used = stackalloc bool[6];
        foreach (var t in targets)
        {
            if (t.Prefix != prefix || t.Reserved) continue;
            var hit = false;
            for (var i = 0; i < item.Mods.Count; i++)
            {
                if (used[i] || !t.Matches(item.Mods[i])) continue;
                used[i] = hit = true;
                break;
            }
            if (!hit) return false;
        }
        return true;
    }

    /// <summary>
    /// 메타 크래프팅: 한쪽 목표가 다 나올 때까지 카오스(또는 에센스)를 굴린 뒤,
    /// 작업대에서 그쪽을 "속성 변경 불가"로 고정 → 정제의 오브로 반대쪽만 지우기 → 엑잘로 반대쪽 채우기.
    /// 고정 옵션은 반대쪽 자리를 하나 차지하므로(접두 고정은 접미 옵션) 반대쪽에 빈자리가 있어야 하고, 정제하면 함께 지워집니다.
    /// 엑잘로 반대쪽이 꽉 찼는데 실패하면 고정할 자리가 없어서 처음부터(정제 후 다시 굴리기) 시작합니다.
    /// </summary>
    public Task<CraftResult> MetaLock(bool lockPrefixes, CraftEssence? essence, bool allowBench, CancellationToken cancel)
    {
        var item = NewItem();
        var lockCost = Currencies.LockCost(pool.Base.Class);
        var forced = essence is null ? null : data.Mods[essence.Mods[pool.Base.Class]];
        var kept = lockPrefixes ? "접두" : "접미";
        var other = lockPrefixes ? "접미" : "접두";
        var lockText = lockPrefixes ? "접두어 속성 변경 불가" : "접미어 속성 변경 불가";
        var roll = essence is null ? "카오스 오브" : essence.Korean;
        var steps = new List<string>
        {
            $"{roll}로 {kept} 옵션이 원하는 대로 나오고 {other}에 빈자리가 있을 때까지 굴리기",
            $"작업대에서 \"{lockText}\" 붙이기 (신성한 오브 4개 + α)",
            $"정제의 오브로 {other} 옵션만 지우기 ({kept}는 그대로 남고 고정 옵션도 같이 지워짐)",
            $"엑잘티드 오브로 {other} 옵션을 하나씩 추가",
        };
        var bs = BenchStep(allowBench);
        if (bs != "") steps.Add(bs);
        steps.Add($"{other}가 꽉 찼는데 실패하면 다시 고정 → 정제, 고정할 자리가 없으면 처음부터");

        var name = $"메타 · {kept} 고정 → 정제 → 엑잘" + (essence is null ? "" : $" ({essence.Korean})");
        var needRoll = true;
        return Run(name, [.. steps], item, uses =>
        {
            // 1단계: 고정할 쪽 목표가 다 나오고 반대쪽에 고정 옵션 자리가 생길 때까지 굴립니다.
            if (needRoll)
            {
                for (var spins = 0; ; spins++)
                {
                    if (spins > 20_000) return null;
                    Use(uses, essence?.Id ?? Currencies.Chaos);
                    RollRare(item, forced);
                    var (whole, b) = Check(item, allowBench);
                    if (whole) return b;
                    if (SideDone(item, lockPrefixes) && item.HasRoom(!lockPrefixes)) break;
                }
                needRoll = false;
            }

            // 2단계: 고정 → 정제 (반대쪽 옵션이 모두 지워짐)
            foreach (var (cur, n) in lockCost) Use(uses, cur, n);
            Use(uses, Currencies.Scour);
            item.Mods.RemoveAll(m => m.Prefix != lockPrefixes && Array.IndexOf(item.Fixed, m) < 0);

            // 3단계: 엑잘로 채우기 (아직 채울 자리가 남았을 때만)
            while (item.Mods.Count < 6 && HasRoomForMissing(item))
            {
                Use(uses, Currencies.Exalt);
                Add(item);
                var (ok, b) = Check(item, allowBench);
                if (ok) { needRoll = true; return b; }
            }
            // 반대쪽에 고정할 자리가 남았으면 다음 시도에서 다시 고정, 없으면 처음부터
            if (!item.HasRoom(!lockPrefixes)) { Use(uses, Currencies.Scour); needRoll = true; }
            return null;
        }, 400, 200_000, cancel);
    }

    /// <summary>아직 못 채운 목표가 들어갈 접두·접미 자리가 남았는지</summary>
    bool HasRoomForMissing(CraftItem item)
    {
        int needP = 0, needS = 0;
        Span<bool> used = stackalloc bool[6];
        for (var t = 0; t < targets.Length; t++)
        {
            var hit = false;
            for (var i = 0; i < item.Mods.Count; i++)
            {
                if (used[i] || !targets[t].Matches(item.Mods[i])) continue;
                used[i] = hit = true;
                break;
            }
            if (hit) continue;
            if (targets[t].Prefix) needP++; else needS++;
        }
        return 3 - item.Prefixes >= needP && 3 - item.Suffixes >= needS;
    }
}

// ───────── 화면용 도우미 ─────────

/// <summary>옵션 종류 하나 (검색 목록). Label: 티어 1 문장의 수치를 #으로 바꾼 것</summary>
public sealed record CraftModType(string Type, bool Prefix, string Label, CraftMod[] Tiers, bool EssenceOnly, int Weight, bool Veiled = false, bool BenchOnly = false, bool FossilOnly = false);

public static partial class CraftHelpers
{
    [GeneratedRegex(@"\(?-?\d+(\.\d+)?(-\d+(\.\d+)?)?\)?")]
    private static partial Regex Numbers();

    public static string Template(string text) => Numbers().Replace(text, "#");

    /// <summary>옵션 문장의 수치만 모아서: "생명력 최대치 +(175-189)" → "175-189", 여러 개면 " / "로</summary>
    public static string Values(string text) =>
        string.Join(" / ", Numbers().Matches(text).Select(m => m.Value.Trim('(', ')')));

    /// <summary>풀과 이 분류에 쓸 수 있는 에센스로 고를 수 있는 옵션 종류를 만듭니다.</summary>
    public static List<CraftModType> Types(CraftData data, ModPool pool, IEnumerable<CraftEssence> essences)
    {
        var list = new List<CraftModType>();
        var byType = pool.Mods.Select((m, i) => (m, w: pool.Weights[i]))
            .GroupBy(x => (x.m.Type, x.m.Prefix));
        var seen = new HashSet<(string, bool)>();
        foreach (var g in byType)
        {
            var tiers = g.OrderByDescending(x => x.m.Level).Select(x => x.m).ToArray();
            list.Add(new CraftModType(g.Key.Type, g.Key.Prefix, Template(tiers[0].Text), tiers, false, g.Sum(x => x.w)));
            seen.Add(g.Key);
        }
        var essenceMods = essences
            .Select(e => e.Mods.TryGetValue(pool.Base.Class, out var id) && data.Mods.TryGetValue(id, out var m) ? m : null)
            .OfType<CraftMod>()
            .Where(m => !seen.Contains((m.Type, m.Prefix)))
            .GroupBy(m => (m.Type, m.Prefix));
        foreach (var g in essenceMods)
        {
            var tiers = g.Distinct().OrderByDescending(m => m.V.Length > 0 ? Math.Abs(m.V[0][0]) : 0).ToArray();
            list.Add(new CraftModType(g.Key.Type, g.Key.Prefix, Template(tiers[0].Text), tiers, true, 0));
        }
        // 화석 전용 옵션: 공허 화석의 심연 홈처럼 화석이 확정으로 붙이는 것
        foreach (var f in data.Fossils.Where(f => f.Forced is { Length: > 0 } && f.UsableOn(pool.Base)))
            foreach (var m in f.Forced!.Select(id => data.Mods.GetValueOrDefault(id)).OfType<CraftMod>())
                if (!list.Any(t => t.Type == m.Type && t.Prefix == m.Prefix))
                    list.Add(new CraftModType(m.Type, m.Prefix, Template(m.Text), [m], false, 0, FossilOnly: true));

        // 작업대 전용 옵션: 일반·에센스로 안 나오고 작업대로만 붙는 종류 (예: 2배 피해)
        var benchMods = data.Bench
            .Where(b => Array.IndexOf(b.Classes, pool.Base.Class) >= 0)
            .Select(b => data.Mods.GetValueOrDefault(b.Mod))
            .OfType<CraftMod>()
            .Where(m => !list.Any(t => t.Type == m.Type && t.Prefix == m.Prefix))
            .GroupBy(m => (m.Type, m.Prefix));
        foreach (var g in benchMods)
        {
            var tiers = g.Distinct().OrderByDescending(m => m.Level).ToArray();
            list.Add(new CraftModType(g.Key.Type, g.Key.Prefix, Template(tiers[0].Text), tiers, false, 0, BenchOnly: true));
        }

        // 베일드 옵션: 일반·에센스로 안 나오는 종류만
        foreach (var g in pool.Veiled.GroupBy(m => (m.Type, m.Prefix)))
        {
            if (list.Any(t => t.Type == g.Key.Type && t.Prefix == g.Key.Prefix)) continue;
            var tiers = g.OrderByDescending(m => m.Level).ToArray();
            list.Add(new CraftModType(g.Key.Type, g.Key.Prefix, Template(tiers[0].Text), tiers, false, 0, Veiled: true));
        }
        return list.OrderBy(t => t.Prefix ? 0 : 1).ThenByDescending(t => t.Weight).ToList();
    }
}

// ───────── 게임에서 복사한 아이템 텍스트 읽기 ─────────

/// <summary>붙여넣은 아이템에서 읽은 것: 베이스, 아이템 레벨, 영향력, 옵션 종류와 티어(1부터), 못 읽은 줄</summary>
public sealed record ParsedItem(CraftBase? Base, int? ItemLevel, List<string> Influences, string[] ModLines, HashSet<string> Fractured);

public static partial class CraftItemText
{
    [GeneratedRegex(@"\(([+-]?\d+(?:\.\d+)?)-([+-]?\d+(?:\.\d+)?)\)|[+-]?\d+(?:\.\d+)?")]
    private static partial Regex NumberToken();

    [GeneratedRegex(@"(?<=\d)\(-?\d+(?:\.\d+)?--?\d+(?:\.\d+)?\)")]
    private static partial Regex RangeSuffix();

    [GeneratedRegex(@"^(?:아이템 레벨|Item Level):\s*(\d+)", RegexOptions.Multiline)]
    private static partial Regex ItemLevelLine();

    static readonly string[] FracturedMarks = ["(fractured)", "(균열)", "(균열됨)"];

    static readonly (string Tag, string[] Words)[] InfluenceWords =
    [
        ("shaper", ["쉐이퍼 아이템", "Shaper Item"]),
        ("elder", ["엘더 아이템", "Elder Item"]),
        ("crusader", ["성전사 아이템", "Crusader Item"]),
        ("eyrie", ["구원자 아이템", "Redeemer Item"]),
        ("basilisk", ["사냥꾼 아이템", "Hunter Item"]),
        ("adjudicator", ["전쟁군주 아이템", "Warlord Item"]),
    ];

    /// <summary>
    /// 한글·영어 클라이언트에서 Ctrl+C(또는 Ctrl+Alt+C)로 복사한 텍스트, 또는 Path of Building에서 복사한 아이템 텍스트.
    /// PoB 형식은 구분선이 없고, "Implicits: N" 다음 N줄이 고정 옵션이며, 줄 앞에 {crafted}·{crucible} 같은 표시가 붙습니다.
    /// </summary>
    public static ParsedItem Parse(CraftData data, string text)
    {
        // Ctrl+Alt+C는 숫자 뒤에 "(100-114)"처럼 범위를 붙여서 지웁니다.
        var lines = text.Replace("\r", "").Split('\n')
            .Select(l => RangeSuffix().Replace(l, "").Trim())
            .Where(l => l != "").ToArray();
        if (!lines.Any(l => l.StartsWith("----"))) lines = FromPathOfBuilding(lines);
        // 이름 부분(첫 구분선 전)에 들어 있는 가장 긴 베이스 이름 (매직은 앞뒤에 접사가 붙음)
        var header = lines.TakeWhile(l => !l.StartsWith("----")).ToArray();
        var found = data.Bases
            .SelectMany(b => new[] { (b, name: b.Korean), (b, name: b.Name) })
            .Where(x => header.Any(h => h.Contains(x.name, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.name.Length)
            .Select(x => x.b)
            .FirstOrDefault();

        var level = ItemLevelLine().Match(text) is { Success: true } m ? int.Parse(m.Groups[1].Value) : (int?)null;
        var influences = InfluenceWords.Where(x => x.Words.Any(w => lines.Contains(w))).Select(x => x.Tag).ToList();

        // 옵션 줄: 이름 부분과 {…} 설명 줄, 작업대·고정·인챈트 표시가 붙은 줄은 뺍니다.
        var modLines = lines.Skip(header.Length)
            .Where(l => !l.StartsWith("----") && !l.StartsWith('{') && !l.Contains(':'))
            .Where(l => !InfluenceWords.Any(x => x.Words.Contains(l)))
            .Where(l => !l.EndsWith("(implicit)") && !l.EndsWith("(enchant)"))
            .Select(l => l.EndsWith("(crafted)") ? l[..^"(crafted)".Length].Trim() : l)
            .Where(l => l is not ("Fractured Item" or "Mirrored" or "Corrupted" or "Searing Exarch Item" or "Eater of Worlds Item" or "균열된 아이템" or "복제됨" or "타락"))
            .ToList();
        // 균열 옵션: 게임 복사는 "(fractured)", PoB는 {fractured} 표시 (FromPathOfBuilding에서 " (fractured)"로 바꿔 둠)
        var fractured = new HashSet<string>();
        for (var i = 0; i < modLines.Count; i++)
        {
            foreach (var mark in FracturedMarks)
            {
                if (!modLines[i].EndsWith(mark)) continue;
                modLines[i] = modLines[i][..^mark.Length].Trim();
                fractured.Add(modLines[i]);
            }
        }
        return new ParsedItem(found, level, influences, [.. modLines], fractured);
    }

    /// <summary>
    /// PoB 아이템 텍스트를 게임 복사 형식처럼 바꿉니다: 이름 부분 뒤에 구분선을 넣고, 고정 옵션(Implicits: N 다음 N줄)과
    /// 작업대·도가니·인챈트 등 크래프팅으로 만들 수 없는 줄은 빼고, 남은 줄의 {…} 표시는 지웁니다 ({fractured}는 일반 옵션으로 둠).
    /// </summary>
    static string[] FromPathOfBuilding(string[] lines)
    {
        var result = new List<string>();
        var i = 0;
        // 이름 부분: "키: 값" 줄이 처음 나오기 전까지 (Rarity: 줄은 이름 부분에 둡니다)
        for (; i < lines.Length && (!lines[i].Contains(':') || lines[i].StartsWith("Rarity:")); i++) result.Add(lines[i]);
        result.Add("--------");
        var skip = 0;
        for (; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("Implicits:"))
            {
                int.TryParse(line["Implicits:".Length..].Trim(), out skip);
                continue;
            }
            if (skip > 0) { skip--; continue; }
            if (line.Contains(':')) { result.Add(line); continue; }  // Item Level 등
            var tags = PobTag().Matches(line).Select(m => m.Groups[1].Value).ToArray();
            // {crafted} 옵션은 남겨서 작업대 목표로 씁니다 (고정 옵션 칸의 작업대 옵션은 위에서 이미 건너뜀).
            if (tags.Any(t => t is "crucible" or "enchant" or "implicit" or "scourge" or "exarch" or "eater" or "synthesis"))
                continue;
            result.Add(PobTag().Replace(line, "") + (tags.Contains("fractured") ? " (fractured)" : ""));
        }
        return [.. result];
    }

    [GeneratedRegex(@"\{(\w+)[^}]*\}")]
    private static partial Regex PobTag();

    /// <summary>옵션 문장 → 숫자 자리를 잡는 정규식 (수치 범위 "(a-b)"와 숫자를 모두 숫자 하나로)</summary>
    static Regex Pattern(string text)
    {
        var parts = new List<string>();
        var last = 0;
        foreach (Match t in NumberToken().Matches(text))
        {
            parts.Add(Regex.Escape(text[last..t.Index]));
            parts.Add(@"([+-]?\d+(?:\.\d+)?)");
            last = t.Index + t.Length;
        }
        parts.Add(Regex.Escape(text[last..]));
        return new Regex("^" + string.Join("", parts).Replace(@"\+([+-]", "([+-]") + "$");
    }

    /// <summary>옵션 문장의 숫자 자리마다 (최소, 최대)</summary>
    static List<(double Lo, double Hi)> Ranges(string text) =>
        NumberToken().Matches(text).Select(t => t.Groups[1].Success
            ? (double.Parse(t.Groups[1].Value), double.Parse(t.Groups[2].Value))
            : (double.Parse(t.Value), double.Parse(t.Value))).ToList();

    /// <summary>
    /// 옵션 줄을 옵션 종류에 맞춰 봅니다. 같은 종류의 티어는 숫자만 달라서 1티어 문장으로 맞추고,
    /// 읽은 숫자가 모두 범위 안에 드는 가장 좋은 티어를 고릅니다 (없으면 가장 가까운 티어).
    /// 반환: (종류, 티어 1부터) 목록과 못 읽은 줄
    /// </summary>
    public static (List<(CraftModType Type, int Tier, string[] Lines)> Found, List<string> Unknown) MatchMods(IEnumerable<CraftModType> types, string[] lines)
    {
        var found = new List<(CraftModType, int, string[])>();
        var used = new bool[lines.Length];
        // 여러 줄짜리 옵션을 먼저 맞춰야 한 줄짜리가 그 줄을 가로채지 않습니다.
        var candidates = types
            .SelectMany(t => new[] { (t, text: t.Tiers[0].Ko), (t, text: t.Tiers[0].En) })
            .Where(x => !string.IsNullOrEmpty(x.text))
            .Select(x => (x.t, x.text, lines: x.text.Split('\n').Length))
            .OrderByDescending(x => x.lines)
            .ThenBy(x => IsNatural(x.t) ? 0 : 1)  // 같은 문장이면 일반 옵션부터
            .ToList();

        foreach (var (type, text, count) in candidates)
        {
            if (found.Any(f => f.Item1 == type)) continue;
            var korean = text == type.Tiers[0].Ko;
            var pattern = Pattern(text);
            for (var i = 0; i + count <= lines.Length; i++)
            {
                if (Enumerable.Range(i, count).Any(j => used[j])) continue;
                var match = pattern.Match(string.Join("\n", lines[i..(i + count)]));
                if (!match.Success) continue;
                var values = match.Groups.Cast<Group>().Skip(1).Select(g => double.Parse(g.Value)).ToArray();
                var (tier, miss) = BestTier(type, values, korean);
                // 여러 줄짜리나 일반으로 안 나오는 옵션은 수치가 범위에서 크게 벗어나면 다른 옵션 줄이 우연히 맞은 것으로 봅니다
                // (예: "에너지 보호막 98% 증가"와 "생명력 +140"이 작업대 혼합 옵션 "18~21% / 14~16"으로 묶이는 것).
                // 한 줄짜리 일반 옵션은 같은 능력치가 합쳐져 범위를 넘을 수 있어서 가장 가까운 티어로 둡니다.
                if ((count > 1 || !IsNatural(type)) && miss > 0.35) continue;
                found.Add((type, tier, lines[i..(i + count)]));
                for (var j = i; j < i + count; j++) used[j] = true;
                break;
            }
        }
        var unknown = lines.Where((_, i) => !used[i]).ToList();
        return (found, unknown);
    }

    static bool IsNatural(CraftModType t) => !t.EssenceOnly && !t.Veiled && !t.BenchOnly && !t.FossilOnly;

    /// <summary>가장 잘 맞는 티어와, 그 티어 범위에서 벗어난 정도(범위 대비 비율, 0이면 범위 안)</summary>
    static (int Tier, double Miss) BestTier(CraftModType type, double[] values, bool korean)
    {
        var bestTier = 1;
        var bestMiss = double.MaxValue;
        for (var i = 0; i < type.Tiers.Length; i++)
        {
            var ranges = Ranges(korean ? type.Tiers[i].Ko : type.Tiers[i].En);
            if (ranges.Count != values.Length) continue;
            double miss = 0;
            for (var k = 0; k < values.Length; k++)
            {
                var v = Math.Abs(values[k]);
                var (lo, hi) = (Math.Min(Math.Abs(ranges[k].Lo), Math.Abs(ranges[k].Hi)), Math.Max(Math.Abs(ranges[k].Lo), Math.Abs(ranges[k].Hi)));
                var off = v < lo ? (lo - v) / Math.Max(lo, 1) : v > hi ? (v - hi) / Math.Max(hi, 1) : 0;
                miss = Math.Max(miss, off);
            }
            if (miss == 0) return (i + 1, 0);
            if (miss < bestMiss) (bestMiss, bestTier) = (miss, i + 1);
        }
        return (bestTier, bestMiss);
    }
}
