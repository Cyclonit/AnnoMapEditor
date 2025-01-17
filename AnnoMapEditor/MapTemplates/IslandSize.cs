﻿using System;

namespace AnnoMapEditor.MapTemplates
{
    public struct IslandSize
    {
        public static readonly IslandSize Small = new("Small");
        public static readonly IslandSize Medium = new("Medium");
        public static readonly IslandSize Large = new("Large");

        private readonly string value;

        public short? ElementValue { get; set; }

        public int InTiles { get; private init; }
        public bool IsDefault { get; private init; }

        public IslandSize(string? size)
        {
            if (size == "Medium" || size == "Large")
            {
                value = size;
                InTiles = size == "Medium" ? 320 : 384;
                IsDefault = false;
                ElementValue = size == "Medium" ? (short)1 : (short)2;
                return;
            }

            value = "Small";
            InTiles = 192;
            IsDefault = size != "Small";
            ElementValue = 0;
        }

        public IslandSize(short? size)
        {
            ElementValue = size;
            switch (size)
            {
                default:
                    IsDefault = true;
                    value = "Small";
                    InTiles = 192;
                    break;
                case 0:
                    IsDefault = false;
                    value = "Small";
                    InTiles = 192;
                    break;
                case 1:
                    IsDefault = false;
                    value = "Medium";
                    InTiles = 320;
                    break;
                case 2:
                    IsDefault = false;
                    value = "Large";
                    InTiles = 384;
                    break;
            }
        }

        public override string ToString() => value;

        public override bool Equals(object? obj) => obj is IslandSize other && value.Equals(other.value);
        public override int GetHashCode() => value.GetHashCode();

        public static bool operator !=(IslandSize a, IslandSize b) => !a.Equals(b);
        public static bool operator ==(IslandSize a, IslandSize b) => a.Equals(b);
    }
}
