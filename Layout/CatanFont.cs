
using System.Collections.Generic;
using Catan3.Models;

namespace Catan3.Utility
{
    public static class CatanFont
    {
        public static readonly string City = "\uE900";
        public static readonly string Deserter = "\uE901";
        public static readonly string Diplomat = "\uE902";
        public static readonly string Gate = "\uE903";
        public static readonly string Politics = "\uE904";
        public static readonly string Spy = "\uE905";
        public static readonly string Inventor = "\uE906";
        public static readonly string Laurel = "\uE907";
        public static readonly string Merchant = "\uE908";
        public static readonly string NoEntitlement = "\uE90A";
        public static readonly string Science = "\uE901B";
        public static readonly string Pirate = "\uE90C";
        public static readonly string Ship = "\uE90D";
        public static readonly string SolidSheild = "\uE925";
        public static readonly string Settlement = "\uE90E";
        public static readonly string FancySheild = "\uE927";
        public static readonly string Knight = "\uE930";
        public static readonly string Road = "\uE909";
        public static readonly string Metro = "\uE90F";

        public static readonly Dictionary<Entitlement, string> EntitlementGlyph = new()
        {
            {Entitlement.Settlement, Settlement },
            {Entitlement.City, City },
            {Entitlement.PlayKnight, Pirate },
            {Entitlement.Road, Road }
        };

        


    }
}
