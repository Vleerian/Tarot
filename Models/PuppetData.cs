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

public class PuppetData
{
    [PrimaryKey]
    public string Name { get; init; }
    public float Bank { get; init; }
    public float Deck_Value { get; init; }
    public int Num_Cards { get; init; }
    public int Num_Issues { get; init; }
    public int Num_Packs { get; init; }
}