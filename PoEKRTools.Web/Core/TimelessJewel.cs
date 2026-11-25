using System.Net.Http.Json;
using System.Text.Json;

namespace PoEKRTools.Web.Core;

/// <summary>
/// 정복자(주얼 변형)와 그 정복자가 주는 핵심 패시브. Name·Keystone은 PoB에 쓰는 영어 이름입니다.
/// 한글 이름은 poedb(kr) 표기이고, poedb에 한글 표기가 없는 정복자는 Korean이 null입니다.
/// LegionId는 PoB ModParser.lua conquerorList의 id로, 핵심 패시브가 "{Faction}_keystone_{LegionId}"로 바뀝니다.
/// </summary>
public sealed record Conqueror(string Name, string? Korean, string Keystone, string KeystoneKorean, string LegionId)
{
    public string Display => Korean ?? Name;
}

/// <summary>
/// 군단 주얼 한 종류. 문구·시드 범위·정복자는 PoB v2.68.1의 Data/Uniques/jewel.lua 기준이고,
/// 한글 이름은 poedb(kr) 기준입니다. 3.11 이전에만 있던 정복자(Zerphi 등)는 뺐습니다.
/// </summary>
public sealed record TimelessJewelType(
    string Name,
    string Korean,
    // PoB의 정복자 종류 (vaal, karui, maraketh, templar, eternal, kalguur). LegionPassives id 앞부분과 같습니다.
    string Faction,
    int MinSeed,
    int MaxSeed,
    int SeedStep,
    // {0} = 시드, {1} = 정복자 이름
    string SeedLine,
    string ConqueredLine,
    Conqueror[] Conquerors)
{
    public bool IsValidSeed(int seed) => seed >= MinSeed && seed <= MaxSeed && seed % SeedStep == 0;

    /// <summary>
    /// 주얼 그림 (공식 이미지 서버 web.poecdn.com의 게임 아이콘). 파일 이름은 poedb와 같은 게임 자산 이름이고,
    /// 영웅적인 비극은 원본 이름이 ExpeditonCivilization(오타 그대로)입니다.
    /// </summary>
    public string ImageUrl => $"https://web.poecdn.com/image/Art/2DItems/Jewels/{Faction switch
    {
        "vaal" => "VaalCivilization",
        "karui" => "KaruiCivilization",
        "maraketh" => "MarakethCivilization",
        "templar" => "TemplarCivilization",
        "eternal" => "EternalEmpireCivilization",
        _ => "ExpeditonCivilization",
    }}.png";

    /// <summary>변환표 파일 이름 (tools/build_timeless_luts.py가 data/timeless/<이름>/에 만듭니다)</summary>
    public string LutName => Name.Replace(" ", "");

    /// <summary>변환표의 시드 순번 (고상한 오만은 시드/20, PoB readLUT과 같음)</summary>
    public int SeedIndex(int seed) => (seed - MinSeed) / SeedStep;
}

public static class TimelessJewels
{
    public static readonly TimelessJewelType[] All =
    [
        new("Glorious Vanity", "찬란한 허영심", "vaal", 100, 8000, 1,
            "Bathed in the blood of {0} sacrificed in the name of {1}",
            "Passives in radius are Conquered by the Vaal",
            [new("Doryani", "도리아니", "Corrupted Soul", "타락한 영혼", "3"), new("Xibaqua", "지바콰", "Divine Flesh", "신성한 육체", "1"), new("Mazcotl", null, "Immortal Ambition", "불멸의 야망", "2_v2")]),
        new("Lethal Pride", "치명적인 긍지", "karui", 10000, 18000, 1,
            "Commanded leadership over {0} warriors under {1}",
            "Passives in radius are Conquered by the Karui",
            [new("Kaom", "카옴", "Strength of Blood", "피의 힘", "1"), new("Rakiata", "라키아타", "Tempered by War", "전쟁의 담금질", "2"), new("Akoya", "아코야", "Chainbreaker", "사슬 파괴자", "3_v2")]),
        new("Brutal Restraint", "잔인한 속박", "maraketh", 500, 8000, 1,
            "Denoted service of {0} dekhara in the akhara of {1}",
            "Passives in radius are Conquered by the Maraketh",
            [new("Asenath", "아세나스", "Dance with Death", "죽음의 춤", "2"), new("Nasima", null, "Second Sight", "꿰뚫어 보는 눈", "3"), new("Balbala", "발바라", "The Traitor", "배반자", "1_v2")]),
        new("Militant Faith", "호전적인 신념", "templar", 2000, 10000, 1,
            "Carved to glorify {0} new faithful converted by High Templar {1}",
            "Passives in radius are Conquered by the Templars",
            [new("Avarius", "아배리우스", "Power of Purpose", "의지력의 힘", "3"), new("Dominus", "도미누스", "Inner Conviction", "내면의 신념", "2"), new("Maxarius", "막사리우스", "Transcendence", "초월", "1_v2")]),
        // 고상한 오만의 시드는 20 단위입니다.
        new("Elegant Hubris", "고상한 오만", "eternal", 2000, 160000, 20,
            "Commissioned {0} coins to commemorate {1}",
            "Passives in radius are Conquered by the Eternal Empire",
            [new("Cadiro", "카디로", "Supreme Decadence", "타락의 정점", "1"), new("Victario", "빅타리오", "Supreme Grandstanding", "과시의 정점", "2"), new("Caspiro", null, "Supreme Ostentation", "허영의 정점", "3_v2")]),
        new("Heroic Tragedy", "영웅적인 비극", "kalguur", 100, 8000, 1,
            "Remembrancing {0} songworthy deeds by the line of {1}",
            "Passives in radius are Conquered by the Kalguur",
            [new("Vorana", "보라나", "Black Scythe Training", "검은 낫의 교육", "1"), new("Uhtred", null, "Celestial Mathematics", "천공의 수학", "2"), new("Medved", null, "The Unbreaking Circle", "부서지지 않는 원", "3")]),
    ];

