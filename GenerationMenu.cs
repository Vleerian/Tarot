using System.Text.RegularExpressions;

using NSDotnet;
using NSDotnet.Models;

using Spectre.Console;

public partial class Tarot
{
    async Task AddPuppets(string[] users)
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
        AnsiConsole.MarkupLine($"{pups.Count()} puppets imported. Deleting duplicates.");
        await Database.ExecuteAsync("DELETE FROM PuppetMap WHERE rowid NOT IN ( SELECT MIN(rowid)  FROM PuppetMap  GROUP BY User, Puppet )"); 
    }

    async Task GenMenu(string[] users)
    {
        while(true)
        {
            var Operation = AnsiConsole.Prompt(SetupMenu);
            if(Operation == "Exit")
                return;
            await SetupFunctions[Operation](Owners);
        }
    }

    async Task Puppet_Links(string[] users)
    {
        string[] Puppets = (await Database.QueryAsync<DBPuppet>("SELECT * FROM PuppetMap;"))
            .Select(P=>P.Puppet).ToArray();
        await TarotHTML.Generate_Puppet_Links(Puppets);
    }

    async Task Junk_Links(string[] users)
    {
        float Threshhold = (float)Math.Round(AnsiConsole.Ask<float>("∆V Threshhold: "), 2);
        var Puppets = await Database.QueryAsync<PuppetViewEntry>("SELECT * FROM PuppetStats WHERE DeckValue - JunkValue < ?;", Threshhold);
        var Cards = (await Database.Table<DeckViewEntry>().ToListAsync())
            .GroupBy( C => C.Owner)
            .Where( G => Puppets.Any(P=>P.Name == G.First().Owner))
            .Select( C => (C.First().Owner, C.ToArray()))
            .ToArray();
        await TarotHTML.Generate_Junk_Links(Cards);
    }

    async Task CreateCardsDB(string[] users)
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
            await Database.ExecuteAsync("CREATE VIEW PuppetStats AS SELECT Name, ROUND(Bank, 2) AS Bank, IFNULL(JunkValue, 0) AS JunkValue, ROUND(Deck_Value, 2) AS DeckValue, Num_Cards FROM PuppetData LEFT JOIN (SELECT Owner, COUNT(Owner) AS Cards, ROUND(SUM(JunkValue), 2) AS JunkValue FROM DeckView GROUP BY Owner) AS pups_1 ON PuppetData.Name = pups_1.Owner;");
        }
    }
}