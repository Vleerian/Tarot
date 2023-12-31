using SQLite;

#region License
/*
Nationstates DotNet Core Library
Copyright (C) 2023 Vleerian R

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
#endregion

[Table("DeckView")]
public class DeckViewEntry
{
    public int ID { get; init; }
    public int Season { get; init; }
    public string Owner { get; init; }
    public string Name { get; init; }
    public string Rarity { get; init; }
    public string Region { get; init; }
    public float JunkValue { get; init; }
    public int RarityInt { get; init; }
    public float MarketValue { get; init; }
    public float TopBuy { get; init; }
    public string owners { get; init; }
    [Ignore]
    public string[] Owners {
        get {
            if(owners == null || owners == string.Empty )
                return new string[0];
            if(owners.Contains(","))
                return owners.Split(",");
            return new string[]{owners};
        }
    }
}
