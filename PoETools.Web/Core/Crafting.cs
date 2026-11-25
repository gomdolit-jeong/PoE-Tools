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

/// <summary>화석: Pos 태그는 가중치 배율(%), Neg 태그는 막음</summary>
public sealed record CraftFossil(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("k")] string Korean,
    [property: JsonPropertyName("pos")] List<List<object>> Pos,
    [property: JsonPropertyName("neg")] string[] Neg);

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

    public Task<CraftData> Load() => loading ??= Fetch();

    async Task<CraftData> Fetch()
    {
        var data = await http.GetFromJsonAsync<CraftData>("data/crafting.json")
                   ?? throw new InvalidOperationException("crafting.json이 비어 있어요");
        foreach (var (id, mod) in data.Mods) mod.Id = id;
        return data;
    }
}

// ───────── 화폐 ─────────

public sealed record Currency(string Id, string Korean, double DefaultPrice);

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

    /// <summary>카오스 기준 기본 가격 (리그마다 달라서 화면에서 고칠 수 있음)</summary>
    public static readonly Currency[] All =
    [
        new(Chaos, "카오스 오브", 1),
        new(Alteration, "변화의 오브", 0.15),
        new(Augment, "확장의 오브", 0.1),
        new(Transmute, "변환의 오브", 0.05),
        new(Regal, "제왕의 오브", 0.5),
        new(Alchemy, "연금술의 오브", 0.3),
        new(Scour, "정제의 오브", 0.5),
        new(Exalt, "엑잘티드 오브", 15),
        new(Divine, "신성한 오브", 150),
        new(P + "CurrencyCorrupt", "바알 오브", 1),
        new(P + "CurrencyRerollImplicit", "축복의 오브", 0.5),
        new(P + "CurrencyRerollDefences", "신성화의 오브", 10),
        new(P + "CurrencyFlaskQuality", "유리공의 방울", 0.1),
        new(P + "CurrencyArmourQuality", "방어구 장인의 고철", 0.02),
        new(P + "CurrencyGemQuality", "젬 세공사의 프리즘", 1),
        new(P + "CurrencyRerollSocketNumbers", "보석공의 오브", 0.1),
        new(P + "CurrencyRerollSocketLinks", "결합의 오브", 0.2),
        new(P + "CurrencyRerollSocketColours", "색채의 오브", 0.05),
        new(P + "CurrencyUpgradeRandomly", "기회의 오브", 0.05),
        new(P + "CurrencyInstillingOrb", "주입의 오브", 1),
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
        foreach (var mod in data.Mods.Values)
        {
            if (mod.Level > itemLevel || mod.W.Count == 0) continue;
            var w = FirstMatch(mod.W, tags) ?? 0;
            if (w <= 0) continue;
            var g = FirstMatch(mod.Gw, tags);
            if (g is { } gm) w = w * gm / 100;
            if (w <= 0) continue;
            mods.Add(mod);
            weights.Add(w);
        }
        Mods = [.. mods];
        Weights = [.. weights];
        (cumulative, Total) = Sum(Weights);
    }

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

    public void Clear(bool rare) { Mods.Clear(); Rare = rare; }
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
    string? Note)
{
    public bool Found => Successes > 0;
}

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

    public bool CanBench(int target) => bench[target] is not null;

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
        if (missing < 0) return (true, -1);
        var mod = bench[missing]!.Value.Mod;
        return item.CanAdd(mod) ? (true, missing) : (false, -1);
    }

    /// <summary>모든 목표가 아이템에 이미 붙은 옵션과 겹치지 않는지 (매직 단계에서 쓰레기 옵션 판정)</summary>
    bool IsTarget(CraftMod mod) => targets.Any(t => t.Matches(mod));

    void RollRare(CraftItem item, CraftMod? forced)
    {
        item.Clear(rare: true);
        if (forced is not null) item.Mods.Add(forced);
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
    async Task<CraftResult> Run(string name, string[] steps, Func<Dictionary<string, double>, int?> tryOnce,
        int maxSuccesses, long maxTries, CancellationToken cancel, string? note = null)
    {
        var uses = new Dictionary<string, double>();
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
            }
            if ((tries & 63) != 0) continue;
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
        return new CraftResult(name, steps, (double)successes / Math.Max(1, tries), successes, tries, perSuccess, cost, note);
    }

    static void Use(Dictionary<string, double> uses, string currency, double n = 1) =>
        uses[currency] = uses.GetValueOrDefault(currency) + n;

    string BenchStep(bool allowBench) =>
        allowBench && targets.Select((_, i) => i).Any(CanBench) ? "모자란 옵션 하나는 작업대로 붙이기 (자리가 있을 때)" : "";

    public Task<CraftResult> ChaosSpam(bool allowBench, CancellationToken cancel)
    {
        var item = new CraftItem();
        string[] steps = ["레어 아이템에 카오스 오브를 원하는 옵션이 나올 때까지 사용", BenchStep(allowBench)];
        return Run("카오스 스팸", steps.Where(s => s != "").ToArray(), uses =>
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
        var item = new CraftItem();
        var id = essence.Id;
        string[] steps = [$"{essence.Korean}을(를) 원하는 옵션이 나올 때까지 사용 (확정: {forced.Text})", BenchStep(allowBench)];
        return Run($"에센스 스팸 · {essence.Korean}", steps.Where(s => s != "").ToArray(), uses =>
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
        var item = new CraftItem();
        var names = string.Join(" + ", fossils.Select(f => f.Korean));
        string[] steps = [$"{resonator.Korean}에 {names}을(를) 넣고 원하는 옵션이 나올 때까지 사용", BenchStep(allowBench)];
        return Run($"화석 · {names}", steps.Where(s => s != "").ToArray(), uses =>
        {
            Use(uses, resonator.Id);
            foreach (var f in fossils) Use(uses, f.Id);
            RollRare(item, null);
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
        var item = new CraftItem();
        var wantPrefix = targets.Any(t => t.Prefix);
        var wantSuffix = targets.Any(t => !t.Prefix);
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
        return Run(useExalts ? "변화 → 제왕 → 엑잘" : "변화 → 제왕", [.. steps], uses =>
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
                if (spins > 50_000) return null;
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
public sealed record CraftModType(string Type, bool Prefix, string Label, CraftMod[] Tiers, bool EssenceOnly, int Weight);

public static partial class CraftHelpers
{
    [GeneratedRegex(@"\(?-?\d+(\.\d+)?(-\d+(\.\d+)?)?\)?")]
    private static partial Regex Numbers();

    public static string Template(string text) => Numbers().Replace(text, "#");

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
        return list.OrderBy(t => t.Prefix ? 0 : 1).ThenByDescending(t => t.Weight).ToList();
    }
}

// ───────── 게임에서 복사한 아이템 텍스트 읽기 ─────────

/// <summary>붙여넣은 아이템에서 읽은 것: 베이스, 아이템 레벨, 영향력, 옵션 종류와 티어(1부터), 못 읽은 줄</summary>
public sealed record ParsedItem(CraftBase? Base, int? ItemLevel, List<string> Influences, string[] ModLines);

public static partial class CraftItemText
{
    [GeneratedRegex(@"\(([+-]?\d+(?:\.\d+)?)-([+-]?\d+(?:\.\d+)?)\)|[+-]?\d+(?:\.\d+)?")]
    private static partial Regex NumberToken();

    [GeneratedRegex(@"(?<=\d)\(-?\d+(?:\.\d+)?--?\d+(?:\.\d+)?\)")]
    private static partial Regex RangeSuffix();

    [GeneratedRegex(@"^(?:아이템 레벨|Item Level):\s*(\d+)", RegexOptions.Multiline)]
    private static partial Regex ItemLevelLine();

    static readonly (string Tag, string[] Words)[] InfluenceWords =
    [
        ("shaper", ["쉐이퍼 아이템", "Shaper Item"]),
        ("elder", ["엘더 아이템", "Elder Item"]),
        ("crusader", ["성전사 아이템", "Crusader Item"]),
        ("eyrie", ["구원자 아이템", "Redeemer Item"]),
        ("basilisk", ["사냥꾼 아이템", "Hunter Item"]),
        ("adjudicator", ["전쟁군주 아이템", "Warlord Item"]),
    ];

    /// <summary>한글·영어 클라이언트에서 Ctrl+C(또는 Ctrl+Alt+C)로 복사한 텍스트</summary>
    public static ParsedItem Parse(CraftData data, string text)
    {
        // Ctrl+Alt+C는 숫자 뒤에 "(100-114)"처럼 범위를 붙여서 지웁니다.
        var lines = text.Replace("\r", "").Split('\n')
            .Select(l => RangeSuffix().Replace(l, "").Trim())
            .Where(l => l != "").ToArray();
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
            .Where(l => !l.EndsWith("(crafted)") && !l.EndsWith("(implicit)") && !l.EndsWith("(enchant)") && !l.EndsWith("(fractured)"))
            .ToArray();
        return new ParsedItem(found, level, influences, modLines);
    }

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
    public static (List<(CraftModType Type, int Tier)> Found, List<string> Unknown) MatchMods(IEnumerable<CraftModType> types, string[] lines)
    {
        var found = new List<(CraftModType, int)>();
        var used = new bool[lines.Length];
        // 여러 줄짜리 옵션을 먼저 맞춰야 한 줄짜리가 그 줄을 가로채지 않습니다.
        var candidates = types
            .SelectMany(t => new[] { (t, text: t.Tiers[0].Ko), (t, text: t.Tiers[0].En) })
            .Where(x => !string.IsNullOrEmpty(x.text))
            .Select(x => (x.t, x.text, lines: x.text.Split('\n').Length))
            .OrderByDescending(x => x.lines)
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
                found.Add((type, BestTier(type, values, korean)));
                for (var j = i; j < i + count; j++) used[j] = true;
                break;
            }
        }
        var unknown = lines.Where((_, i) => !used[i]).ToList();
        return (found, unknown);
    }

    static int BestTier(CraftModType type, double[] values, bool korean)
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
                miss += v < lo ? lo - v : v > hi ? v - hi : 0;
            }
            if (miss == 0) return i + 1;
            if (miss < bestMiss) (bestMiss, bestTier) = (miss, i + 1);
        }
        return bestTier;
    }
}
