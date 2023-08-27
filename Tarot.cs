using NSDotnet;
using NSDotnet.Models;

using SQLite;

using Spectre.Console;
using Spectre.Console.Rendering;

public partial class Tarot
{
    public readonly static string TAROT_VERSION = "0.9.2";
    public string User { get; private set; }
    // Setup database connection
    SQLiteAsyncConnection Database = new ("DeckDB.db");

    // Delegate for program functions
    delegate Task TarotFunction();

    // Color lookup for rarity
    public static Dictionary<string, Color> colors = new(){
        {"common", Color.White},
        {"uncommon", Color.Green},
        {"rare",Color.Blue},
        {"ultra-rare",Color.Purple},
        {"epic",Color.Orange1},
        {"legendary",Color.Magenta1}
    };

    // Helper Methods
    public static Markup Linkify(string puppet) =>
        new Markup($"[link=https://nationstates.net/container={puppet}/nation={puppet}]{puppet}[/]");

    public static IRenderable MarkWrap(object item) =>
        new Markup($"{item}");

    public static List<IRenderable> MarkWrapMany(params object[] items) =>
        items.Select(I => (IRenderable)(new Markup($"{I}"))).ToList();

    public static async Task<BreakdownChart> GenerateBreakdown(DeckViewEntry[] Cards)
    {
        var chart = new BreakdownChart()
            .Width(10)
            .HideTags()
            .HideTagValues();
        foreach(var set in Cards.OrderBy(C=>C.RarityInt).GroupBy(C=>C.RarityInt))
        {
            string rarity = set.First().Rarity;
            chart.AddItem(rarity, set.Count(), colors[rarity]);
        }
        return chart;
    }

    // Main menu
    public async Task<int> Execute()
    {
        AnsiConsole.MarkupLine("[red]ooooooooooo   o      oooooooooo    ooooooo   ooooooooooo\n88  888  88  888      888    888 o888   888o 88  888  88 \n    888     8  88     888oooo88  888     888     888     \n    888    8oooo88    888  88o   888o   o888     888     \n   o888o o88o  o888o o888o  88o8   88ooo88      o888o[/]");
        User = AnsiConsole.Ask<string>("Main Nation: ");
        NSAPI.Instance.UserAgent = $"Tarot/{TAROT_VERSION} (By Vleerian, vleerian@hotmail.com in use by {User})";

        await Menu(new Dictionary<string, TarotFunction>() {
            {"Fetch Puppet Info", GetPuppetInfo}, {"List Puppets", ListPuppets},
            {"Find Legendaries", FindLegendaries}, {"Generate Issue Links", Issues_Links},
            {"Generate Pack Links", Pack_Links}, {"Find Owners", FindOwner},
            {"Junker", Junker}, {"Fetch Deck Info", GetCardInfo},
            {"Config", ConfigMenu}, {"Exit", null}
        });

        return 0;
    }

    // Config submenu
    Task ConfigMenu() => Menu(new Dictionary<string, TarotFunction>() {
        {"Add Puppets",AddPuppets}, {"Generate Puppet Links", Puppet_Links},
        {"Create Database",CreateCardsDB}, {"Regenerate Views", CreateViews},
        {"Back", null}
    });

    async Task Menu(Dictionary<string, TarotFunction> MenuOptions)
    {
        while(true)
        {
            var Operation = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Select Function")
                .AddChoices(MenuOptions.Keys));
            if(MenuOptions[Operation] == null)
                return;
            await MenuOptions[Operation]();
        }
    }
}