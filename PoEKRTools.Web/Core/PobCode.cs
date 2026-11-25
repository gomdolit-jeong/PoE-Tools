using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace PoEKRTools.Web.Core;

/// <summary>
/// 뽑기 결과를 Path of Building 가져오기 코드로 만듭니다.
/// 코드 = PoB 빌드 XML을 zlib으로 압축하고 base64url로 바꾼 것 (PoB ImportTab.lua와 같은 방식).
/// 직업·전직과 주요 스킬 젬(프레시아면 이동기까지)만 들어가고, 패시브 트리와 장비는 비어 있습니다.
/// </summary>
public static class PobCode
{
    // PoB v2.68.1 기준 트리 버전. 일반 전직은 최신 트리(3.30), 프레시아 전직은 "alternate" 트리에 있습니다.
    // PoB 버전을 올릴 때(tools/pob/build.sh의 POB_TAG·KEEP_TREES) 함께 확인합니다.
    // 3.30 트리에서 워든의 내부 id는 "Raider"지만 이름과 순서(레인저 1번)는 그대로입니다.
    public const string TreeVersion = "3_30";
    public const string PhreciaTreeVersion = "3_28_alternate";

    // 트리 데이터(TreeData/<버전>/tree.lua)의 "classes" 순서. 직업 번호는 0부터, 전직 번호는 1부터입니다.
    static readonly string[] ClassOrder = ["Scion", "Marauder", "Ranger", "Witch", "Duelist", "Templar", "Shadow"];

    static readonly Dictionary<string, string[]> AscendancyOrder = new()
    {
        ["Scion"] = ["Ascendant", "Reliquarian", "Luminary"],
        ["Marauder"] = ["Juggernaut", "Berserker", "Chieftain"],
        ["Ranger"] = ["Warden", "Deadeye", "Pathfinder"],
        ["Witch"] = ["Occultist", "Elementalist", "Necromancer"],
        ["Duelist"] = ["Slayer", "Gladiator", "Champion"],
        ["Templar"] = ["Inquisitor", "Hierophant", "Guardian"],
        ["Shadow"] = ["Assassin", "Trickster", "Saboteur"],
    };

    static readonly Dictionary<string, string[]> PhreciaAscendancyOrder = new()
    {
        ["Scion"] = ["Scavenger"],
        ["Marauder"] = ["Ancestral Commander", "Behemoth", "Antiquarian"],
        ["Ranger"] = ["Wildspeaker", "Whisperer", "Daughter of Oshabi"],
        ["Witch"] = ["Harbinger", "Herald", "Bog Shaman"],
        ["Duelist"] = ["Aristocrat", "Gambler", "Paladin"],
        ["Templar"] = ["Architect of Chaos", "Puppeteer", "Polytheist"],
        ["Shadow"] = ["Servant of Arakaali", "Blind Prophet", "Surfcaster"],
    };

    public static string Create(GachaResult result) => Encode(Xml(result));

    /// <summary>
    /// 군단 주얼 하나만 든 빌드. 직업 시작점에서 소켓까지 경로(pathNodes)를 찍고 그 소켓에 주얼을 꽂습니다.
    /// PoB는 찍힌 소켓의 군단 주얼만 트리에 변환을 보여 줍니다 (PassiveSpec.lua의 radiusJewels).
    /// </summary>
    public static string CreateTimeless(string className, TimelessJewelType jewel, Conqueror conqueror, int seed,
        int socketId, IEnumerable<int> pathNodes) => Encode(TimelessXml(className, jewel, conqueror, seed, socketId, pathNodes));

