using System.Text;
using System.Xml.Linq;

namespace ConstantLearning.Tests.Content.References;

internal static class ReferenceFixtures
{
    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ConstantLearning.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Не знайдено корінь тестових зразків.");
        }
    }

    public static string Read(string name) => File.ReadAllText(Path.Combine(Root,
        "ConstantLearning.Tests", "Content", "References", "Fixtures", name + ".wikitext"));

    public static Stream Dump(params (string Lemma, string Text, string Revision)[] pages) => new MemoryStream(Encoding.UTF8.GetBytes(
        new XElement("mediawiki", pages.Select(page => new XElement("page",
            new XElement("title", page.Lemma), new XElement("ns", "0"),
            new XElement("revision", new XElement("id", page.Revision), new XElement("text", page.Text))))).ToString()));
}
