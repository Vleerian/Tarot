using NSDotnet;
using NSDotnet.Models;

using SQLite;

using Spectre.Console;
using Spectre.Console.Rendering;

public partial class Tarot
{
    public readonly static string TAROT_VERSION = "0.7.1";
    // Setup database connection
    SQLiteAsyncConnection Database = new ("DeckDB.db");
    // List of owners used across the program
    string[] Owners = null;

    // Delegate for program functions
    delegate Task TarotFunction(string[] Users);

    // Color lookup for rarity
    public static Dictionary<string, Color> colors = new(){
        {"common", Color.White},
        {"uncommon", Color.Green},
        {"rare",Color.Blue},
        {"ultra-rare",Color.Purple},
        {"epic",Color.Orange1},
        {"legendary",Color.Magenta1}
    };

    // Main Menu setup
    Dictionary<string, TarotFunction> Functions;
    SelectionPrompt<string> MainMenu;
    // Setup menu setup
    Dictionary<string, TarotFunction> SetupFunctions;
    SelectionPrompt<string> SetupMenu;

    public Tarot()
    {
        Functions = new() {
            {"Select User(s)", SelectUser}, {"Pull Puppet Info", GetPuppetInfo},
            {"Find Legendaries", FindLegendaries}, {"List Puppets", ListPuppets},
            {"Find Owners", FindOwner}, {"Generation Functions", GenMenu}
        };
        SetupFunctions = new() {
            {"Add Puppets",AddPuppets}, {"Generate Puppet Links", Puppet_Links},
            {"Generate Junk Links", Junk_Links},
            {"Create Database",CreateCardsDB}, {"Exit", null}
        };

        MainMenu = new SelectionPrompt<string>()
            .Title("Select Function")
            .AddChoices(Functions.Keys);
        SetupMenu = new SelectionPrompt<string>()
            .Title("Select Function")
            .AddChoices(SetupFunctions.Keys);
    }

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

    public async Task<int> Execute()
    {
        AnsiConsole.MarkupLine("[red]ooooooooooo   o      oooooooooo    ooooooo   ooooooooooo\n88  888  88  888      888    888 o888   888o 88  888  88 \n    888     8  88     888oooo88  888     888     888     \n    888    8oooo88    888  88o   888o   o888     888     \n   o888o o88o  o888o o888o  88o8   88ooo88      o888o[/]");
        string Main = AnsiConsole.Ask<string>("Main Nation: ");
        NSAPI.Instance.UserAgent = $"Tarot/{TAROT_VERSION} (By Vleerian, vleerian@hotmail.com in use by {Main})";

        while(true)
        {
            string Operation = AnsiConsole.Prompt(MainMenu);
            if(Operation == "Select User(s)" || Operation == "Generation Functions")
                await Functions[Operation](Owners);
            else if (Owners == null)
                AnsiConsole.MarkupLine("No user(s) selected.");
            else
                await Functions[Operation](Owners);
        }
    }

    async Task ListPuppets(string[] Users)
    {
        var SortMode = AnsiConsole.Prompt(new SelectionPrompt<string>()
        .Title("Select Sorting mode")
        .AddChoices(new[] {
            "1. DeckValue", "2. Junk Value + Bank",
            "3. DV-JV"
        }));
        List<PuppetViewEntry> PuppetData;
        switch(SortMode[0])
        {
            case '1':
                PuppetData = await Database.QueryAsync<PuppetViewEntry>("SELECT * FROM PuppetStats ORDER BY DeckValue DESC");
                break;
            case '2':
                PuppetData = await Database.QueryAsync<PuppetViewEntry>("SELECT * FROM PuppetStats ORDER BY JunkValue + Bank DESC");
                break;
            case '3':
                PuppetData = await Database.QueryAsync<PuppetViewEntry>("SELECT * FROM PuppetStats ORDER BY JunkValue - DeckValue DESC");
                break;
            default:
                return;
        }

        List<DeckViewEntry> DeckData = await Database.QueryAsync<DeckViewEntry>("SELECT * FROM DeckView");

        var table = new Table()
            .MinimalDoubleHeadBorder();;
        table.AddColumn("Puppet").AddColumn("Bank").AddColumn("JV").AddColumn("DV").AddColumn("∆V").AddColumn("Cards").AddColumn("Breakdown");
        foreach(var puppet in PuppetData)
        {
            var Items = MarkWrapMany(puppet.Bank, puppet.JunkValue + puppet.Bank, puppet.DeckValue,
                Math.Round(puppet.DeckValue - puppet.JunkValue, 2), puppet.Num_Cards)
                .Prepend(Linkify(puppet.Name))
                .Append(await GenerateBreakdown(DeckData.Where(P=>P.Owner == puppet.Name).ToArray()));
            table.AddRow(Items);
        }
        AnsiConsole.Write(table);

    }

