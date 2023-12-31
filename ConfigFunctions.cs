using System.Text.RegularExpressions;

using NSDotnet;
using NSDotnet.Models;

using Spectre.Console;

public partial class Tarot
{
    async Task AddPuppets()
    {
        // Get basic info
        AnsiConsole.MarkupLine("Read the documentation for the puppet file format");
        var Filename = AnsiConsole.Ask<string>("Enter filename: ");
        var Lines = File.ReadAllLines(Filename);
        // Convert CSV to a format that's nice to work with
        (string Puppet, string Password)[] pups = Lines
            .Select(P=>{
                var x = P.Split(',', StringSplitOptions.TrimEntries);
                return (x[0], x[1]);
            }).ToArray();
        await Database.CreateTableAsync<DBPuppet>();
        AnsiConsole.MarkupLine("Tarot will now ping all your nations to get X-Autologin");
        
        await Database.RunInTransactionAsync(tran => {
            foreach(var pup in pups)
            {
                AnsiConsole.MarkupLine($"Pinging [yellow]{pup.Puppet}[/]");
                NSAPI.Instance.Auth = new NSAuth(NSDotnet.Enums.AuthType.Password, pup.Password);
                // Why is there no async internal transaction?
                Thread.Sleep(600);
                var Result = NSAPI.Instance.MakeRequest($"https://www.nationstates.net/cgi-bin/api.cgi?nation={pup.Puppet}&q=ping")
                    .GetAwaiter().GetResult();
                IEnumerable<string>? Values;
                bool AutoLogin = Result.Headers.TryGetValues("X-Autologin", out Values);
                if(AutoLogin)
                    tran.Insert(new DBPuppet(){
                        Puppet = Helpers.SanitizeName(pup.Puppet),
                        Password = Values.First()
                    });
                else
                    AnsiConsole.MarkupLine($"[red]Failed to ping {pup.Puppet}[/]");
            }
        });
        
        // Remove dupes
        AnsiConsole.MarkupLine($"{pups.Count()} puppets imported. Deleting duplicates.");
        await Database.ExecuteAsync("DELETE FROM PuppetMap WHERE rowid NOT IN ( SELECT MIN(rowid)  FROM PuppetMap  GROUP BY Puppet, Password )"); 
    }

    async Task Puppet_Links()
    {
        string[] Puppets = (await Database.QueryAsync<DBPuppet>("SELECT * FROM PuppetMap;"))
            .Select(P=>P.Puppet).ToArray();
        await TarotHTML.Generate_Puppet_Links(Puppets);
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
        }

        AnsiConsole.WriteLine("Creating views...");
        await CreateViews();
    }

    async Task CreateViews()
    {
        // In case the user is regenerating the views
        await Database.ExecuteAsync("DROP VIEW IF EXISTS PuppetStats");
        await Database.ExecuteAsync("DROP VIEW IF EXISTS DeckView");
        await Database.ExecuteAsync("DROP VIEW IF EXISTS CardData");
        await Database.ExecuteAsync("DROP TABLE IF EXISTS CommonInfo");

        // Update all the tables
        await Database.CreateTableAsync<DBCard>();
        await Database.CreateTableAsync<DBPuppet>();
        await Database.CreateTableAsync<DeckDB>();
        await Database.CreateTableAsync<PuppetData>();
        await Database.CreateTableAsync<DBPuppet>();

        // Create the CommonInfo table used to provide rarity names and junk value
        await Database.ExecuteAsync("CREATE TABLE CommonInfo (\"Rarity\" integer, \"Name\" varchar, \"JunkValue\" float);");
        foreach(var Rarity in DBCard.Rarities)
        {
            int index = Array.IndexOf(DBCard.Rarities, Rarity);
            await Database.ExecuteAsync("INSERT INTO CommonInfo VALUES (?, ?, ?)", index, Rarity, DBCard.JunkValues[index]);
        }
        // Create views
        await Database.ExecuteAsync("CREATE VIEW CardData AS SELECT ID, Season, Cards.Name, CommonInfo.Name AS Rarity, Region, JunkValue, Cards.Rarity AS RarityInt, ROUND(MarketValue, 2) AS MarketValue, ROUND(TopBuy, 2) AS TopBuy, Owners FROM Cards INNER JOIN CommonInfo ON Cards.Rarity = CommonInfo.Rarity");
        await Database.ExecuteAsync("CREATE VIEW DeckView AS SELECT Deck.ID, Deck.Season, Owner, CardData.Name, CardData.Rarity, CardData.Region, CardData.JunkValue, CardData.RarityInt, MarketValue, TopBuy, Owners FROM Deck INNER JOIN CardData ON CardData.ID = Deck.ID AND CardData.Season = Deck.Season");
        await Database.ExecuteAsync("CREATE VIEW PuppetStats AS SELECT Name, ROUND(Bank, 2) AS Bank, IFNULL(JunkValue, 0) AS JunkValue, ROUND(Deck_Value, 2) AS DeckValue, Num_Cards FROM PuppetData LEFT JOIN (SELECT Owner, COUNT(Owner) AS Cards, ROUND(SUM(JunkValue), 2) AS JunkValue FROM DeckView GROUP BY Owner) AS pups_1 ON PuppetData.Name = pups_1.Owner;");
    }
}