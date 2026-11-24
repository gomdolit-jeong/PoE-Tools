using System.Net.Http.Json;
using System.Text.Json;

namespace PoEBuildGacha.Web.Gacha;

/// <summary>정복자(주얼 변형)와 그 정복자가 주는 핵심 패시브 이름</summary>
public sealed record Conqueror(string Name, string Keystone);

/// <summary>
/// 군단 주얼 한 종류. 문구·시드 범위·정복자는 PoB v2.68.1의 Data/Uniques/jewel.lua 기준이고,
/// 한글 이름은 poedb(kr) 기준입니다. 3.11 이전에만 있던 정복자(Zerphi 등)는 뺐습니다.
/// </summary>
public sealed record TimelessJewelType(
    string Name,
    string Korean,
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
}

public static class TimelessJewels
{
    public static readonly TimelessJewelType[] All =
    [
        new("Glorious Vanity", "찬란한 허영심", "바알", 100, 8000, 1,
            "Bathed in the blood of {0} sacrificed in the name of {1}",
            "Passives in radius are Conquered by the Vaal",
            [new("Doryani", "Corrupted Soul"), new("Xibaqua", "Divine Flesh"), new("Mazcotl", "Immortal Ambition")]),
        new("Lethal Pride", "치명적인 긍지", "카루이", 10000, 18000, 1,
            "Commanded leadership over {0} warriors under {1}",
            "Passives in radius are Conquered by the Karui",
            [new("Kaom", "Strength of Blood"), new("Rakiata", "Tempered by War"), new("Akoya", "Chainbreaker")]),
        new("Brutal Restraint", "잔인한 속박", "마라케스", 500, 8000, 1,
            "Denoted service of {0} dekhara in the akhara of {1}",
            "Passives in radius are Conquered by the Maraketh",
            [new("Asenath", "Dance with Death"), new("Nasima", "Second Sight"), new("Balbala", "The Traitor")]),
        new("Militant Faith", "호전적인 신념", "템플러", 2000, 10000, 1,
            "Carved to glorify {0} new faithful converted by High Templar {1}",
            "Passives in radius are Conquered by the Templars",
            [new("Avarius", "Power of Purpose"), new("Dominus", "Inner Conviction"), new("Maxarius", "Transcendence")]),
        // 고상한 오만의 시드는 20 단위입니다.
        new("Elegant Hubris", "고상한 오만", "영원자", 2000, 160000, 20,
            "Commissioned {0} coins to commemorate {1}",
            "Passives in radius are Conquered by the Eternal Empire",
            [new("Cadiro", "Supreme Decadence"), new("Victario", "Supreme Grandstanding"), new("Caspiro", "Supreme Ostentation")]),
        new("Heroic Tragedy", "영웅적인 비극", "Kalguur", 100, 8000, 1,
            "Remembrancing {0} songworthy deeds by the line of {1}",
            "Passives in radius are Conquered by the Kalguur",
            [new("Vorana", "Black Scythe Training"), new("Uhtred", "Celestial Mathematics"), new("Medved", "The Unbreaking Circle")]),
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

public sealed record TreeNode(int I, double X, double Y, string K, string? N)
{
    public bool IsSocket => K == "S";
    public bool IsStart => K == "C";
    public bool IsKeystone => K == "K";
    public bool IsNotable => K == "N";
}

public sealed record TimelessTreeData(string TreeVersion, Dictionary<string, int> Starts, int[] Sockets, TreeNode[] Nodes, int[][] Edges);

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