    async Task FindLegendaries(string[] Users)
    {
        var owners = await Database.QueryAsync<DeckViewEntry>("SELECT * FROM DeckView WHERE RarityInt = 5");
        var table = new Table()
            .MinimalDoubleHeadBorder();
        table.AddColumn("Owner").AddColumn("Card").AddColumn("Rarity").AddColumn("Season");
        foreach(var owner in owners)
            table.AddRow(MarkWrapMany(owner.Name, owner.Season, owner.Rarity).Prepend(Linkify(owner.Owner)));
        AnsiConsole.Write(table);
    }

    async Task FindOwner(string[] Users)
    {
        bool Run = true;
        while(Run)
        {
            string search = AnsiConsole.Ask<string>("Card Name (exit to stop)");
            if(search.Trim().ToLower() == "exit")
                return;
            var card = Helpers.SanitizeName(search);
            var owners = await Database.QueryAsync<DeckViewEntry>("SELECT * FROM DeckView WHERE Name = ?", card);
            var table = new Table();
            table.AddColumn("Owner").AddColumn("Card").AddColumn("Season");
            foreach(var owner in owners)
                table.AddRow(owner.Owner, owner.Name, owner.Season.ToString());
            AnsiConsole.Write(table);
        }
    }

    async Task<DBCard[]> FindCard(string cardName, int season = 0)
    {
        if(season < 0 || season > 3)
            season = 0;
        List<DBCard> cards;
        if(season != 0)
            cards = await Database.QueryAsync<DBCard>("SELECT * FROM Cards WHERE Name = ? AND Season = ?", cardName, season);
        else
            cards = await Database.QueryAsync<DBCard>("SELECT * FROM Cards WHERE Name = ?", cardName, season);
        
        return cards.ToArray();
    }

    async Task SelectUser(string[] users)
    {
        var Users = await Database.QueryScalarsAsync<string>("SELECT DISTINCT User FROM PuppetMap;");
        if(Users.Count == 0)
            throw new Exception("No users present in Database");
        else if(Users.Count == 1)
        {
            Owners = Users.ToArray();
        }
        else
        {
            var prompt = new MultiSelectionPrompt<string>().Title("Select Users");
            foreach(var user in Users)
            {
                prompt.AddChoice(user);
            }
            Owners = AnsiConsole.Prompt(prompt).ToArray();
        }
    }

    async Task GetPuppetInfo(string[] Users)
    {
        DBPuppet[] Puppets = await Database.Table<DBPuppet>().Where(P=>Users.Contains(P.User)).ToArrayAsync();
        await Database.CreateTableAsync<DeckDB>();
        await Database.CreateTableAsync<PuppetData>();
        await Database.DeleteAllAsync<DeckDB>();
        await Database.DeleteAllAsync<PuppetData>();
        foreach(var Puppet in Puppets)
        {
            AnsiConsole.MarkupLine($"Fetching data for {Puppet.Puppet}");
            try{
                var Result = await NSAPI.Instance.GetAPI<CardsAPI>($"https://www.nationstates.net/cgi-bin/api.cgi?q=cards+deck+info;nationname={Puppet.Puppet}");
                var Deck = Result.Data.Deck.Select(D=>new DeckDB() {
                    Owner = Puppet.Puppet,
                    ID = D.ID,
                    Rarity = DBCard.RarityToInt(D.Rarity),
                    Season = D.Season
                }).ToArray();
                await Database.InsertAllAsync(Deck);
                var DeckInfo = Result.Data.Deck_Info;
                await Database.InsertAsync(new PuppetData() {
                    Name = Puppet.Puppet,
                    Bank = DeckInfo.Bank,
                    Deck_Value = DeckInfo.DeckValue,
                    Num_Cards = DeckInfo.Num_Cards
                });
                System.Threading.Thread.Sleep(800);
            }
            catch(Exception e)
            {
                AnsiConsole.MarkupLine($"Failed to get deck information for {Puppet.Puppet}");
                AnsiConsole.WriteException(e);
            }
        }
    }
}