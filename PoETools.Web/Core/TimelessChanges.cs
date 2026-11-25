using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PoETools.Web.Core;

/// <summary>설명 틀의 {k} 자리: I 굴림 번호(1부터), D 나눌 값, M·X 보여 줄 단위의 최소·최대 (굴림이 없을 때는 M)</summary>
public sealed record LegionSlot(int I, double D, double M, double X);

/// <summary>
/// 군단 주얼이 바꿔 넣는 패시브(nodes)나 덧붙이는 능력치(additions) 하나 (legion.json, tools/build_timeless_luts.py).
/// En·Ko는 "{0}" 자리에 값을 넣는 설명 틀이고, 한글이 없으면 Ko·Kn이 null입니다.
/// </summary>
public sealed record LegionEntry(string Id, string? N, string? Kn, string[] En, string[]? Ko, LegionSlot[] V, bool Ks, bool Nt)
{
    public string Display => Kn ?? N ?? Id;

    /// <summary>굴림값(rolls[0]은 id라서 1부터 씀)을 넣은 한글(없으면 영어) 설명</summary>
    public string[] Lines(IReadOnlyList<int>? rolls = null, IReadOnlyDictionary<int, int>? fixedRolls = null) =>
        (Ko ?? En).Select(line => Placeholder.Replace(line, m =>
        {
            var index = int.Parse(m.Groups[1].Value);
            if (index >= V.Length) return "#";  // 데이터가 어긋나도 화면이 멈추지 않게 합니다.
            var slot = V[index];
            // 굴림값은 PoB처럼 단위를 바꿔(분당 → 초당 등) 보여 줍니다.
            double value = fixedRolls is not null && fixedRolls.TryGetValue(slot.I, out var f) ? f / slot.D
                : rolls is not null && slot.I < rolls.Count ? rolls[slot.I] / slot.D
                : slot.M;
            return Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
        })).ToArray();

    /// <summary>굴림 자리를 "(최소~최대)"로 보여 주는 설명 (시드 찾기의 패시브 목록)</summary>
    public string[] RangeLines() =>
        (Ko ?? En).Select(line => Placeholder.Replace(line, m =>
        {
            var index = int.Parse(m.Groups[1].Value);
            if (index >= V.Length) return "#";
            var slot = V[index];
            return slot.M == slot.X ? Number(slot.M) : $"({Number(slot.M)}~{Number(slot.X)})";
        })).ToArray();

    static string Number(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

    static readonly Regex Placeholder = new(@"\{(\d+)\}");
}

public sealed record LegionData(LegionEntry[] Nodes, LegionEntry[] Additions);

/// <summary>노드 하나가 어떻게 바뀌는지</summary>
public sealed record NodeChange(TreeNode Node, NodeChangeKind Kind, string Title, string[] Lines);

/// <summary>시드 찾기에서 고를 수 있는 결과 (Id: 변환표 값, 337 미만은 덧붙는 능력치, 이상은 바뀌는 패시브)</summary>
public sealed record SeedOutcome(int Id, string Title, string[] Lines, double PerSeed);

/// <summary>시드 찾기 결과 하나: 점수와 결과별 개수</summary>
public sealed record SeedHit(int Seed, double Score, (SeedOutcome Outcome, int Count)[] Counts);

public enum NodeChangeKind
{
    /// <summary>다른 패시브로 바뀜</summary>
    Replaced,
    /// <summary>원래 효과에 능력치가 더해짐</summary>
    Added,
}

/// <summary>
/// 시드·정복자·소켓에 따른 패시브 변화 계산. PoB PassiveSpec.lua(BuildAllDependsAndPaths의 conqueredBy 부분)와 같은 규칙입니다.
/// 변환표는 소켓별 파일(data/timeless/&lt;주얼&gt;/&lt;소켓&gt;.bin)을 받아 씁니다.
/// </summary>
public sealed class TimelessChanges(HttpClient http)
{
    // PoB Data.lua timelessJewelAdditions: 이보다 작은 값은 덧붙는 능력치, 크거나 같으면 바뀌는 패시브
    const int AdditionCount = 337;

    Task<LegionData?>? legion;
    readonly Dictionary<string, Task<SocketLut?>> luts = [];

