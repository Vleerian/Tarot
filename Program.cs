using System.Text;
using System.Text.RegularExpressions;

using System.Xml;
using System.Xml.Serialization;

using NSDotnet;
using NSDotnet.Models;

using SQLite;

using Spectre.Console;
using Spectre.Console.Cli;

SQLiteAsyncConnection Database = new ("DeckDB.db");
AnsiConsole.MarkupLine("[red]ooooooooooo   o      oooooooooo    ooooooo   ooooooooooo\n88  888  88  888      888    888 o888   888o 88  888  88 \n    888     8  88     888oooo88  888     888     888     \n    888    8oooo88    888  88o   888o   o888     888     \n   o888o o88o  o888o o888o  88o8   88ooo88      o888o[/]");

string[] Owners = null;
while(true)
{
    var Operation = AnsiConsole.Prompt(new SelectionPrompt<string>()
    .Title("Select Operation")
    .AddChoices(new[] {
        "A. Process Cards Dumps", "B. Add Puppet(s)",
        "C. Select Users", "D. Pull puppet info",
        "E. Find Legendaries", "F. Find Owners",
        "G. List Puppets"
    }));
    switch(Operation[0])
    {
        case 'A':
            await CreateCardsDB();
            break;
        case 'B':
            await AddPuppets();
            break;
        case 'C':
            Owners = await SelectUser();
            break;
        case 'D':
            if(Owners != null)
                await GetPuppetInfo(Owners);
            else
                AnsiConsole.MarkupLine("No user(s) selected.");
            break;
        case 'E':
            if(Owners != null)
                await FindLegendaries(Owners);
            else
                AnsiConsole.MarkupLine("No user(s) selected.");
            break;
        case 'F':
            if(Owners != null)
                await FindOwner(Owners);
            else
                AnsiConsole.MarkupLine("No user(s) selected.");
            break;
        case 'G':
            if(Owners != null)
                await ListPuppets(Owners);
            else
                AnsiConsole.MarkupLine("No user(s) selected.");
            break;
    }
}

async Task ListPuppets(string[] Users)
{
    //var puppets = await Database.QueryAsync<>()
}

async Task FindLegendaries(string[] Users)
{
    var owners = await Database.QueryAsync<DeckViewEntry>("SELECT * FROM DeckView WHERE RarityInt = 5");
    var table = new Table();
    table.AddColumn("Owner").AddColumn("Card").AddColumn("Rarity").AddColumn("Season");
    foreach(var owner in owners)
        table.AddRow(owner.Owner, owner.Name, owner.Season.ToString(), owner.Rarity);
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

async Task AddPuppets()
{
    // Get basic info
    var Owner = AnsiConsole.Ask<string>("Puppet Owner: ");
    AnsiConsole.MarkupLine("Comma separate lists of puppets. Type \"file\" to use a file");
    var Puppets = AnsiConsole.Ask<string>("Puppet(s): ");
    string[] pups;
    // Turn the puppets into an array
    if(Puppets.ToLower() == "file")
    {
        AnsiConsole.MarkupLine("Puppets must be on one line each.");
        var Filename = AnsiConsole.Ask<string>("Enter filename: ");
        pups = File.ReadAllLines(Filename);
    }
    else
    {
        pups = Puppets.Contains(',') ?
            Puppets.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) :
            new[] { Puppets };
    }
    
    // Turn the array of puppets into DBPuppet objects
    var Pups = pups.Select(P=>new DBPuppet(){
        Puppet = Helpers.SanitizeName(P),
        User = Owner
    });

    await Database.CreateTableAsync<DBPuppet>();
    // Insert them into the database
    await Database.InsertAllAsync(Pups);
}

async Task<string[]> SelectUser()
{
    var Users = await Database.QueryScalarsAsync<string>("SELECT DISTINCT User FROM PuppetMap;");
    if(Users.Count == 0)
        throw new Exception("No users present in Database");
    if(Users.Count == 1)
        return Users.ToArray();
    var prompt = new MultiSelectionPrompt<string>().Title("Select Users");
    foreach(var user in Users)
    {
        prompt.AddChoice(user);
    }
    return AnsiConsole.Prompt(prompt).ToArray();
}

async Task GetPuppetInfo(string[] Users)
{
    DBPuppet[] Puppets = await Database.Table<DBPuppet>().Where(P=>Users.Contains(P.User)).ToArrayAsync();
    await Database.CreateTableAsync<DeckDB>();
    await Database.CreateTableAsync<PuppetData>();
    await Database.DeleteAllAsync<DeckDB>();
    await Database.DeleteAllAsync<PuppetData>();
    NSAPI.Instance.UserAgent = "Tarot/0.5 (By Vleerian, vleerian@hotmail.com)";
    foreach(var Puppet in Puppets)
    {
        Console.WriteLine($"Fetching data for {Puppet.Puppet}");
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
            Console.WriteLine($"Failed to get deck information for {Puppet}");
        }
    }
}

async Task CreateCardsDB()
{
    AnsiConsole.MarkupLine("Note: You are reuqired to download the cards dumps yourself.");
    // Create the DBCard table
    await Database.CreateTableAsync<DBCard>();
    for (int i = 0; i < 3; i++)
    {
        int Season = i+1;
        string RawXML = NSAPI.UnzipDump($"cardlist_S{Season}.xml.gz");
        // Mottos can contain non-escaped characters, so we wrap mottos in CDATA tags
        RawXML = Regex.Replace(RawXML, @"<MOTTO>(.*)</MOTTO>", "<MOTTO><![CDATA[$1]]></MOTTO>");
        // Deserialize the dump, convert all the cards to DBCards, then insert them
        var dump = Helpers.BetterDeserialize<CardsDataDump>(RawXML);
        await Database.InsertAllAsync(dump.CardSet.Cards.Select(C => new DBCard(C, Season)));
        // Create a few utiltiy tables to contain basic data
        await Database.ExecuteAsync("CREATE TABLE CommonInfo (\"Rarity\" integer, \"Name\" varchar, \"JunkValue\" float);");
        foreach(var Rarity in DBCard.Rarities)
        {
            int index = Array.IndexOf(DBCard.Rarities, Rarity);
            await Database.ExecuteAsync("INSERT INTO CommonInfo VALUES (?, ?)", index, Rarity, DBCard.JunkValues[index]);
        }
        // Create views
        await Database.ExecuteAsync("CREATE VIEW CardData AS SELECT ID, Season, Cards.Name, CommonInfo.Name AS Rarity, Region, JunkValue, Cards.Rarity AS RarityInt FROM Cards INNER JOIN CommonInfo ON Cards.Rarity = CommonInfo.Rarity;");
        await Database.ExecuteAsync("CREATE VIEW DeckView AS SELECT Deck.ID, Deck.Season, Owner, CardData.Name, CardData.Rarity, CardData.Region, CardData.JunkValue, CardData.RarityInt FROM Deck INNER JOIN CardData ON CardData.ID = Deck.ID AND CardData.Season = Deck.Season");
        await Database.ExecuteAsync("CREATE VIEW PuppetStats AS SELECT Name, ROUND(Bank, 2) AS Bank, JunkValue, ROUND(Deck_Value, 2) AS DeckValue, Num_Cards FROM PuppetData INNER JOIN (SELECT Owner, COUNT(Owner) AS Cards, ROUND(SUM(JunkValue), 2) AS JunkValue FROM DeckView GROUP BY Owner) AS pups_1 ON PuppetData.Name = pups_1.Owner");
    }
}