    public static XDocument TimelessXml(string className, TimelessJewelType jewel, Conqueror conqueror, int seed,
        int socketId, IEnumerable<int> pathNodes)
    {
        var classId = Array.IndexOf(ClassOrder, className);
        if (classId < 0) throw new InvalidOperationException($"PoB 트리에 없는 직업입니다: {className}");

        return new XDocument(
            new XElement("PathOfBuilding",
                new XElement("Build",
                    new XAttribute("level", 100),
                    new XAttribute("targetVersion", "3_0"),
                    new XAttribute("className", className),
                    new XAttribute("ascendClassName", "None"),
                    new XAttribute("mainSocketGroup", 1)),
                new XElement("Skills", new XAttribute("activeSkillSet", 1), new XElement("SkillSet", new XAttribute("id", 1))),
                new XElement("Items", new XAttribute("activeItemSet", 1),
                    new XElement("Item", new XAttribute("id", 1), TimelessJewels.ItemText(jewel, conqueror, seed)),
                    new XElement("ItemSet", new XAttribute("id", 1))),
                new XElement("Tree", new XAttribute("activeSpec", 1),
                    new XElement("Spec",
                        new XAttribute("title", $"{jewel.Name} {seed} ({conqueror.Name})"),
                        new XAttribute("treeVersion", TreeVersion),
                        new XAttribute("classId", classId),
                        new XAttribute("ascendClassId", 0),
                        new XAttribute("nodes", string.Join(',', pathNodes)),
                        new XElement("Sockets",
                            new XElement("Socket", new XAttribute("nodeId", socketId), new XAttribute("itemId", 1))))),
                new XElement("Notes", $"PoE KR Tools: {jewel.Name} {seed} / {conqueror.Name} ({conqueror.Keystone})")));
    }

    static string Encode(XDocument xml)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal))
        {
            zlib.Write(Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting)));
        }
        return Convert.ToBase64String(output.ToArray()).Replace('+', '-').Replace('/', '_');
    }

    public static XDocument Xml(GachaResult result)
    {
        var className = result.Class.Name;
        var ascendancies = result.Phrecia ? PhreciaAscendancyOrder[className] : AscendancyOrder[className];
        var classId = Array.IndexOf(ClassOrder, className);
        var ascendClassId = Array.IndexOf(ascendancies, result.Ascendancy.Name) + 1;
        if (classId < 0 || ascendClassId <= 0)
            throw new InvalidOperationException($"PoB 트리에 없는 직업·전직입니다: {className} / {result.Ascendancy.Name}");

        var skills = new XElement("Skills",
            new XAttribute("activeSkillSet", 1),
            new XElement("SkillSet", new XAttribute("id", 1),
                SocketGroup(result.Skill, "Main"),
                result.Movement is { } movement ? SocketGroup(movement, "Movement") : null));

        return new XDocument(
            new XElement("PathOfBuilding",
                new XElement("Build",
                    new XAttribute("level", 1),
                    new XAttribute("targetVersion", "3_0"),
                    new XAttribute("className", className),
                    new XAttribute("ascendClassName", result.Ascendancy.Name),
                    new XAttribute("mainSocketGroup", 1)),
                skills,
                new XElement("Tree", new XAttribute("activeSpec", 1),
                    new XElement("Spec",
                        new XAttribute("title", "PoE KR Tools"),
                        new XAttribute("treeVersion", result.Phrecia ? PhreciaTreeVersion : TreeVersion),
                        new XAttribute("classId", classId),
                        new XAttribute("ascendClassId", ascendClassId),
                        new XAttribute("nodes", ""))),
                new XElement("Notes", $"PoE KR Tools: {result.Ascendancy.Name} / {result.Skill.Name}")));
    }

    static XElement SocketGroup(Skill skill, string label)
    {
        // PoB 젬 ID는 이름의 단어를 붙인 것입니다. 예: "Rain of Arrows" → SkillGemRainOfArrows / RainOfArrows
        var id = string.Concat(skill.Name.Split(' ').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        return new XElement("Skill",
            new XAttribute("enabled", "true"),
            new XAttribute("label", label),
            new XAttribute("includeInFullDPS", label == "Main" ? "true" : "false"),
            new XElement("Gem",
                new XAttribute("nameSpec", skill.Name),
                new XAttribute("skillId", id),
                new XAttribute("gemId", "Metadata/Items/Gems/SkillGem" + id),
                new XAttribute("level", 20),
                new XAttribute("quality", 0),
                new XAttribute("enabled", "true")));
    }
}