    /// <summary>PoB 아이템 텍스트 (Items 탭에 그대로 들어갑니다)</summary>
    public static string ItemText(TimelessJewelType jewel, Conqueror conqueror, int seed) =>
        string.Join('\n',
            "Rarity: UNIQUE",
            jewel.Name,
            "Timeless Jewel",
            "League: Legion",
            "Limited to: 1 Historic",
            "Radius: Large",
            "Implicits: 0",
            string.Format(jewel.SeedLine, seed, conqueror.Name),
            jewel.ConqueredLine,
            "Historic");
}

/// <summary>
/// 트리 노드 (timeless-tree.json). K: C 시작점, S 소켓, K 핵심, N 주요, n 작은 노드.
/// G·O: 그룹 번호와 궤도 반지름 (같은 그룹·궤도의 연결선은 호로 그림), Kn·Ks: 한글 이름·설명, A: 힘·민첩·지능 노드
/// </summary>
public sealed record TreeNode(int I, double X, double Y, string K, int G, double O, string? N, string? Kn, string[]? Ks, int? A)
{
    public bool IsSocket => K == "S";
    public bool IsStart => K == "C";
    public bool IsKeystone => K == "K";
    public bool IsNotable => K == "N";
    public bool IsSmall => K == "n";
    public bool IsAttribute => A == 1;
    public string Display => Kn ?? N ?? "";
}

public sealed record TimelessTreeData(string TreeVersion, Dictionary<string, int> Starts, int[] Sockets,
    Dictionary<string, double[]> Groups, TreeNode[] Nodes, int[][] Edges);

/// <summary>
/// 본 패시브 트리(timeless-tree.json, tools/build_timeless_tree.py로 생성)와 소켓까지의 경로 계산.
/// PoB는 소켓이 찍혀 있어야 군단 주얼 변환을 보여 주므로, 직업 시작점에서 소켓까지 가장 짧은 경로를 찍습니다.
/// </summary>
public sealed class TimelessTree
{
    // PoB Data.lua의 3.16 이후 대형 반경
    public const double LargeRadius = 1800;

    public TimelessTreeData Data { get; }
    public IReadOnlyDictionary<int, TreeNode> Nodes { get; }
    public IReadOnlyList<TreeNode> Sockets { get; }
    readonly Dictionary<int, List<int>> links = [];

    TimelessTree(TimelessTreeData data)
    {
        Data = data;
        Nodes = data.Nodes.ToDictionary(n => n.I);
        Sockets = data.Sockets.Select(id => Nodes[id]).ToArray();
        foreach (var edge in data.Edges)
        {
            Link(edge[0], edge[1]);
            Link(edge[1], edge[0]);
        }
    }

    void Link(int from, int to)
    {
        if (!links.TryGetValue(from, out var list)) links[from] = list = [];
        list.Add(to);
    }

    public static async Task<TimelessTree?> LoadAsync(HttpClient http)
    {
        try
        {
            var data = await http.GetFromJsonAsync<TimelessTreeData>("data/timeless-tree.json",
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return data is null ? null : new TimelessTree(data);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>대형 반경 안의 노드 (소켓·시작점 제외, PoB처럼 중심 거리로 판정)</summary>
    public IEnumerable<TreeNode> InRadius(TreeNode socket) =>
        Data.Nodes.Where(n => n != socket && !n.IsStart && !n.IsSocket
            && Square(n.X - socket.X) + Square(n.Y - socket.Y) <= LargeRadius * LargeRadius);

    /// <summary>직업 시작점에서 소켓까지 찍을 노드 (시작점 제외, 소켓 포함). 다른 직업 시작점은 지나가지 않습니다.</summary>
    public int[]? PathTo(string className, int socketId)
    {
        var start = Data.Starts[className];
        var previous = new Dictionary<int, int> { [start] = start };
        var queue = new Queue<int>([start]);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == socketId)
            {
                var path = new List<int>();
                for (var id = socketId; id != start; id = previous[id]) path.Add(id);
                path.Reverse();
                return [.. path];
            }
            foreach (var next in links.GetValueOrDefault(current) ?? [])
            {
                if (previous.ContainsKey(next) || (Nodes[next].IsStart && next != start)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        return null;
    }

    /// <summary>소켓까지 경로가 가장 짧은 직업</summary>
    public string NearestClass(int socketId) =>
        Data.Starts.Keys.MinBy(c => PathTo(c, socketId)?.Length ?? int.MaxValue)!;

    static double Square(double v) => v * v;
}
