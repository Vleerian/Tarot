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

namespace NSDotnet.Models
{
    [Table("Cards")]
    public class DBCard
    {
        public static readonly float[] JunkValues = new[]{
            0.1f, 0.5f, 0.10f, 0.20f, 0.50f, 1.0f
        };
        public static readonly string[] Rarities = new[]{
            "common","uncommon","rare","ultra-rare","epic","legendary"
        };
        public static int RarityToInt(string Rarity) =>
            Array.IndexOf(Rarities, Rarity.ToLower());
        public static string IntToRarity(int Rarity) =>
            Rarities[Rarity];

        public DBCard() { }
        public DBCard(CardAPI card, int season) {
            ID = card.ID;
            Season = season;
            Name = card.Name;
            Region = card.Region;
            Rarity = Array.IndexOf(Rarities, card.Rarity.ToLower());
        }
        public int ID { get; init; }
        public int Season { get; init; }
        public string Name { get; init; }
        public string Region { get; init; }
        public int Rarity { get; init; }
    }
}