    public Task<LegionData?> LegionAsync() => legion ??= LoadLegionAsync();

    async Task<LegionData?> LoadLegionAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<LegionData>("data/timeless/legion.json",
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch
        {
            return null;
        }
    }

    Task<SocketLut?> LutAsync(TimelessJewelType jewel, int socketId)
    {
        var key = $"{jewel.LutName}/{socketId}";
        if (!luts.TryGetValue(key, out var task)) luts[key] = task = LoadLutAsync(key);
        return task;
    }

    async Task<SocketLut?> LoadLutAsync(string key)
    {
        try
        {
            var compressed = await http.GetByteArrayAsync($"data/timeless/{key}.bin");
            using var zlib = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
            using var raw = new MemoryStream();
            await zlib.CopyToAsync(raw);
            return new SocketLut(raw.ToArray());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>소켓 반경 안 노드들의 변화. 데이터를 받지 못하면 null</summary>
    public async Task<NodeChange[]?> ComputeAsync(TimelessTree tree, TreeNode socket, TimelessJewelType jewel, Conqueror conqueror, int seed)
    {
        var legionData = await LegionAsync();
        var lut = await LutAsync(jewel, socket.I);
        if (legionData is null || lut is null) return null;

        var nodesById = legionData.Nodes.ToDictionary(n => n.Id);
        var additionsById = legionData.Additions.ToDictionary(a => a.Id);
        var values = lut.Read(jewel.SeedIndex(seed));
        var vaal = jewel.Faction == "vaal";
        var changes = new List<NodeChange>();

        foreach (var node in tree.InRadius(socket))
        {
            if (node.IsKeystone)
            {
                if (nodesById.TryGetValue($"{jewel.Faction}_keystone_{conqueror.LegionId}", out var keystone))
                    changes.Add(Replace(node, keystone));
            }
            else if (node.IsNotable)
            {
                if (values.TryGetValue(node.I, out var v) && Notable(node, v, vaal, legionData) is { } change)
                    changes.Add(change);
            }
            else if (node.IsSmall)
            {
                if (vaal)
                {
                    // 찬란한 허영심 작은 노드: [바뀌는 패시브 id, 굴림] (PoB는 모든 수치에 같은 굴림을 씁니다)
                    if (values.TryGetValue(node.I, out var v) && v.Length >= 2 && Node(legionData, v[0]) is { } entry)
                    {
                        var rolls = entry.V.ToDictionary(s => s.I, _ => (int)v[1]);
                        changes.Add(new(node, NodeChangeKind.Replaced, entry.Display, entry.Lines(fixedRolls: rolls)));
                    }
                }
                else if (Small(node, jewel.Faction, nodesById, additionsById) is { } change)
                {
                    changes.Add(change);
                }
            }
        }
        return [.. changes];
    }

    /// <summary>
    /// 고른 노드들에서 이 주얼로 나올 수 있는 결과 목록 (모든 시드를 훑어서, 시드당 평균 개수가 많은 순).
    /// 찬란한 허영심이 아닌 주얼의 작은 노드와 핵심 패시브는 시드와 상관없이 같아서 빠집니다.
    /// </summary>
    public async Task<SeedOutcome[]?> OutcomesAsync(TreeNode socket, TimelessJewelType jewel, IReadOnlyCollection<int> nodes)
    {
        var legionData = await LegionAsync();
        var lut = await LutAsync(jewel, socket.I);
        if (legionData is null || lut is null) return null;

        var vaal = jewel.Faction == "vaal";
        var counts = new Dictionary<int, int>();
        lut.Scan(nodes.ToHashSet(), (_, _, v) =>
        {
            foreach (var id in OutcomeIds(v, vaal)) counts[id] = counts.GetValueOrDefault(id) + 1;
        });
        return counts.Select(kv => Outcome(legionData, kv.Key, (double)kv.Value / lut.SeedCount))
            .OfType<SeedOutcome>().OrderByDescending(o => o.PerSeed).ToArray();
    }

    /// <summary>모든 시드에서 (고른 노드마다 나온 결과의 가중치) 합이 높은 순으로 top개</summary>
    public async Task<SeedHit[]?> SearchAsync(TreeNode socket, TimelessJewelType jewel, IReadOnlyCollection<int> nodes,
        IReadOnlyDictionary<int, double> weights, int top = 30)
    {
        var legionData = await LegionAsync();
        var lut = await LutAsync(jewel, socket.I);
        if (legionData is null || lut is null) return null;

        var vaal = jewel.Faction == "vaal";
        var scores = new double[lut.SeedCount];
        var matched = new Dictionary<int, int>[lut.SeedCount];
        lut.Scan(nodes.ToHashSet(), (seedIndex, _, v) =>
        {
            foreach (var id in OutcomeIds(v, vaal))
            {
                if (!weights.TryGetValue(id, out var w) || w == 0) continue;
                scores[seedIndex] += w;
                var m = matched[seedIndex] ??= [];
                m[id] = m.GetValueOrDefault(id) + 1;
            }
        });

        var outcomes = new Dictionary<int, SeedOutcome?>();
        SeedOutcome? Lookup(int id) => outcomes.TryGetValue(id, out var o) ? o : outcomes[id] = Outcome(legionData, id, 0);
        return Enumerable.Range(0, lut.SeedCount)
            .Where(i => scores[i] > 0)
            .OrderByDescending(i => scores[i]).ThenBy(i => i)
            .Take(top)
            .Select(i => new SeedHit(jewel.MinSeed + i * jewel.SeedStep, scores[i],
                (matched[i] ?? []).Select(kv => (Lookup(kv.Key), kv.Value))
                    .Where(x => x.Item1 is not null).Select(x => (x.Item1!, x.Value))
                    .OrderByDescending(x => x.Value).ToArray()))
            .ToArray();
    }

    /// <summary>변환표 값에서 결과 id들 (PoB와 같은 해석). 바알의 완력·유산은 덧붙는 능력치마다 하나씩 셉니다.</summary>
    static IEnumerable<int> OutcomeIds(ushort[] v, bool vaal)
    {
        if (!vaal)
        {
            if (v.Length == 1) yield return v[0];
        }
        else if (v.Length is 2 or 3)
        {
            yield return v[0];
        }
        else if (v.Length is 6 or 8)
        {
            for (var i = 0; i < v.Length / 2; i++) yield return v[i];
        }
    }

    static SeedOutcome? Outcome(LegionData legion, int id, double perSeed)
    {
        if (id >= AdditionCount)
            return Node(legion, id) is { } node ? new(id, node.Display, node.RangeLines(), perSeed) : null;
        var addition = legion.Additions.ElementAtOrDefault(id);
        return addition is null ? null : new(id, "능력치 추가", addition.RangeLines(), perSeed);
    }

    static NodeChange? Notable(TreeNode node, ushort[] v, bool vaal, LegionData legion)
    {
        if (!vaal)
        {
            // 나머지 주얼: 값 하나. 337 이상이면 바뀌는 패시브, 아니면 덧붙는 능력치
            if (v.Length != 1) return null;
            if (v[0] >= AdditionCount)
                return Node(legion, v[0]) is { } entry ? Replace(node, entry) : null;
            var addition = legion.Additions.ElementAtOrDefault(v[0]);
            return addition is null ? null : new(node, NodeChangeKind.Added, node.Display, addition.Lines());
        }

        var rolls = v.Select(x => (int)x).ToArray();
        if (v.Length is 2 or 3)
        {
            // [바뀌는 패시브 id, 굴림1, (굴림2)]
            return Node(legion, v[0]) is { } entry
                ? new(node, NodeChangeKind.Replaced, entry.Display, entry.Lines(rolls))
                : null;
        }
        if (v.Length is 6 or 8)
        {
            // 바알의 완력 / 바알의 유산: 앞 절반은 덧붙는 능력치 id, 뒤 절반은 그 굴림 (같은 능력치는 합칩니다)
            var half = v.Length / 2;
            var bias = v.Take(half).Sum(id => id <= 21 ? 1 : -1);
            var title = legion.Nodes.ElementAtOrDefault(bias >= 0 ? 76 : 77)?.Display ?? (bias >= 0 ? "Might of the Vaal" : "Legacy of the Vaal");
            var sums = new Dictionary<int, int>();
            for (var i = 0; i < half; i++) sums[v[i]] = sums.GetValueOrDefault(v[i]) + v[i + half];
            var lines = sums.SelectMany(kv => legion.Additions.ElementAtOrDefault(kv.Key) is { } add
                ? add.Lines(fixedRolls: add.V.ToDictionary(s => s.I, _ => kv.Value))
                : []).ToArray();
            return new(node, NodeChangeKind.Replaced, title, lines);
        }
        return null;
    }

    /// <summary>찬란한 허영심이 아닌 주얼의 작은 노드 (PoB에 정해진 규칙)</summary>
    static NodeChange? Small(TreeNode node, string faction, Dictionary<string, LegionEntry> nodes, Dictionary<string, LegionEntry> additions)
    {
        string? addId = faction switch
        {
            "karui" => node.IsAttribute ? "karui_attribute_strength" : "karui_small_strength",
            "maraketh" => node.IsAttribute ? "maraketh_attribute_dex" : "maraketh_small_dex",
            "kalguur" => node.IsAttribute ? "kalguuran_attribute_ward" : "kalguuran_small_ward",
            "templar" when !node.IsAttribute => "templar_small_devotion",
            _ => null,
        };
        if (addId is not null)
            return additions.TryGetValue(addId, out var add) ? new(node, NodeChangeKind.Added, "작은 패시브", add.Lines()) : null;

        var replaceId = faction switch
        {
            "templar" => "templar_devotion_node",
            "eternal" => "eternal_small_blank",
            _ => null,
        };
        return replaceId is not null && nodes.TryGetValue(replaceId, out var entry) ? Replace(node, entry) : null;
    }

    static LegionEntry? Node(LegionData legion, int value) => legion.Nodes.ElementAtOrDefault(value - AdditionCount);

    static NodeChange Replace(TreeNode node, LegionEntry entry) => new(node, NodeChangeKind.Replaced, entry.Display, entry.Lines());

    /// <summary>소켓별 변환표 (형식은 tools/build_timeless_luts.py write_socket_file)</summary>
    sealed class SocketLut(byte[] data)
    {
        public int SeedCount => BitConverter.ToUInt16(data, 2);

        /// <summary>모든 시드를 처음부터 차례로 읽으며, 고른 노드의 값마다 visit(시드 순번, 노드 id, 값)을 부릅니다.</summary>
        public void Scan(HashSet<int> wanted, Action<int, int, ushort[]> visit)
        {
            var span = data.AsSpan();
            int count = BitConverter.ToUInt16(span[0..]), seeds = BitConverter.ToUInt16(span[2..]);
            var ids = new int[count];
            for (var i = 0; i < count; i++) ids[i] = BitConverter.ToUInt16(span[(4 + 2 * i)..]);
            var pos = 4 + 2 * count + 4 * seeds;
            for (var seed = 0; seed < seeds; seed++)
            {
                foreach (var id in ids)
                {
                    var length = data[pos++];
                    if (wanted.Contains(id))
                    {
                        var values = new ushort[length];
                        for (var j = 0; j < length; j++) values[j] = BitConverter.ToUInt16(span[(pos + 2 * j)..]);
                        visit(seed, id, values);
                    }
                    pos += 2 * length;
                }
            }
        }

        public Dictionary<int, ushort[]> Read(int seedIndex)
        {
            var span = data.AsSpan();
            int count = BitConverter.ToUInt16(span[0..]), seeds = BitConverter.ToUInt16(span[2..]);
            var ids = new int[count];
            for (var i = 0; i < count; i++) ids[i] = BitConverter.ToUInt16(span[(4 + 2 * i)..]);
            var offsetsStart = 4 + 2 * count;
            var bodyStart = offsetsStart + 4 * seeds;
            if (seedIndex < 0 || seedIndex >= seeds) return [];

            var pos = bodyStart + (int)BitConverter.ToUInt32(span[(offsetsStart + 4 * seedIndex)..]);
            var result = new Dictionary<int, ushort[]>();
            foreach (var id in ids)
            {
                var length = data[pos++];
                var values = new ushort[length];
                for (var j = 0; j < length; j++, pos += 2) values[j] = BitConverter.ToUInt16(span[pos..]);
                result[id] = values;
            }
            return result;
        }
    }
}
